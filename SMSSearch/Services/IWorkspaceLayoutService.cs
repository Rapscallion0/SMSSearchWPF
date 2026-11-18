#nullable enable
using System;
using System.Collections.Generic;

namespace SMS_Search.Services
{
    public sealed class WorkspaceLayoutProfile
    {
        public string Name { get; set; } = string.Empty;
        public string LayoutXml { get; set; } = string.Empty;
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public double? WindowWidth { get; set; }
        public double? WindowHeight { get; set; }
        public string? WindowState { get; set; }
        public bool RememberWindowPosition { get; set; }
        public bool SuppressCloseSavePrompt { get; set; }
        public SMS_Search.Data.LayoutSearchTabPreference SearchTabPreference { get; set; } = SMS_Search.Data.LayoutSearchTabPreference.Function;
        public SMS_Search.Data.LayoutSearchModePreference SearchModePreference { get; set; } = SMS_Search.Data.LayoutSearchModePreference.Simple;
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class ActiveWorkspaceState
    {
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public double? WindowWidth { get; set; }
        public double? WindowHeight { get; set; }
        public string? WindowState { get; set; }
        public string? ActiveProfileName { get; set; }
    }

    public interface IWorkspaceLayoutService
    {
        event EventHandler? ProfilesChanged;

        string? LoadActiveLayout();
        void SaveActiveLayout(string layoutXml);
        ActiveWorkspaceState? LoadActiveWindowState();
        void SaveActiveWindowState(ActiveWorkspaceState state);

        string? GetStartupLayoutName();
        void SetStartupLayoutName(string? profileName);
        bool GetDefaultRememberWindowPosition();

        IReadOnlyList<WorkspaceLayoutProfile> GetProfiles();
        WorkspaceLayoutProfile? GetProfile(string name);
        void SaveProfile(
            string name,
            string layoutXml,
            double? windowLeft = null,
            double? windowTop = null,
            double? windowWidth = null,
            double? windowHeight = null,
            string? windowState = null,
            bool? rememberWindowPosition = null);
        void SetProfileRememberWindowPosition(string name, bool rememberWindowPosition);
        void SetProfileSuppressCloseSavePrompt(string name, bool suppress);
        (SMS_Search.Data.LayoutSearchTabPreference Tab, SMS_Search.Data.LayoutSearchModePreference Mode) GetDefaultSearchPreferences();
        void SetProfileSearchPreferences(string name, SMS_Search.Data.LayoutSearchTabPreference tabPreference, SMS_Search.Data.LayoutSearchModePreference modePreference);
        void RenameProfile(string oldName, string newName);
        void DeleteProfile(string name);
        string? GetProfileLayout(string name);
    }
}
