using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.Views;

namespace SMS_Search.ViewModels
{
    public partial class LauncherSettingsViewModel : ObservableObject
    {
        private readonly IConfigService _config;
        private readonly IHotkeyService _hotkeyService;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private bool _isMonitoring = true;

        public LauncherSettingsViewModel(IConfigService config, IHotkeyService hotkeyService, ILoggerService logger, IDialogService dialogService)
        {
            _config = config;
            _hotkeyService = hotkeyService;
            _logger = logger;
            _dialogService = dialogService;
            Load();
            Task.Run(MonitorServiceStatus);
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
        }

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private string _hotkeyDisplay = "";

        [ObservableProperty]
        private SolidColorBrush _statusColor = Brushes.Red;

        [ObservableProperty]
        private string _serviceStatusText = "Stopped";

        [ObservableProperty]
        private Visibility _serviceWarningVisibility = Visibility.Collapsed;

        private Key _hotkey;
        private ModifierKeys _modifiers;

        public IRelayCommand StartServiceCommand => new RelayCommand(StartService);
        public IRelayCommand StopServiceCommand => new RelayCommand(StopService);

        private void Load()
        {
            StartWithWindows = _config.GetValue("LAUNCHER", "START_WITH_WINDOWS") == "1";

            string? hotkeyStr = _config.GetValue("LAUNCHER", "HOTKEY");
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                var (k, m) = HotkeyUtils.Parse(hotkeyStr);
                _hotkey = k;
                _modifiers = m;
                HotkeyDisplay = HotkeyUtils.GetFriendlyString(k, m);
            }
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
                if ((key != _hotkey || modifiers != _modifiers) && !_hotkeyService.CheckAvailability(key, modifiers))
                {
                     _dialogService.ShowToast("This hotkey is already in use.", "In Use", ToastType.Error);
                     return;
                }

                // If valid, commit
                _hotkey = key;
                _modifiers = modifiers;
            }
        }

        public void ResetPreview()
        {
            if (_hotkey != Key.None)
            {
                HotkeyDisplay = HotkeyUtils.GetFriendlyString(_hotkey, _modifiers);
            }
            else
            {
                HotkeyDisplay = "";
            }
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

        private async void StartService()
        {
            StatusColor = Brushes.Yellow;
            ServiceStatusText = "Starting...";
            try
            {
                string? fileName = Process.GetCurrentProcess().MainModule?.FileName;
                if (fileName != null)
                {
                    Process.Start(new ProcessStartInfo(fileName, "--listener") { UseShellExecute = true });
                    _logger.LogInfo("Service started manually.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start service", ex);
                StatusColor = Brushes.Red;
            }

            // Wait for polling to pick it up
            await Task.Delay(1000);
            CheckServiceStatus();
        }

        private void StopService()
        {
            StatusColor = Brushes.Yellow;
            ServiceStatusText = "Stopping...";
            try
            {
                IntPtr hWnd = FindWindow(null, "SMS_Search_Listener_Hidden_Window");
                if (hWnd != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    var proc = Process.GetProcessById((int)pid);
                    proc.Kill();
                    _logger.LogInfo("Service stopped manually.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to stop service", ex);
            }

            CheckServiceStatus();
        }

        private async Task MonitorServiceStatus()
        {
            while (_isMonitoring)
            {
                CheckServiceStatus();
                await Task.Delay(2000);
            }
        }

        private void CheckServiceStatus()
        {
            IntPtr hWnd = FindWindow(null, "SMS_Search_Listener_Hidden_Window");
            bool running = hWnd != IntPtr.Zero;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (running)
                {
                    ServiceStatusText = "Running";
                    StatusColor = Brushes.Green;
                    ServiceWarningVisibility = Visibility.Collapsed;
                }
                else
                {
                    ServiceStatusText = "Stopped";
                    StatusColor = Brushes.Red;
                    ServiceWarningVisibility = Visibility.Visible;
                }
            });
        }

        public void Save()
        {
            _config.SetValue("LAUNCHER", "START_WITH_WINDOWS", StartWithWindows ? "1" : "0");

            if (_hotkey != Key.None)
            {
                string hotkeyStr = $"{_modifiers}+{_hotkey}";
                _config.SetValue("LAUNCHER", "HOTKEY", hotkeyStr);
            }

            // Manage Registry Key for Startup
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key != null)
                    {
                        if (StartWithWindows)
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
    }
}
