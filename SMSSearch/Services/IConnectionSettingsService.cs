namespace SMS_Search.Services
{
    public sealed record ConnectionSettingsInfo(
        string Server,
        string Database,
        string User,
        string? Password,
        bool IsWindowsAuth);

    public interface IConnectionSettingsService
    {
        ConnectionSettingsInfo GetConnectionSettings();
    }
}

