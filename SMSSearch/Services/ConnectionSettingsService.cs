using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class ConnectionSettingsService : IConnectionSettingsService
    {
        private const string SectionName = "CONNECTION_PROFILES";
        private const string ItemsKey = "ITEMS";
        private const string ActiveIdKey = "ACTIVE_ID";

        private readonly IConfigService _configService;
        private readonly IStateService? _stateService;

        public event EventHandler? ProfilesChanged;

        public ConnectionSettingsService(IConfigService configService, IStateService? stateService = null)
        {
            _configService = configService;
            _stateService = stateService;
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

        public IReadOnlyList<ConnectionProfile> GetProfiles()
        {
            if (_stateService == null)
            {
                return new[] { CreateDefaultProfileFromConfig() };
            }

            var profiles = LoadProfilesInternal();
            if (profiles.Count == 0)
            {
                var defaultProfile = CreateDefaultProfileFromConfig();
                profiles.Add(defaultProfile);
                PersistProfiles(profiles, defaultProfile.Id);
            }

            return profiles;
        }

        public ConnectionProfile? GetActiveProfile()
        {
            var profiles = GetProfiles();
            if (profiles.Count == 0) return null;

            string? activeId = _stateService?.GetValue(SectionName, ActiveIdKey);
            return profiles.FirstOrDefault(p => p.Id == activeId) ?? profiles[0];
        }

        public ConnectionProfile SaveProfile(ConnectionProfile profile, string? plainPassword = null, bool preserveExistingPasswordIfNull = true)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.Name))
                throw new ArgumentException("Profile name is required.", nameof(profile));

            var profiles = GetProfiles().ToList();
            var existing = profiles.FirstOrDefault(p => p.Id == profile.Id);

            string? encryptedPassword = existing?.EncryptedPassword ?? profile.EncryptedPassword;
            if (plainPassword != null)
            {
                encryptedPassword = string.IsNullOrEmpty(plainPassword) ? null : GeneralUtils.Encrypt(plainPassword);
            }
            else if (!preserveExistingPasswordIfNull)
            {
                encryptedPassword = null;
            }

            var saved = new ConnectionProfile
            {
                Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id,
                Name = profile.Name.Trim(),
                Server = profile.Server?.Trim() ?? string.Empty,
                Database = profile.Database?.Trim() ?? string.Empty,
                IsWindowsAuth = profile.IsWindowsAuth,
                User = profile.User?.Trim() ?? string.Empty,
                EncryptedPassword = encryptedPassword,
                Environment = profile.Environment
            };

            if (existing != null)
            {
                int idx = profiles.IndexOf(existing);
                profiles[idx] = saved;
            }
            else
            {
                profiles.Add(saved);
            }

            string activeId = _stateService?.GetValue(SectionName, ActiveIdKey) ?? saved.Id;
            if (activeId == saved.Id)
            {
                ApplyProfileToConfig(saved);
            }

            PersistProfiles(profiles, activeId);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return saved;
        }

        public void DeleteProfile(string id)
        {
            if (_stateService == null || string.IsNullOrWhiteSpace(id)) return;

            var profiles = GetProfiles().ToList();
            if (profiles.Count <= 1) return; // Always keep at least one profile

            int removed = profiles.RemoveAll(p => p.Id == id);
            if (removed == 0) return;

            string? activeId = _stateService.GetValue(SectionName, ActiveIdKey);
            if (activeId == id)
            {
                activeId = profiles[0].Id;
                ApplyProfileToConfig(profiles[0]);
                WeakReferenceMessenger.Default.Send(new ConnectionSettingsChangedMessage(true));
            }

            PersistProfiles(profiles, activeId ?? profiles[0].Id);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ActivateProfile(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var profiles = GetProfiles().ToList();
            var target = profiles.FirstOrDefault(p => p.Id == id);
            if (target == null) return;

            ApplyProfileToConfig(target);
            PersistProfiles(profiles, target.Id);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            WeakReferenceMessenger.Default.Send(new ConnectionSettingsChangedMessage(true));
        }

        private ConnectionProfile CreateDefaultProfileFromConfig()
        {
            string server = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Server) ?? "";
            string database = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database) ?? "";
            bool isWindowsAuth = !bool.TryParse(_configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth), out bool parsed) || parsed;
            string user = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlUser) ?? "";
            string? encryptedPass = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlPassword);

            return new ConnectionProfile
            {
                Name = "Default",
                Server = server,
                Database = database,
                IsWindowsAuth = isWindowsAuth,
                User = user,
                EncryptedPassword = encryptedPass,
                Environment = EnvironmentTag.None
            };
        }

        private void ApplyProfileToConfig(ConnectionProfile profile)
        {
            _configService.SetValue(AppSettings.Sections.Connection, AppSettings.Keys.Server, profile.Server);
            _configService.SetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database, profile.Database);
            _configService.SetValue(AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth, profile.IsWindowsAuth.ToString());
            _configService.SetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlUser, profile.User);
            _configService.SetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlPassword, profile.EncryptedPassword ?? string.Empty);
            _configService.Save();
        }

        private List<ConnectionProfile> LoadProfilesInternal()
        {
            string? json = _stateService?.GetValue(SectionName, ItemsKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<ConnectionProfile>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<ConnectionProfile>>(json) ?? new List<ConnectionProfile>();
            }
            catch
            {
                return new List<ConnectionProfile>();
            }
        }

        private void PersistProfiles(List<ConnectionProfile> profiles, string activeId)
        {
            if (_stateService == null) return;
            string json = JsonSerializer.Serialize(profiles);
            _stateService.SetValue(SectionName, ItemsKey, json);
            _stateService.SetValue(SectionName, ActiveIdKey, activeId);
            _stateService.Save();
        }
    }
}
