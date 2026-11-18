using SMS_Search.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels.Settings
{
    public partial class GeneralSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly IDialogService _dialogService;
        private readonly IThemeService _themeService;

        public override string Title => "General";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_General");

        public GeneralSectionViewModel(ISettingsRepository repository, IDialogService dialogService, IThemeService themeService)
        {
            _repository = repository;
            _dialogService = dialogService;
            _themeService = themeService;


            // Always On Top
            var alwaysOnTopStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.AlwaysOnTop);
            AlwaysOnTop = new ObservableSetting<bool>(
                repository, AppSettings.Sections.General, AppSettings.Keys.AlwaysOnTop,
                alwaysOnTopStr == "1",
                v => v ? "1" : "0");

            // Show In Tray
            var showInTrayStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.ShowInTray);
            ShowInTray = new ObservableSetting<bool>(
                repository, AppSettings.Sections.General, AppSettings.Keys.ShowInTray,
                showInTrayStr == "1",
                v => v ? "1" : "0");

            // Main Startup Location
            var mainStartStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.MainStartupLocation);
            StartupLocationMode mainStart;
            if (!Enum.TryParse(mainStartStr, out mainStart)) mainStart = StartupLocationMode.Last;
            MainStartupLocation = new ObservableSetting<StartupLocationMode>(
                repository, AppSettings.Sections.General, AppSettings.Keys.MainStartupLocation,
                mainStart,
                v => v.ToString());

            // Unarchive Startup Location
            var unarchiveStartStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.UnarchiveStartupLocation);
            StartupLocationMode unarchiveStart;
            if (!Enum.TryParse(unarchiveStartStr, out unarchiveStart)) unarchiveStart = StartupLocationMode.Last;
            UnarchiveStartupLocation = new ObservableSetting<StartupLocationMode>(
                repository, AppSettings.Sections.General, AppSettings.Keys.UnarchiveStartupLocation,
                unarchiveStart,
                v => v.ToString());

            // Copy Delimiter
            var copyDelimStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.CopyDelimiter);
            CopyDelimiter = new ObservableSetting<string>(
                repository, AppSettings.Sections.General, AppSettings.Keys.CopyDelimiter,
                !string.IsNullOrEmpty(copyDelimStr) ? copyDelimStr! : "TAB");

            CopyDelimiter.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<string>.Value))
                {
                    UpdateVisibility();
                }
            };

            // Custom Delimiter
            var customDelimStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.CopyDelimiterCustom);
            CustomDelimiter = new ObservableSetting<string>(
                repository, AppSettings.Sections.General, AppSettings.Keys.CopyDelimiterCustom,
                customDelimStr ?? "");

            // Toast Timeout
            var toastTimeoutStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.ToastTimeout);
            int toastTimeout;
            if (!int.TryParse(toastTimeoutStr, out toastTimeout)) toastTimeout = 5;
            ToastTimeout = new ObservableSetting<int>(
                repository, AppSettings.Sections.General, AppSettings.Keys.ToastTimeout,
                toastTimeout,
                v => v.ToString());

            // Theme Mode
            var themeModeStr = repository.GetValue(AppSettings.Sections.General, AppSettings.Keys.ThemeMode);
            if (!Enum.TryParse(themeModeStr, out AppThemeMode themeMode)) themeMode = AppThemeMode.System;
            ThemeMode = new ObservableSetting<AppThemeMode>(
                repository, AppSettings.Sections.General, AppSettings.Keys.ThemeMode,
                themeMode,
                v => v.ToString());
            ThemeMode.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<AppThemeMode>.Value))
                {
                    _themeService.ApplyTheme(ThemeMode.Value);
                }
            };

            UpdateVisibility();

            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            var version = exePath != null ? System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath).FileVersion : "Unknown";



        }





        public ObservableSetting<bool> AlwaysOnTop { get; }
        public ObservableSetting<bool> ShowInTray { get; }

        public ObservableSetting<StartupLocationMode> MainStartupLocation { get; }
        public ObservableSetting<StartupLocationMode> UnarchiveStartupLocation { get; }
        public ObservableSetting<string> CopyDelimiter { get; }
        public ObservableSetting<string> CustomDelimiter { get; }
        public ObservableSetting<int> ToastTimeout { get; }
        public ObservableSetting<AppThemeMode> ThemeMode { get; }

        public IEnumerable<StartupLocationMode> StartupLocationModes => Enum.GetValues<StartupLocationMode>();
        public IEnumerable<AppThemeMode> ThemeModes => Enum.GetValues<AppThemeMode>();
        [ObservableProperty]
        private bool _isCustomDelimiterVisible;

        public ObservableCollection<string> Delimiters { get; } = new ObservableCollection<string>
        {
            "TAB",
            "Comma (,)",
            "Pipe (|)",
            "Semicolon (;)",
            "Custom..."
        };

        [RelayCommand]
        private void OpenLayoutManager()
        {
            if (System.Windows.Application.Current.MainWindow is SMS_Search.MainWindow mainWindow)
            {
                mainWindow.OpenLayoutManager();
            }
        }

        private void UpdateVisibility()
        {
            IsCustomDelimiterVisible = CopyDelimiter.Value == "Custom...";
        }

        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             if ("Startup".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Location".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Window".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Tray".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Export".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Delimiter".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Toast".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Notification".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Query".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Fields".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Records".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Theme".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Dark".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Light".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Appearance".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
