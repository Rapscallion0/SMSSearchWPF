using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels
{
    public partial class LauncherSettingsViewModel : ObservableObject
    {
        private readonly IConfigService _config;
        private readonly IHotkeyService _hotkeyService;
        private readonly ILoggerService _logger;

        public LauncherSettingsViewModel(IConfigService config, IHotkeyService hotkeyService, ILoggerService logger)
        {
            _config = config;
            _hotkeyService = hotkeyService;
            _logger = logger;
            Load();
        }

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private bool _enableHotkey;

        [ObservableProperty]
        private string _hotkeyDisplay;

        private Key _hotkey;
        private ModifierKeys _modifiers;

        private void Load()
        {
            StartWithWindows = _config.GetValue("LAUNCHER", "START_WITH_WINDOWS") == "1";
            EnableHotkey = _config.GetValue("LAUNCHER", "ENABLE_HOTKEY") == "1";

            string hotkeyStr = _config.GetValue("LAUNCHER", "HOTKEY");
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                var (k, m) = HotkeyUtils.Parse(hotkeyStr);
                _hotkey = k;
                _modifiers = m;
                HotkeyDisplay = $"{m} + {k}";
            }
        }

        public void CaptureHotkey(Key key, ModifierKeys modifiers)
        {
            _hotkey = key;
            _modifiers = modifiers;
            HotkeyDisplay = $"{modifiers} + {key}";
        }

        public void Save()
        {
            _config.SetValue("LAUNCHER", "START_WITH_WINDOWS", StartWithWindows ? "1" : "0");
            _config.SetValue("LAUNCHER", "ENABLE_HOTKEY", EnableHotkey ? "1" : "0");

            if (_hotkey != Key.None)
            {
                string hotkeyStr = $"{_modifiers}+{_hotkey}";
                _config.SetValue("LAUNCHER", "HOTKEY", hotkeyStr);
            }

            // Manage Registry Key for Startup
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key != null)
                    {
                        if (StartWithWindows)
                        {
                            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                            key.SetValue("SMS_Search_Launcher", $"\"{exePath}\" --listener");
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
