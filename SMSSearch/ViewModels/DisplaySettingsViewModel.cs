using CommunityToolkit.Mvvm.ComponentModel;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels
{
    public partial class DisplaySettingsViewModel : ObservableObject
    {
        private readonly IConfigService _config;

        public DisplaySettingsViewModel(IConfigService config)
        {
            _config = config;
            Load();
        }

        [ObservableProperty]
        private bool _showRowNumbers;

        [ObservableProperty]
        private bool _highlightMatches;

        [ObservableProperty]
        private bool _resizeColumns;

        [ObservableProperty]
        private int _autoResizeLimit;

        [ObservableProperty]
        private bool _descriptionColumns;

        public void Load()
        {
            ShowRowNumbers = _config.GetValue("GENERAL", "SHOW_ROW_NUMBERS") == "1";
            HighlightMatches = _config.GetValue("GENERAL", "HIGHLIGHT_MATCHES") == "1";
            ResizeColumns = _config.GetValue("GENERAL", "RESIZECOLUMNS") == "1";
            DescriptionColumns = _config.GetValue("GENERAL", "DESCRIPTIONCOLUMNS") == "1";

            if (int.TryParse(_config.GetValue("GENERAL", "AUTO_RESIZE_LIMIT"), out int limit))
                AutoResizeLimit = limit;
            else
                AutoResizeLimit = 5000;
        }

        public void Save()
        {
            _config.SetValue("GENERAL", "SHOW_ROW_NUMBERS", ShowRowNumbers ? "1" : "0");
            _config.SetValue("GENERAL", "HIGHLIGHT_MATCHES", HighlightMatches ? "1" : "0");
            _config.SetValue("GENERAL", "RESIZECOLUMNS", ResizeColumns ? "1" : "0");
            _config.SetValue("GENERAL", "DESCRIPTIONCOLUMNS", DescriptionColumns ? "1" : "0");
            _config.SetValue("GENERAL", "AUTO_RESIZE_LIMIT", AutoResizeLimit.ToString());
        }
    }
}
