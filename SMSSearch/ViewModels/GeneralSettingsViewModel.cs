using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Utils;
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

        [ObservableProperty]
        private bool _alwaysOnTop;

        [ObservableProperty]
        private bool _showInTray;

        [ObservableProperty]
        private bool _checkUpdate;

        [ObservableProperty]
        private string _copyDelimiter;

        [ObservableProperty]
        private string _customDelimiter;

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

            CopyDelimiter = _config.GetValue("GENERAL", "COPY_DELIMITER");
            if (string.IsNullOrEmpty(CopyDelimiter)) CopyDelimiter = "TAB";

            CustomDelimiter = _config.GetValue("GENERAL", "COPY_DELIMITER_CUSTOM");

            UpdateVisibility();
        }

        public void Save()
        {
            _config.SetValue("GENERAL", "ALWAYSONTOP", AlwaysOnTop ? "1" : "0");
            _config.SetValue("GENERAL", "SHOWINTRAY", ShowInTray ? "1" : "0");
            _config.SetValue("GENERAL", "CHECKUPDATE", CheckUpdate ? "1" : "0");
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
