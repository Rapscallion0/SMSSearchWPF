using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels.Settings
{
    public partial class DisplaySectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;

        public override string Title => "Display";
        public override string IconData => "M21 3H3c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H3V5h18v14zM8 15c0-1.66 1.34-3 3-3 .35 0 .69.07 1 .18V6h5v2h-3v7.03c-.02 1.64-1.35 2.97-3 2.97-1.66 0-3-1.34-3-3z"; // Monitor/Display icon

        public DisplaySectionViewModel(ISettingsRepository repository)
        {
            _repository = repository;

            // Show Row Numbers
            ShowRowNumbers = new ObservableSetting<bool>(
                repository, "GENERAL", "SHOW_ROW_NUMBERS",
                repository.GetValue("GENERAL", "SHOW_ROW_NUMBERS") == "1",
                v => v ? "1" : "0");

            ShowRowNumbers.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    WeakReferenceMessenger.Default.Send(new RowNumberVisibilityChangedMessage(ShowRowNumbers.Value));
                }
            };

            // Highlight Matches
            HighlightMatches = new ObservableSetting<bool>(
                repository, "GENERAL", "HIGHLIGHT_MATCHES",
                repository.GetValue("GENERAL", "HIGHLIGHT_MATCHES") == "1",
                v => v ? "1" : "0");

            // Resize Columns
            ResizeColumns = new ObservableSetting<bool>(
                repository, "GENERAL", "RESIZECOLUMNS",
                repository.GetValue("GENERAL", "RESIZECOLUMNS") == "1",
                v => v ? "1" : "0");

            // Description Columns
            DescriptionColumns = new ObservableSetting<bool>(
                repository, "GENERAL", "DESCRIPTIONCOLUMNS",
                repository.GetValue("GENERAL", "DESCRIPTIONCOLUMNS") == "1",
                v => v ? "1" : "0");

            // Auto Resize Limit
            int limit = 5000;
            if (int.TryParse(repository.GetValue("GENERAL", "AUTO_RESIZE_LIMIT"), out int l))
                limit = l;

            AutoResizeLimit = new ObservableSetting<int>(
                repository, "GENERAL", "AUTO_RESIZE_LIMIT",
                limit,
                v => v.ToString());
        }

        public ObservableSetting<bool> ShowRowNumbers { get; }
        public ObservableSetting<bool> HighlightMatches { get; }
        public ObservableSetting<bool> ResizeColumns { get; }
        public ObservableSetting<bool> DescriptionColumns { get; }
        public ObservableSetting<int> AutoResizeLimit { get; }
    }
}
