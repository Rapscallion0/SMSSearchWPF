using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using SMS_Search.Services;

namespace SMS_Search.Utils
{
    public class UpdateInfo
    {
        public string? Version { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ReleaseUrl { get; set; }
        public string? Changelog { get; set; }
        public bool IsNewer { get; set; }
    }

    public class UpdateChecker
    {
        private const string RepoOwner = "Rapscallion0";
        private const string RepoName = "SMSSearchWPF";
        private const string GitHubApiUrl = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest";

        private readonly IDialogService _dialogService;
        private readonly ILoggerService _loggerService;

        public UpdateChecker(IDialogService dialogService, ILoggerService loggerService)
        {
            _dialogService = dialogService;
            _loggerService = loggerService;
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                _loggerService.LogDebug("Starting background check for updates...");
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "SMS-Search-Updater");
                    client.Timeout = TimeSpan.FromSeconds(10);

                    string json = await client.GetStringAsync(GitHubApiUrl);

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("tag_name", out JsonElement tagNameElement))
                        {
                            string? tagName = tagNameElement.GetString();
                            string? releaseUrl = root.TryGetProperty("html_url", out JsonElement urlElement) ? urlElement.GetString() : "";
                            string? body = root.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() : "";

                            if (string.IsNullOrEmpty(tagName)) return new UpdateInfo { IsNewer = false };

                            string versionStr = tagName.TrimStart('v', 'V');
                            if (Version.TryParse(versionStr, out Version? remoteVersion) && remoteVersion != null)
                            {
                                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                                string? currentVersionStr = exePath != null ? FileVersionInfo.GetVersionInfo(exePath).FileVersion : null;

                                if (Version.TryParse(currentVersionStr, out Version? currentVersion) && currentVersion != null && remoteVersion > currentVersion)
                                {
                                    string? downloadUrl = null;
                                    if (root.TryGetProperty("assets", out JsonElement assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (JsonElement asset in assetsElement.EnumerateArray())
                                        {
                                            if (asset.TryGetProperty("name", out JsonElement nameElement) &&
                                                asset.TryGetProperty("browser_download_url", out JsonElement downloadUrlElement))
                                            {
                                                string? name = nameElement.GetString();
                                                if (!string.IsNullOrEmpty(name) && name.Equals("SMS_Search.zip", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    downloadUrl = downloadUrlElement.GetString();
                                                    _loggerService.LogDebug($"Found download asset: {name} -> {downloadUrl}");
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    _loggerService.LogInfo($"Update check: New version found ({tagName})");
                                    return new UpdateInfo
                                    {
                                        Version = tagName,
                                        DownloadUrl = downloadUrl,
                                        ReleaseUrl = releaseUrl,
                                        Changelog = body,
                                        IsNewer = true
                                    };
                                }
                                else
                                {
                                    _loggerService.LogDebug($"Update check: Current version is up to date or newer. Remote: {remoteVersion}, Current: {currentVersion}");
                                }
                            }
                            else
                            {
                                _loggerService.LogDebug($"Update check: Could not parse remote version '{versionStr}'");
                            }
                        }
                        else
                        {
                            _loggerService.LogDebug("Update check: 'tag_name' property not found in JSON response.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Update check threw an exception.", ex);
            }

            return new UpdateInfo { IsNewer = false };
        }

        public async Task DownloadAndInstallUpdateAsync(UpdateInfo info, IProgress<double>? progressCallback = null, IProgress<string>? statusCallback = null)
        {
            _loggerService.LogInfo("Starting download and install for new update...");

            if (string.IsNullOrEmpty(info.DownloadUrl))
            {
                _loggerService.LogWarning("Download URL is empty. Opening release URL in browser.");
                if (!string.IsNullOrEmpty(info.ReleaseUrl))
                {
                    Process.Start(new ProcessStartInfo(info.ReleaseUrl) { UseShellExecute = true });
                }
                else
                {
                    _loggerService.LogError("No download URL and no release URL found.");
                    _dialogService.ShowError("No download URL found for the new version.", "Update Error");
                }
                return;
            }

            string tempPath = Path.GetTempPath();
            string zipPath = Path.Combine(tempPath, "SMS_Search_Update.zip");
            string scriptPath = Path.Combine(tempPath, "sms_update.ps1");

            try
            {
                _loggerService.LogDebug($"Downloading update from {info.DownloadUrl} to {zipPath}");
                statusCallback?.Report("Downloading update...");
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "SMS-Search-Updater");

                    using (var response = await client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        long? totalBytes = response.Content.Headers.ContentLength;
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalRead = 0;
                            int bytesRead;

                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (totalBytes.HasValue && progressCallback != null)
                                {
                                    double percentage = (double)totalRead / totalBytes.Value * 100;
                                    progressCallback.Report(percentage);
                                }
                            }
                        }
                    }
                }

                _loggerService.LogDebug("Download complete. Preparing installation script...");
                statusCallback?.Report("Preparing installation script...");
                string? currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                if (currentExe == null) return;

                string appDir = Path.GetDirectoryName(currentExe) ?? "";
                string pid = Process.GetCurrentProcess().Id.ToString();

                string scriptContent = $@"
$pidToWait = {pid}
$appDir = '{appDir}'
$zipPath = '{zipPath}'
$exePath = '{currentExe}'
$scriptPath = '{scriptPath}'

# Wait for the main app to close
Write-Host 'Waiting for SMS Search to exit...'
$process = Get-Process -Id $pidToWait -ErrorAction SilentlyContinue
if ($process) {{
    $process.WaitForExit(10000)
}}

# Extract the zip contents into the application directory
Write-Host 'Extracting files...'
Expand-Archive -Path $zipPath -DestinationPath $appDir -Force

# Restart the application
Write-Host 'Restarting application...'
Start-Process -FilePath $exePath

# Cleanup temporary files
Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue

# We cannot easily delete the script file that is currently executing directly in the same process in some cases
# so we start a small cmd to clean it up after a brief delay
Start-Process -FilePath 'cmd.exe' -ArgumentList ""/c timeout /t 2 /nobreak > NUL & del `""$scriptPath`"""" -WindowStyle Hidden
";

                File.WriteAllText(scriptPath, scriptContent);

                _loggerService.LogInfo("Starting Powershell installation script and shutting down application.");
                statusCallback?.Report("Restarting and installing...");
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe")
                {
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                };
                Process.Start(psi);

                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Update failed during download/install phase.", ex);
                _dialogService.ShowError("Update failed: " + ex.Message, "Update Error");
            }
        }
    }
}
