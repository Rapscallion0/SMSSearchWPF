#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class WorkspaceLayoutService : IWorkspaceLayoutService
    {
        public const string BuiltInDefaultName = "Default";

        private const string SectionName = "WORKSPACE_LAYOUT";
        private const string ActiveXmlKey = "ACTIVE_XML";
        private const string ActiveWindowKey = "ACTIVE_WINDOW";
        private const string StartupProfileKey = "STARTUP_PROFILE";
        private const string DefaultRememberPositionKey = "DEFAULT_REMEMBER_POSITION";
        private const string DefaultSearchTabKey = "DEFAULT_SEARCH_TAB";
        private const string DefaultSearchModeKey = "DEFAULT_SEARCH_MODE";
        private const string ProfilesKey = "PROFILES";

        private readonly IStateService _state;
        private readonly ILoggerService _logger;

        public event EventHandler? ProfilesChanged;

        public WorkspaceLayoutService(IStateService state, ILoggerService logger)
        {
            _state = state;
            _logger = logger;
        }

        public static bool IsBuiltInDefault(string? name) =>
            string.Equals(name?.Trim(), BuiltInDefaultName, StringComparison.OrdinalIgnoreCase);

        public string? LoadActiveLayout()
        {
            string? xml = _state.GetValue(SectionName, ActiveXmlKey);
            return string.IsNullOrWhiteSpace(xml) ? null : xml;
        }

        public void SaveActiveLayout(string layoutXml)
        {
            if (string.IsNullOrWhiteSpace(layoutXml)) return;
            _state.SetValue(SectionName, ActiveXmlKey, layoutXml);
            _state.Save();
        }

        public ActiveWorkspaceState? LoadActiveWindowState()
        {
            string? json = _state.GetValue(SectionName, ActiveWindowKey);
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonSerializer.Deserialize<ActiveWorkspaceState>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to deserialize active workspace window state: {Message}", ex.Message);
                return null;
            }
        }

        public void SaveActiveWindowState(ActiveWorkspaceState state)
        {
            if (state == null) return;
            _state.SetValue(SectionName, ActiveWindowKey, JsonSerializer.Serialize(state));
            _state.Save();
        }

        public string? GetStartupLayoutName()
        {
            string? name = _state.GetValue(SectionName, StartupProfileKey);
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }

        public void SetStartupLayoutName(string? profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                _state.RemoveValue(SectionName, StartupProfileKey);
            }
            else
            {
                _state.SetValue(SectionName, StartupProfileKey, profileName.Trim());
            }

            _state.Save();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool GetDefaultRememberWindowPosition()
        {
            return string.Equals(_state.GetValue(SectionName, DefaultRememberPositionKey), "1", StringComparison.Ordinal);
        }

        public IReadOnlyList<WorkspaceLayoutProfile> GetProfiles()
        {
            string? json = _state.GetValue(SectionName, ProfilesKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<WorkspaceLayoutProfile>();
            }

            try
            {
                var list = JsonSerializer.Deserialize<List<WorkspaceLayoutProfile>>(json) ?? new List<WorkspaceLayoutProfile>();
                return list
                    .Where(p => !IsBuiltInDefault(p.Name))
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to deserialize workspace layout profiles: {Message}", ex.Message);
                return Array.Empty<WorkspaceLayoutProfile>();
            }
        }

        public WorkspaceLayoutProfile? GetProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || IsBuiltInDefault(name)) return null;
            return GetProfiles().FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public void SaveProfile(
            string name,
            string layoutXml,
            double? windowLeft = null,
            double? windowTop = null,
            double? windowWidth = null,
            double? windowHeight = null,
            string? windowState = null,
            bool? rememberWindowPosition = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Layout profile name is required.", nameof(name));
            if (IsBuiltInDefault(name))
                throw new InvalidOperationException($"The built-in '{BuiltInDefaultName}' layout cannot be overwritten.");
            if (string.IsNullOrWhiteSpace(layoutXml))
                throw new ArgumentException("Layout XML is required.", nameof(layoutXml));

            string trimmedName = name.Trim();
            var list = GetProfiles().ToList();
            var existing = list.FirstOrDefault(p => p.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.LayoutXml = layoutXml;
                existing.WindowLeft = windowLeft;
                existing.WindowTop = windowTop;
                existing.WindowWidth = windowWidth;
                existing.WindowHeight = windowHeight;
                existing.WindowState = windowState;
                if (rememberWindowPosition.HasValue)
                {
                    existing.RememberWindowPosition = rememberWindowPosition.Value;
                }
                existing.SavedAtUtc = DateTime.UtcNow;
            }
            else
            {
                list.Add(new WorkspaceLayoutProfile
                {
                    Name = trimmedName,
                    LayoutXml = layoutXml,
                    WindowLeft = windowLeft,
                    WindowTop = windowTop,
                    WindowWidth = windowWidth,
                    WindowHeight = windowHeight,
                    WindowState = windowState,
                    RememberWindowPosition = rememberWindowPosition ?? false,
                    SavedAtUtc = DateTime.UtcNow
                });
            }

            PersistProfiles(list);
        }

        public void SetProfileRememberWindowPosition(string name, bool rememberWindowPosition)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (IsBuiltInDefault(name))
            {
                _state.SetValue(SectionName, DefaultRememberPositionKey, rememberWindowPosition ? "1" : "0");
                _state.Save();
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
            var list = GetProfiles().ToList();
            var target = list.FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (target == null || target.RememberWindowPosition == rememberWindowPosition) return;

            target.RememberWindowPosition = rememberWindowPosition;
            PersistProfiles(list);
        }

        public void SetProfileSuppressCloseSavePrompt(string name, bool suppress)
        {
            if (string.IsNullOrWhiteSpace(name) || IsBuiltInDefault(name)) return;
            var list = GetProfiles().ToList();
            var target = list.FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (target == null || target.SuppressCloseSavePrompt == suppress) return;

            target.SuppressCloseSavePrompt = suppress;
            PersistProfiles(list);
        }

        public (SMS_Search.Data.LayoutSearchTabPreference Tab, SMS_Search.Data.LayoutSearchModePreference Mode) GetDefaultSearchPreferences()
        {
            var tab = Enum.TryParse(_state.GetValue(SectionName, DefaultSearchTabKey), out SMS_Search.Data.LayoutSearchTabPreference t)
                ? t
                : SMS_Search.Data.LayoutSearchTabPreference.Function;
            var mode = Enum.TryParse(_state.GetValue(SectionName, DefaultSearchModeKey), out SMS_Search.Data.LayoutSearchModePreference m)
                ? m
                : SMS_Search.Data.LayoutSearchModePreference.Simple;
            return (tab, mode);
        }

        public void SetProfileSearchPreferences(
            string name,
            SMS_Search.Data.LayoutSearchTabPreference tabPreference,
            SMS_Search.Data.LayoutSearchModePreference modePreference)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (IsBuiltInDefault(name))
            {
                _state.SetValue(SectionName, DefaultSearchTabKey, tabPreference.ToString());
                _state.SetValue(SectionName, DefaultSearchModeKey, modePreference.ToString());
                _state.Save();
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            var list = GetProfiles().ToList();
            var target = list.FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (target == null) return;
            if (target.SearchTabPreference == tabPreference && target.SearchModePreference == modePreference) return;

            target.SearchTabPreference = tabPreference;
            target.SearchModePreference = modePreference;
            PersistProfiles(list);
        }

        public void RenameProfile(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Both old and new profile names are required.");
            if (IsBuiltInDefault(oldName) || IsBuiltInDefault(newName))
                throw new InvalidOperationException($"The built-in '{BuiltInDefaultName}' layout cannot be renamed or overwritten.");

            string trimmedNew = newName.Trim();
            var list = GetProfiles().ToList();
            var target = list.FirstOrDefault(p => p.Name.Equals(oldName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (target == null) return;

            if (!string.Equals(target.Name, trimmedNew, StringComparison.OrdinalIgnoreCase) &&
                list.Any(p => p.Name.Equals(trimmedNew, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A layout profile named '{trimmedNew}' already exists.");
            }

            string previousName = target.Name;
            target.Name = trimmedNew;
            PersistProfiles(list);

            if (string.Equals(GetStartupLayoutName(), previousName, StringComparison.OrdinalIgnoreCase))
            {
                SetStartupLayoutName(trimmedNew);
            }
        }

        public void DeleteProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || IsBuiltInDefault(name)) return;
            string trimmed = name.Trim();
            var list = GetProfiles().ToList();
            int removed = list.RemoveAll(p => p.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                PersistProfiles(list);
                if (string.Equals(GetStartupLayoutName(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    SetStartupLayoutName(null);
                }
            }
        }

        public string? GetProfileLayout(string name)
        {
            return GetProfile(name)?.LayoutXml;
        }

        private void PersistProfiles(List<WorkspaceLayoutProfile> profiles)
        {
            _state.SetValue(SectionName, ProfilesKey, JsonSerializer.Serialize(profiles));
            _state.Save();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
