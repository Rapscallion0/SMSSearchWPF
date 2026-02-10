using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SMS_Search.Utils
{
    public class UpdateInfo
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseUrl { get; set; }
        public string Changelog { get; set; }
        public bool IsNewer { get; set; }
    }

    public class UpdateChecker
    {
        private const string RepoOwner = "Rapscallion0";
        private const string RepoName = "SMS-Search";
        private const string GitHubApiUrl = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest";

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
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
                            string tagName = tagNameElement.GetString();
                            string releaseUrl = root.TryGetProperty("html_url", out JsonElement urlElement) ? urlElement.GetString() : "";
                            string body = root.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() : "";

                            if (string.IsNullOrEmpty(tagName)) return new UpdateInfo { IsNewer = false };

                            string versionStr = tagName.TrimStart('v', 'V');
                            if (Version.TryParse(versionStr, out Version remoteVersion))
                            {
                                Version currentVersion = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;

                                if (remoteVersion > currentVersion)
                                {
                                    string downloadUrl = null;
                                    if (root.TryGetProperty("assets", out JsonElement assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (JsonElement asset in assetsElement.EnumerateArray())
                                        {
                                            if (asset.TryGetProperty("name", out JsonElement nameElement) &&
                                                asset.TryGetProperty("browser_download_url", out JsonElement downloadUrlElement))
                                            {
                                                string name = nameElement.GetString();
                                                if (!string.IsNullOrEmpty(name) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    downloadUrl = downloadUrlElement.GetString();
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    return new UpdateInfo
                                    {
                                        Version = tagName,
                                        DownloadUrl = downloadUrl,
                                        ReleaseUrl = releaseUrl,
                                        Changelog = body,
                                        IsNewer = true
                                    };
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail
            }

            return new UpdateInfo { IsNewer = false };
        }

        public async Task PerformUpdate(UpdateInfo info)
        {
            if (string.IsNullOrEmpty(info.DownloadUrl))
            {
                if (!string.IsNullOrEmpty(info.ReleaseUrl))
                {
                    Process.Start(new ProcessStartInfo(info.ReleaseUrl) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("No download URL found for the new version.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            string tempPath = Path.GetTempPath();
            string installerPath = Path.Combine(tempPath, "SMS_Search_Update.exe");

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "SMS-Search-Updater");
                    var data = await client.GetByteArrayAsync(info.DownloadUrl);
                    File.WriteAllBytes(installerPath, data);
                }

                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string batchPath = Path.Combine(tempPath, "sms_update.bat");
                string pid = Process.GetCurrentProcess().Id.ToString();

                string batchContent =
                    "@echo off\r\n" +
                    "timeout /t 2 /nobreak > NUL\r\n" +
                    ":loop\r\n" +
                    "tasklist /FI \"PID eq " + pid + "\" 2>NUL | find /I /N \"" + pid + "\">NUL\r\n" +
                    "if \"%ERRORLEVEL%\"==\"0\" (\r\n" +
                    "    timeout /t 1 /nobreak > NUL\r\n" +
                    "    goto loop\r\n" +
                    ")\r\n" +
                    "copy /Y \"" + installerPath + "\" \"" + currentExe + "\"\r\n" +
                    "start \"\" \"" + currentExe + "\"\r\n" +
                    "del \"" + installerPath + "\"\r\n" +
                    "del \"%~f0\"\r\n";

                File.WriteAllText(batchPath, batchContent);

                ProcessStartInfo psi = new ProcessStartInfo(batchPath);
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.UseShellExecute = false;
                Process.Start(psi);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed: " + ex.Message, "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
