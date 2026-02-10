using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SMS_Search.ViewModels
{
    public partial class GeneralSettingsViewModel : ObservableObject
    {
        private readonly IConfigService _config;

        public GeneralSettingsViewModel(IConfigService config)
        {
            _config = config;
            Load();
        }

        public IEnumerable<StartupLocationMode> StartupLocationModes => Enum.GetValues<StartupLocationMode>();
        public IEnumerable<SearchMode> SearchModes => Enum.GetValues<SearchMode>();

        [ObservableProperty]
        private StartupLocationMode _mainStartupLocation = StartupLocationMode.Last;

        [ObservableProperty]
        private StartupLocationMode _unarchiveStartupLocation = StartupLocationMode.Last;

        [ObservableProperty]
        private SearchMode _defaultSearchTab = SearchMode.Function;

        [ObservableProperty]
        private bool _alwaysOnTop;

        [ObservableProperty]
        private bool _showInTray;

        [ObservableProperty]
        private bool _checkUpdate;

        [ObservableProperty]
        private string _copyDelimiter = "TAB";

        [ObservableProperty]
        private string _customDelimiter = "";

        [ObservableProperty]
        private ObservableCollection<string> _delimiters = new ObservableCollection<string>
        {
            "TAB",
            "Comma (,)",
            "Pipe (|)",
            "Semicolon (;)",
            "Custom..."
        };

        [ObservableProperty]
        private bool _isCustomDelimiterVisible;

        public void Load()
        {
            AlwaysOnTop = _config.GetValue("GENERAL", "ALWAYSONTOP") == "1";
            ShowInTray = _config.GetValue("GENERAL", "SHOWINTRAY") == "1";
            CheckUpdate = _config.GetValue("GENERAL", "CHECKUPDATE") == "1";

            if (Enum.TryParse(_config.GetValue("GENERAL", "MAIN_STARTUP_LOCATION"), out StartupLocationMode mainMode))
                MainStartupLocation = mainMode;
            else
                MainStartupLocation = StartupLocationMode.Last;

            if (Enum.TryParse(_config.GetValue("GENERAL", "UNARCHIVE_STARTUP_LOCATION"), out StartupLocationMode unarchiveMode))
                UnarchiveStartupLocation = unarchiveMode;
            else
                UnarchiveStartupLocation = StartupLocationMode.Last;

            if (Enum.TryParse(_config.GetValue("GENERAL", "DEFAULT_TAB"), out SearchMode tabMode))
                DefaultSearchTab = tabMode;
            else
                DefaultSearchTab = SearchMode.Function;

            string? copyDelim = _config.GetValue("GENERAL", "COPY_DELIMITER");
            CopyDelimiter = !string.IsNullOrEmpty(copyDelim) ? copyDelim : "TAB";

            string? customDelim = _config.GetValue("GENERAL", "COPY_DELIMITER_CUSTOM");
            CustomDelimiter = customDelim ?? "";

            UpdateVisibility();
        }

        public void Save()
        {
            _config.SetValue("GENERAL", "ALWAYSONTOP", AlwaysOnTop ? "1" : "0");
            _config.SetValue("GENERAL", "SHOWINTRAY", ShowInTray ? "1" : "0");
            _config.SetValue("GENERAL", "CHECKUPDATE", CheckUpdate ? "1" : "0");

            _config.SetValue("GENERAL", "MAIN_STARTUP_LOCATION", MainStartupLocation.ToString());
            _config.SetValue("GENERAL", "UNARCHIVE_STARTUP_LOCATION", UnarchiveStartupLocation.ToString());
            _config.SetValue("GENERAL", "DEFAULT_TAB", DefaultSearchTab.ToString());

            _config.SetValue("GENERAL", "COPY_DELIMITER", CopyDelimiter);
            _config.SetValue("GENERAL", "COPY_DELIMITER_CUSTOM", CustomDelimiter);
        }

        partial void OnCopyDelimiterChanged(string value)
        {
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            IsCustomDelimiterVisible = CopyDelimiter == "Custom...";
        }

        [RelayCommand]
        private void ResetEula()
        {
            _config.SetValue("GENERAL", "EULA", "0");
            _config.Save();
            System.Windows.MessageBox.Show("EULA has been reset. It will appear on next startup.", "Settings");
        }
    }
}
