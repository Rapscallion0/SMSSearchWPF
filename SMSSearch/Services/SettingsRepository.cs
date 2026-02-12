using System.Threading.Tasks;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly IConfigService _configService;
        private readonly ILoggerService _logger;

        public SettingsRepository(IConfigService configService, ILoggerService logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public string? GetValue(string section, string key)
        {
            return _configService.GetValue(section, key);
        }

        public void SetValue(string section, string key, string value)
        {
            var oldValue = _configService.GetValue(section, key);
            _configService.SetValue(section, key, value);

            if (oldValue != value)
            {
                _logger.LogInfo($"Config changed: Section: {section}, Key: {key}, Old Value: {oldValue ?? "null"}, New Value: {value}");
            }
        }

        public void ClearSection(string section)
        {
            _configService.ClearSection(section);
        }

        public Task SaveAsync(string section, string key, string value)
        {
            SetValue(section, key, value);
            return SaveAsync();
        }

        public Task SaveAsync()
        {
            // Wrap the synchronous ConfigManager.Save() in a Task to satisfy the requirement
            // for async I/O and prevent blocking the UI thread during disk writes.
            return Task.Run(() => _configService.Save());
        }
    }
}
