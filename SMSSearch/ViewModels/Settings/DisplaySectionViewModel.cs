using System.Windows;
using System.Windows.Media;
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
        public override Style Icon => (Style)Application.Current.FindResource("Icon_Nav_Display");

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
