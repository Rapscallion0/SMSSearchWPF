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

        public LoggerService(IConfigService configService)
        {
            _configService = configService;
            ApplyConfig();
        }

        public void ApplyConfig()
        {
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
            // This is crucial for the requirement "On first log creation of the day"
            bool isNewFile = false;
            string currentPath = GetCurrentLogPath();
            if (!File.Exists(currentPath))
            {
                isNewFile = true;
            }

            Serilog.Core.Logger? newLogger = null;

            if (isEnabled)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", $"{_appName}_.json");
                newLogger = new LoggerConfiguration()
                    .MinimumLevel.Is(minimumLevel)
                    .WriteTo.File(new JsonFormatter(renderMessage: true), logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: retentionDays)
                    .CreateLogger();
            }

            // Dispose old logger if exists
            var oldLogger = _logger;
            _logger = newLogger;
            oldLogger?.Dispose();

            if (isEnabled && isNewFile && _logger != null)
            {
                LogStartupInfo();
            }
        }

        public void LogStartupInfo()
        {
            if (_logger == null) return;
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";
            var settings = _configService.GetAllSettings();
            _logger.Information("Application Startup. Version: {Version}. Settings: {@Settings}", version, settings);
        }

        public string GetCurrentLogPath()
        {
            // Predict the current log file path
            // Serilog rolling file with RollingInterval.Day appends yyyyMMdd before extension
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string fileName = $"{_appName}_{datePart}.json";
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", fileName);
        }

        public void Log(LogLevel level, string message)
        {
             if (_logger == null) return;

             switch (level)
             {
                 case LogLevel.Debug: _logger.Debug(message); break;
                 case LogLevel.Info: _logger.Information(message); break;
                 case LogLevel.Warning: _logger.Warning(message); break;
                 case LogLevel.Error: _logger.Error(message); break;
                 case LogLevel.Critical: _logger.Fatal(message); break;
             }
        }

        public void LogError(string message, Exception? ex = null)
        {
            if (_logger == null) return;
            if (ex != null)
                _logger.Error(ex, message);
            else
                _logger.Error(message);
        }

        public void LogWarning(string message) => _logger?.Warning(message);
        public void LogInfo(string message) => _logger?.Information(message);
        public void LogDebug(string message) => _logger?.Debug(message);
        public void LogCritical(string message) => _logger?.Fatal(message);

        public void Dispose()
        {
            _logger?.Dispose();
        }
    }
}
