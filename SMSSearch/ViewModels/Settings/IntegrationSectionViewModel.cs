using SMS_Search.Services;
using SMS_Search.Utils;
using Microsoft.Win32;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Views;

namespace SMS_Search.ViewModels.Settings
{
    public partial class IntegrationSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly IHotkeyService _hotkeyService;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private Key _currentKey = Key.None;
        private ModifierKeys _currentModifiers = ModifierKeys.None;

        public override string Title => "Tray & Startup";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_Launcher");

        public IntegrationSectionViewModel(
            ISettingsRepository repository,
            IHotkeyService hotkeyService,
            ILoggerService logger,
            IDialogService dialogService)
        {
            _repository = repository;
            _hotkeyService = hotkeyService;
            _logger = logger;
            _dialogService = dialogService;

            // Minimize to Tray
            var showInTrayStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.ShowInTray);
            ShowInTray = new ObservableSetting<bool>(
                repository, AppSettings.Sections.General, AppSettings.Keys.ShowInTray,
                showInTrayStr == "1",
                v => v ? "1" : "0");

            // Close to Tray (falls back to legacy LISTENER_ENABLED if unset)
            var closeToTrayStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.CloseToTray);
            bool initialCloseToTray = closeToTrayStr != null
                ? closeToTrayStr == "1"
                : repository.GetValue(AppSettings.Sections.Launcher, AppSettings.Keys.ListenerEnabled) == "1";
            CloseToTray = new ObservableSetting<bool>(
                repository, AppSettings.Sections.General, AppSettings.Keys.CloseToTray,
                initialCloseToTray,
                v => v ? "1" : "0");
            CloseToTray.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value) &&
                    System.Windows.Application.Current is App app)
                {
                    app.RefreshTrayAndHotkeyState();
                }
            };

            StartWithWindows = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Launcher, AppSettings.Keys.StartWithWindows,
                repository.GetValue(AppSettings.Sections.Launcher, AppSettings.Keys.StartWithWindows) == "1",
                v => v ? "1" : "0");

            StartWithWindows.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    UpdateRegistry(StartWithWindows.Value);
                }
            };

            // Load Hotkey (default to Ctrl + Shift + ` when unset; preserve "" when explicitly cleared)
            string? rawHotkey = repository.GetValue(AppSettings.Sections.Launcher, AppSettings.Keys.Hotkey);
            string effectiveHotkey = rawHotkey ?? AppSettings.Defaults.Hotkey;
            StoredHotkey = new ObservableSetting<string>(
                repository, AppSettings.Sections.Launcher, AppSettings.Keys.Hotkey,
                effectiveHotkey);

            if (!string.IsNullOrWhiteSpace(effectiveHotkey))
            {
                var (k, m) = HotkeyUtils.Parse(effectiveHotkey);
                _currentKey = k;
                _currentModifiers = m;
                HotkeyDisplay = HotkeyUtils.GetFriendlyString(k, m);
            }

            RefreshHotkeyStatus();
        }

        public ObservableSetting<bool> ShowInTray { get; }
        public ObservableSetting<bool> CloseToTray { get; }
        public ObservableSetting<bool> StartWithWindows { get; }
        public ObservableSetting<string> StoredHotkey { get; }

        [ObservableProperty]
        private string _hotkeyDisplay = "";

        [ObservableProperty]
        private string _hotkeyStatusText = "Not configured";

        [ObservableProperty]
        private System.Windows.Media.Brush _statusColor = System.Windows.Media.Brushes.Gray;

        public void CaptureHotkey(Key key, ModifierKeys modifiers)
        {
            // Always update display for "building" effect
            HotkeyDisplay = HotkeyUtils.GetFriendlyString(key, modifiers);

            bool isModifierKey =
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin ||
                key == Key.System || key == Key.None;

            if (!isModifierKey)
            {
                if (modifiers == ModifierKeys.None)
                {
                    return;
                }

                if (IsBlacklisted(key, modifiers))
                {
                    _dialogService.ShowToast("This hotkey is reserved.", "Reserved", ToastType.Warning);
                    ResetPreview();
                    return;
                }

                // Check availability (unless it's the same as already selected)
                if ((key != _currentKey || modifiers != _currentModifiers) && !_hotkeyService.CheckAvailability(key, modifiers))
                {
                    _dialogService.ShowToast("This hotkey is already in use.", "In Use", ToastType.Error);
                    ResetPreview();
                    return;
                }

                _currentKey = key;
                _currentModifiers = modifiers;

                string val = $"{_currentModifiers}+{_currentKey}";
                StoredHotkey.Value = val;
                ApplyAndRefreshHotkey();
            }
        }

        [RelayCommand]
        public void ResetPreview()
        {
            if (_currentKey != Key.None)
            {
                HotkeyDisplay = HotkeyUtils.GetFriendlyString(_currentKey, _currentModifiers);
            }
            else
            {
                HotkeyDisplay = "";
            }
        }

        [RelayCommand]
        public void ClearHotkey()
        {
            _currentKey = Key.None;
            _currentModifiers = ModifierKeys.None;
            HotkeyDisplay = "";
            StoredHotkey.Value = "";
            ApplyAndRefreshHotkey();
        }

        private void ApplyAndRefreshHotkey()
        {
            if (System.Windows.Application.Current is App app)
            {
                app.ApplyGlobalHotkey();
            }
            RefreshHotkeyStatus();
        }

        private void RefreshHotkeyStatus()
        {
            if (string.IsNullOrWhiteSpace(StoredHotkey.Value) || _currentKey == Key.None)
            {
                HotkeyStatusText = "Not configured";
                StatusColor = System.Windows.Media.Brushes.Gray;
                return;
            }

            if (_hotkeyService.IsRegistered)
            {
                HotkeyStatusText = $"Active ({HotkeyDisplay})";
                StatusColor = System.Windows.Media.Brushes.Green;
            }
            else
            {
                HotkeyStatusText = "Unable to register (may be in use by another app)";
                StatusColor = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private bool IsBlacklisted(Key key, ModifierKeys modifiers)
        {
            if (modifiers == ModifierKeys.Control)
            {
                if (key == Key.C || key == Key.V || key == Key.X || key == Key.Z ||
                    key == Key.A || key == Key.S || key == Key.P || key == Key.F) return true;
            }
            if (modifiers == ModifierKeys.Alt && key == Key.F4) return true;

            return false;
        }

        private void UpdateRegistry(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                            if (exePath != null)
                            {
                                key.SetValue("SMS_Search_Launcher", $"\"{exePath}\" --listener");
                            }
                        }
                        else
                        {
                            key.DeleteValue("SMS_Search_Launcher", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update startup registry key", ex);
            }
        }

        public override bool Matches(string query)
        {
            if (base.Matches(query)) return true;
            return "Global Hotkey".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   "Shortcut".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   "Startup".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   "Windows".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   "Tray".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   "Minimize".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   "Close".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   "Integration".Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   "Listener".Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
