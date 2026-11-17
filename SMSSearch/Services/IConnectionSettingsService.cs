using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SMS_Search.Services
{
    public enum EnvironmentTag
    {
        [Description("None")]
        None,
        [Description("DEV")]
        Dev,
        [Description("QA")]
        Qa,
        [Description("PROD")]
        Prod
    }

    public sealed class ConnectionProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Default";
        public string Server { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public bool IsWindowsAuth { get; set; } = true;
        public string User { get; set; } = string.Empty;
        public string? EncryptedPassword { get; set; }
        public EnvironmentTag Environment { get; set; } = EnvironmentTag.None;
    }

    public sealed record ConnectionSettingsInfo(
        string Server,
        string Database,
        string User,
        string? Password,
        bool IsWindowsAuth);

    public interface IConnectionSettingsService
    {
        event EventHandler? ProfilesChanged;

        ConnectionSettingsInfo GetConnectionSettings();
        IReadOnlyList<ConnectionProfile> GetProfiles();
        ConnectionProfile? GetActiveProfile();
        ConnectionProfile SaveProfile(ConnectionProfile profile, string? plainPassword = null, bool preserveExistingPasswordIfNull = true);
        void DeleteProfile(string id);
        void ActivateProfile(string id);
    }
}
