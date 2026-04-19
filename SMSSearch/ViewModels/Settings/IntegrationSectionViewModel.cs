using SMS_Search.Services;
using SMS_Search.Utils;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Views;

namespace SMS_Search.ViewModels.Settings
{
    public partial class IntegrationSectionViewModel : SettingsSectionViewModel, IDisposable
    {
        private readonly ISettingsRepository _repository;
        private readonly IHotkeyService _hotkeyService;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private bool _isMonitoring = true;
        private Key _currentKey = Key.None;
        private ModifierKeys _currentModifiers = ModifierKeys.None;

        public override string Title => "Integration";
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

            // Load Hotkey
            string? hotkeyStr = repository.GetValue(AppSettings.Sections.Launcher, AppSettings.Keys.Hotkey);
            StoredHotkey = new ObservableSetting<string>(
                repository, AppSettings.Sections.Launcher, AppSettings.Keys.Hotkey,
                hotkeyStr ?? "");

            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                var (k, m) = HotkeyUtils.Parse(hotkeyStr);
                _currentKey = k;
                _currentModifiers = m;
                HotkeyDisplay = HotkeyUtils.GetFriendlyString(k, m);
            }

            MonitorListenerStatus();
        }

        public ObservableSetting<bool> StartWithWindows { get; }
        public ObservableSetting<string> StoredHotkey { get; } // Only used for storage

        [ObservableProperty]
        private string _hotkeyDisplay = "";

        [ObservableProperty]
        private string _listenerStatusText = "Checking...";

        [ObservableProperty]
        private System.Windows.Media.Brush _statusColor = System.Windows.Media.Brushes.Gray;

        [ObservableProperty]
        private System.Windows.Visibility _listenerWarningVisibility = System.Windows.Visibility.Collapsed;

        public void Dispose()
        {
            _isMonitoring = false;
        }

        public void CaptureHotkey(Key key, ModifierKeys modifiers)
        {
            // Always update display for "building" effect
            HotkeyDisplay = HotkeyUtils.GetFriendlyString(key, modifiers);

            // Check if it's a "complete" combination
            bool isModifierKey =
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin ||
                key == Key.System || key == Key.None;

            if (!isModifierKey)
            {
                // Validate
                if (modifiers == ModifierKeys.None)
                {
                    // Must have modifier
                    return;
                }

                if (IsBlacklisted(key, modifiers))
                {
                    _dialogService.ShowToast("This hotkey is reserved.", "Reserved", ToastType.Warning);
                    return;
                }

                // Check availability (unless it's the same as already selected)
                if ((key != _currentKey || modifiers != _currentModifiers) && !_hotkeyService.CheckAvailability(key, modifiers))
                {
                     _dialogService.ShowToast("This hotkey is already in use.", "In Use", ToastType.Error);
                     return;
                }

                // If valid, commit
                _currentKey = key;
                _currentModifiers = modifiers;

                string val = $"{_currentModifiers}+{_currentKey}";
                StoredHotkey.Value = val; // Trigger auto-save
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
        }

        private bool IsBlacklisted(Key key, ModifierKeys modifiers)
        {
            // Common shortcuts (Ctrl+C, V, X, Z, A, S, P, F)
            if (modifiers == ModifierKeys.Control)
            {
                if (key == Key.C || key == Key.V || key == Key.X || key == Key.Z ||
                    key == Key.A || key == Key.S || key == Key.P || key == Key.F) return true;
            }
            // Alt+F4
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

        [RelayCommand]
        private async Task StartListenerProcess()
        {
            if (string.IsNullOrEmpty(StoredHotkey.Value))
            {
                _dialogService.ShowToast("Cannot start Listener: A hotkey must be configured first.", "Configuration Error", ToastType.Error);
                return;
            }

            StatusColor = System.Windows.Media.Brushes.Yellow;
            ListenerStatusText = "Starting...";
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ((App)System.Windows.Application.Current).StartListener();
                });
                _logger.LogInfo("Listener started manually.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start listener", ex);
                StatusColor = System.Windows.Media.Brushes.Red;
            }

            // Wait for polling to pick it up
            await Task.Delay(1000);
            CheckListenerStatus();
        }

        [RelayCommand]
        private void StopListenerProcess()
        {
            StatusColor = System.Windows.Media.Brushes.Yellow;
            ListenerStatusText = "Stopping...";
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ((App)System.Windows.Application.Current).StopListener();
                });
                _logger.LogInfo("Listener stopped manually.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to stop listener", ex);
            }

            CheckListenerStatus();
        }

        private async void MonitorListenerStatus()
        {
            try
            {
                while (_isMonitoring)
                {
                    CheckListenerStatus();
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MonitorListenerStatus: {ex.Message}");
            }
        }

        private void CheckListenerStatus()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                bool running = App.IsListenerRunning;
                if (running)
                {
                    ListenerStatusText = "Running";
                    StatusColor = System.Windows.Media.Brushes.Green;
                    ListenerWarningVisibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    ListenerStatusText = "Stopped";
                    StatusColor = System.Windows.Media.Brushes.Red;
                    ListenerWarningVisibility = System.Windows.Visibility.Visible;
                }
            });
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             if ("Launcher".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Hotkey".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Startup".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Listener".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Global".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
