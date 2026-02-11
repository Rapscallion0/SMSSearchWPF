using System.Threading.Tasks;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly IConfigService _configService;

        public SettingsRepository(IConfigService configService)
        {
            _configService = configService;
        }

        public string? GetValue(string section, string key)
        {
            return _configService.GetValue(section, key);
        }

        public void SetValue(string section, string key, string value)
        {
            _configService.SetValue(section, key, value);
        }

        public void ClearSection(string section)
        {
            _configService.ClearSection(section);
        }

        public Task SaveAsync(string section, string key, string value)
        {
            _configService.SetValue(section, key, value);
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
