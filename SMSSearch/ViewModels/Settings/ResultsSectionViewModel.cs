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
                repository, AppSettings.Sections.Results, AppSettings.Keys.ShowRowNumbers,
                repository.GetValue(AppSettings.Sections.Results, AppSettings.Keys.ShowRowNumbers) == "1",
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
                repository, AppSettings.Sections.Results, AppSettings.Keys.HighlightMatches,
                repository.GetValue(AppSettings.Sections.Results, AppSettings.Keys.HighlightMatches) == "1",
                v => v ? "1" : "0");

            HighlightMatches.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    SendHighlightMessage();
                }
            };

            // Highlight Color
            string? highlightColor = repository.GetValue(AppSettings.Sections.Results, AppSettings.Keys.HighlightColor);
            if (string.IsNullOrEmpty(highlightColor)) highlightColor = "#FFFFE0"; // Light Yellow

            HighlightColor = new ObservableSetting<string>(
                repository, AppSettings.Sections.Results, AppSettings.Keys.HighlightColor,
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
                repository, AppSettings.Sections.Results, AppSettings.Keys.ResizeColumns,
                repository.GetValue(AppSettings.Sections.Results, AppSettings.Keys.ResizeColumns) == "1",
                v => v ? "1" : "0");

            // Description Columns
            DescriptionColumns = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Results, AppSettings.Keys.DescriptionColumns,
                repository.GetValue(AppSettings.Sections.Results, AppSettings.Keys.DescriptionColumns) == "1",
                v => v ? "1" : "0");

            // Auto Resize Limit
            int limit = 5000;
            if (int.TryParse(repository.GetValue(AppSettings.Sections.Results, AppSettings.Keys.AutoResizeLimit), out int l))
                limit = l;

            AutoResizeLimit = new ObservableSetting<int>(
                repository, AppSettings.Sections.Results, AppSettings.Keys.AutoResizeLimit,
                limit,
                v => v.ToString());

            // Horizontal Scroll Speed
            int scrollSpeed = 16;
            if (int.TryParse(repository.GetValue(AppSettings.Sections.Results, AppSettings.Keys.HorizontalScrollSpeed), out int s))
                scrollSpeed = s;

            HorizontalScrollSpeed = new ObservableSetting<int>(
                repository, AppSettings.Sections.Results, AppSettings.Keys.HorizontalScrollSpeed,
                scrollSpeed,
                v => v.ToString());

            HorizontalScrollSpeed.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<int>.Value))
                {
                    WeakReferenceMessenger.Default.Send(new HorizontalScrollSpeedChangedMessage(HorizontalScrollSpeed.Value));
                }
            };
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
        public ObservableSetting<int> HorizontalScrollSpeed { get; }

        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             if ("Grid".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Highlight".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Color".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Resize".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Row".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Scroll".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
