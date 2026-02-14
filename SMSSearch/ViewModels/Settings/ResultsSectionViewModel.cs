using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels.Settings
{
    public partial class ResultsSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly IDialogService _dialogService;

        public override string Title => "Results";
        // Keeping Icon_Nav_Display as it represents the grid/display settings
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_Display");

        public ResultsSectionViewModel(ISettingsRepository repository, IDialogService dialogService)
        {
            _repository = repository;
            _dialogService = dialogService;

            PickHighlightColorCommand = new RelayCommand(PickHighlightColor);

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

            HighlightMatches.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    SendHighlightMessage();
                }
            };

            // Highlight Color
            string? highlightColor = repository.GetValue("GENERAL", "HIGHLIGHT_COLOR");
            if (string.IsNullOrEmpty(highlightColor)) highlightColor = "#FFFFE0"; // Light Yellow

            HighlightColor = new ObservableSetting<string>(
                repository, "GENERAL", "HIGHLIGHT_COLOR",
                highlightColor,
                v => v);

            HighlightColor.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<string>.Value))
                {
                    SendHighlightMessage();
                }
            };

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

        private void SendHighlightMessage()
        {
            WeakReferenceMessenger.Default.Send(new HighlightConfigurationChangedMessage(HighlightMatches.Value, HighlightColor.Value));
        }

        private void PickHighlightColor()
        {
            string? color = _dialogService.PickColor(HighlightColor.Value);
            if (!string.IsNullOrEmpty(color))
            {
                HighlightColor.Value = color;
            }
        }

        public IRelayCommand PickHighlightColorCommand { get; }

        public ObservableSetting<bool> ShowRowNumbers { get; }
        public ObservableSetting<bool> HighlightMatches { get; }
        public ObservableSetting<string> HighlightColor { get; }
        public ObservableSetting<bool> ResizeColumns { get; }
        public ObservableSetting<bool> DescriptionColumns { get; }
        public ObservableSetting<int> AutoResizeLimit { get; }

        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             if ("Grid".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Highlight".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Color".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Resize".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Row".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
