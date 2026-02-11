using System.Threading.Tasks;

namespace SMS_Search.Services
{
    public interface ISettingsRepository
    {
        string? GetValue(string section, string key);
        void SetValue(string section, string key, string value);
        void ClearSection(string section);
        Task SaveAsync(string section, string key, string value);
        Task SaveAsync(); // To commit changes to disk
    }
}
