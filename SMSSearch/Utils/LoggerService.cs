using System;
using Serilog;

namespace SMS_Search.Utils
{
    public interface ILoggerService
    {
        void Log(LogLevel level, string message);
        void LogError(string message, Exception ex = null);
        void LogInfo(string message);
        void LogDebug(string message);
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
        private readonly Serilog.ILogger _logger;

        public LoggerService(string appName)
        {
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File($"logs/{appName}_.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
                .CreateLogger();
        }

        public void Log(LogLevel level, string message)
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

        public void LogError(string message, Exception ex = null)
        {
            if (ex != null)
                _logger.Error(ex, message);
            else
                _logger.Error(message);
        }

        public void LogInfo(string message) => _logger.Information(message);
        public void LogDebug(string message) => _logger.Debug(message);
    }
}
