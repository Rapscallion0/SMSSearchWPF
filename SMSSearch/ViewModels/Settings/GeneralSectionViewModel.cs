using SMS_Search.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
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

        public override string Title => "General";
        public override ImageSource Icon => (ImageSource)Application.Current.FindResource("Icon_Nav_General");

        public GeneralSectionViewModel(ISettingsRepository repository, IDialogService dialogService)
        {
            _repository = repository;
            _dialogService = dialogService;

            // Always On Top
            var alwaysOnTopStr = repository.GetValue("GENERAL", "ALWAYSONTOP");
            AlwaysOnTop = new ObservableSetting<bool>(
                repository, "GENERAL", "ALWAYSONTOP",
                alwaysOnTopStr == "1",
                v => v ? "1" : "0");

            // Show In Tray
            var showInTrayStr = repository.GetValue("GENERAL", "SHOWINTRAY");
            ShowInTray = new ObservableSetting<bool>(
                repository, "GENERAL", "SHOWINTRAY",
                showInTrayStr == "1",
                v => v ? "1" : "0");

            // Check Update
            var checkUpdateStr = repository.GetValue("GENERAL", "CHECKUPDATE");
            CheckUpdate = new ObservableSetting<bool>(
                repository, "GENERAL", "CHECKUPDATE",
                checkUpdateStr == "1",
                v => v ? "1" : "0");

            // Main Startup Location
            var mainStartStr = repository.GetValue("GENERAL", "MAIN_STARTUP_LOCATION");
            StartupLocationMode mainStart;
            if (!Enum.TryParse(mainStartStr, out mainStart)) mainStart = StartupLocationMode.Last;
            MainStartupLocation = new ObservableSetting<StartupLocationMode>(
                repository, "GENERAL", "MAIN_STARTUP_LOCATION",
                mainStart,
                v => v.ToString());

            // Unarchive Startup Location
            var unarchiveStartStr = repository.GetValue("GENERAL", "UNARCHIVE_STARTUP_LOCATION");
            StartupLocationMode unarchiveStart;
            if (!Enum.TryParse(unarchiveStartStr, out unarchiveStart)) unarchiveStart = StartupLocationMode.Last;
            UnarchiveStartupLocation = new ObservableSetting<StartupLocationMode>(
                repository, "GENERAL", "UNARCHIVE_STARTUP_LOCATION",
                unarchiveStart,
                v => v.ToString());

            // Default Search Tab
            var defaultTabStr = repository.GetValue("GENERAL", "DEFAULT_TAB");
            SearchMode defaultTab;
            if (!Enum.TryParse(defaultTabStr, out defaultTab)) defaultTab = SearchMode.Function;
            DefaultSearchTab = new ObservableSetting<SearchMode>(
                repository, "GENERAL", "DEFAULT_TAB",
                defaultTab,
                v => v.ToString());

            // Copy Delimiter
            var copyDelimStr = repository.GetValue("GENERAL", "COPY_DELIMITER");
            CopyDelimiter = new ObservableSetting<string>(
                repository, "GENERAL", "COPY_DELIMITER",
                !string.IsNullOrEmpty(copyDelimStr) ? copyDelimStr! : "TAB");

            CopyDelimiter.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<string>.Value))
                {
                    UpdateVisibility();
                }
            };

            // Custom Delimiter
            var customDelimStr = repository.GetValue("GENERAL", "COPY_DELIMITER_CUSTOM");
            CustomDelimiter = new ObservableSetting<string>(
                repository, "GENERAL", "COPY_DELIMITER_CUSTOM",
                customDelimStr ?? "");

            UpdateVisibility();
        }

        public ObservableSetting<bool> AlwaysOnTop { get; }
        public ObservableSetting<bool> ShowInTray { get; }
        public ObservableSetting<bool> CheckUpdate { get; }
        public ObservableSetting<StartupLocationMode> MainStartupLocation { get; }
        public ObservableSetting<StartupLocationMode> UnarchiveStartupLocation { get; }
        public ObservableSetting<SearchMode> DefaultSearchTab { get; }
        public ObservableSetting<string> CopyDelimiter { get; }
        public ObservableSetting<string> CustomDelimiter { get; }

        public IEnumerable<StartupLocationMode> StartupLocationModes => Enum.GetValues<StartupLocationMode>();
        public IEnumerable<SearchMode> SearchModes => Enum.GetValues<SearchMode>();

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

        private void UpdateVisibility()
        {
            IsCustomDelimiterVisible = CopyDelimiter.Value == "Custom...";
        }

        [RelayCommand]
        private async Task ResetEula()
        {
            await _repository.SaveAsync("GENERAL", "EULA", "0");
            _dialogService.ShowToast("EULA has been reset. It will appear on next startup.", "Settings", ToastType.Info);
        }
    }
}
