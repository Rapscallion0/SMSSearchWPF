using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class ConnectionSettingsService : IConnectionSettingsService
    {
        private readonly IConfigService _configService;

        public ConnectionSettingsService(IConfigService configService)
        {
            _configService = configService;
        }

        public ConnectionSettingsInfo GetConnectionSettings()
        {
            string server = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Server) ?? "";
            string database = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database) ?? "";
            string user = "";
            string? decryptedPass = null;

            bool isWindowsAuth = true;
            if (bool.TryParse(_configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth), out bool parsed))
            {
                isWindowsAuth = parsed;
            }

            if (!isWindowsAuth)
            {
                user = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlUser) ?? "";
                string? pass = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlPassword);
                decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;
            }

            return new ConnectionSettingsInfo(server, database, user, decryptedPass, isWindowsAuth);
        }
    }
}

