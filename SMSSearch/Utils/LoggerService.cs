using System;
using System.IO;
using System.Reflection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace SMS_Search.Utils
{
    public interface ILoggerService : IDisposable
    {
        void LogStartupInfo();
        void Log(LogLevel level, string message);
        void LogError(string message, Exception? ex = null);
        void LogWarning(string message);
        void LogInfo(string message);
        void LogDebug(string message);
        void LogCritical(string message);
        void ApplyConfig();
        string GetCurrentLogPath();
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public class LoggerService : ILoggerService
    {
        private Serilog.Core.Logger? _logger;
        private readonly IConfigService _configService;
        private readonly string _appName = "SMSSearch_log";
        private string _logDirectory;

        public LoggerService(IConfigService configService)
        {
            _configService = configService;
            // Initialize with a safe default, will be refined in ApplyConfig or helper
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            ApplyConfig();
        }

        private string ResolveLogDirectory()
        {
            // 1. Try Base Directory
            string localLogs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (TryCreateAndWrite(localLogs))
            {
                return localLogs;
            }

            // 2. Try LocalAppData
            string appDataLogs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMS Search", "logs");
            if (TryCreateAndWrite(appDataLogs))
            {
                return appDataLogs;
            }

            // 3. Fallback to Temp
            string tempLogs = Path.Combine(Path.GetTempPath(), "SMS Search", "logs");
            if (TryCreateAndWrite(tempLogs))
            {
                return tempLogs;
            }

            // If all else fails, return localLogs and let Serilog fail/complain later, or return null?
            return localLogs;
        }

        private bool TryCreateAndWrite(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                // Verify write permission
                string testFile = Path.Combine(path, $".write_test_{Guid.NewGuid()}");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ApplyConfig()
        {
            try
            {
                // Resolve directory first
                _logDirectory = ResolveLogDirectory();

                // Read settings
                string? enabledStr = _configService.GetValue("LOGGING", "ENABLED");
                bool isEnabled = enabledStr != "0"; // Default to true

                string? levelStr = _configService.GetValue("LOGGING", "LEVEL");
                LogEventLevel minimumLevel = LogEventLevel.Information;

                // Map "Critical" to Fatal if stored that way, or just parse
                if (!string.IsNullOrEmpty(levelStr))
                {
                    if (string.Equals(levelStr, "Critical", StringComparison.OrdinalIgnoreCase))
                    {
                        minimumLevel = LogEventLevel.Fatal;
                    }
                    else if (string.Equals(levelStr, "Info", StringComparison.OrdinalIgnoreCase))
                    {
                        minimumLevel = LogEventLevel.Information;
                    }
                    else if (Enum.TryParse(levelStr, true, out LogEventLevel parsedLevel))
                    {
                        minimumLevel = parsedLevel;
                    }
                }

                string? retentionStr = _configService.GetValue("LOGGING", "RETENTION");
                int retentionDays = 14;
                if (int.TryParse(retentionStr, out int r))
                {
                    retentionDays = r;
                }

                // Check if log file exists BEFORE creating logger
                bool isNewFile = false;
                string currentPath = GetCurrentLogPath();
                if (!File.Exists(currentPath))
                {
                    isNewFile = true;
                }

                // Dispose old logger if exists before creating new one to release file lock
                _logger?.Dispose();
                _logger = null;

                Serilog.Core.Logger? newLogger = null;

                if (isEnabled)
                {
                    // Use the resolved _logDirectory
                    string logPath = Path.Combine(_logDirectory, $"{_appName}_.json");

                    newLogger = new LoggerConfiguration()
                        .MinimumLevel.Is(minimumLevel)
                        .WriteTo.File(new JsonFormatter(renderMessage: true), logPath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: retentionDays,
                            shared: true)
                        .CreateLogger();
                }

                _logger = newLogger;

                if (isEnabled && isNewFile && _logger != null)
                {
                    LogStartupInfo();
                }
            }
            catch (Exception)
            {
                // If logging configuration fails entirely, we should not crash the app.
                // Leave _logger as null.
                // Optionally: Console.WriteLine(ex);
            }
        }

        public void LogStartupInfo()
        {
            if (_logger == null) return;
            try
            {
                var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";
                var settings = _configService.GetAllSettings();
                _logger.Information("Application Startup. Version: {Version}. Settings: {@Settings}", version, settings);
            }
            catch { /* Ignore logging failures */ }
        }

        public string GetCurrentLogPath()
        {
            // Predict the current log file path
            // Serilog rolling file with RollingInterval.Day appends yyyyMMdd before extension
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string fileName = $"{_appName}_{datePart}.json";
            return Path.Combine(_logDirectory, fileName);
        }

        public void Log(LogLevel level, string message)
        {
             if (_logger == null) return;
             try
             {
                 switch (level)
                 {
                     case LogLevel.Debug: _logger.Debug(message); break;
                     case LogLevel.Info: _logger.Information(message); break;
                     case LogLevel.Warning: _logger.Warning(message); break;
                     case LogLevel.Error: _logger.Error(message); break;
                     case LogLevel.Critical: _logger.Fatal(message); break;
                 }
             }
             catch { /* Ignore logging failures */ }
        }

        public void LogError(string message, Exception? ex = null)
        {
            if (_logger == null) return;
            try
            {
                if (ex != null)
                    _logger.Error(ex, message);
                else
                    _logger.Error(message);
            }
            catch { /* Ignore logging failures */ }
        }

        public void LogWarning(string message)
        {
            try { _logger?.Warning(message); } catch {}
        }
        public void LogInfo(string message)
        {
            try { _logger?.Information(message); } catch {}
        }
        public void LogDebug(string message)
        {
            try { _logger?.Debug(message); } catch {}
        }
        public void LogCritical(string message)
        {
            try { _logger?.Fatal(message); } catch {}
        }

        public void Dispose()
        {
            _logger?.Dispose();
        }
    }
}
