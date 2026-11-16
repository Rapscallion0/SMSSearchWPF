using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using SMS_Search.Data;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class ThemeService : IThemeService
    {
        private const string LightThemeUri = "pack://application:,,,/Resources/Themes/LightTheme.xaml";
        private const string DarkThemeUri = "pack://application:,,,/Resources/Themes/DarkTheme.xaml";
        private const int DwmwaUseImmersiveDarkMode = 20;

        private readonly IConfigService _configService;
        private readonly ILoggerService _logger;

        public AppThemeMode CurrentMode { get; private set; } = AppThemeMode.System;
        public bool IsDarkTheme { get; private set; }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public ThemeService(IConfigService configService, ILoggerService logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public void Initialize()
        {
            var modeStr = _configService.GetValue(AppSettings.Sections.General, AppSettings.Keys.ThemeMode);
            if (!Enum.TryParse(modeStr, out AppThemeMode mode))
            {
                mode = AppThemeMode.System;
            }

            ApplyTheme(mode);

            // Ensure newly opened windows get matching dark/light titlebar
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    if (s is Window w)
                    {
                        ApplyWindowTitleBarTheme(w, IsDarkTheme);
                    }
                }));
        }

        public void ApplyTheme(AppThemeMode mode)
        {
            CurrentMode = mode;
            bool isDark = ResolveIsDark(mode);
            IsDarkTheme = isDark;

            var app = System.Windows.Application.Current;
            if (app != null)
            {
                var targetUri = new Uri(isDark ? DarkThemeUri : LightThemeUri, UriKind.Absolute);
                var merged = app.Resources.MergedDictionaries;

                var existingTheme = merged.FirstOrDefault(d =>
                    d.Source != null &&
                    (d.Source.OriginalString.Contains("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                     d.Source.OriginalString.Contains("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)));

                var newDict = new ResourceDictionary { Source = targetUri };
                if (existingTheme != null)
                {
                    int idx = merged.IndexOf(existingTheme);
                    merged[idx] = newDict;
                }
                else
                {
                    merged.Insert(0, newDict);
                }

                foreach (Window window in app.Windows)
                {
                    ApplyWindowTitleBarTheme(window, isDark);
                }
            }

            _logger.LogInfo("Applied theme mode {ThemeMode} (IsDark={IsDark})", mode, isDark);
            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(isDark));
        }

        public static bool ResolveIsDark(AppThemeMode mode)
        {
            return mode switch
            {
                AppThemeMode.Dark => true,
                AppThemeMode.Light => false,
                AppThemeMode.System => IsSystemDarkTheme(),
                _ => false
            };
        }

        private static bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int value)
                {
                    return value == 0;
                }
            }
            catch
            {
                // Fall back to light theme if registry cannot be read
            }

            return false;
        }

        private static void ApplyWindowTitleBarTheme(Window window, bool isDark)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int useDark = isDark ? 1 : 0;
                _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
            }
            catch
            {
                // Ignore on older Windows versions that do not support DWMWA_USE_IMMERSIVE_DARK_MODE
            }
        }
    }
}
