using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SMS_Search.ViewModels.Gs1
{
    public partial class Gs1BarcodeSegmentViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _text = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isHovered;

        [ObservableProperty]
        private bool _isAnimatingUpdate;

        private Gs1ParsedAiViewModel? _associatedAi;
        public Gs1ParsedAiViewModel? AssociatedAi
        {
            get => _associatedAi;
            set
            {
                SetProperty(ref _associatedAi, value);
                OnPropertyChanged(nameof(ToolTipText));
            }
        }

        public string ToolTipText
        {
            get
            {
                if (AssociatedAi == null) return string.Empty;
                if (AssociatedAi.Ai == "└─" || string.IsNullOrEmpty(AssociatedAi.Ai))
                {
                    return AssociatedAi.Title;
                }
                return $"{AssociatedAi.Ai} - {AssociatedAi.Title}";
            }
        }

        public Action? HoverStarted { get; set; }
        public Action? HoverEnded { get; set; }

        public void StartHover()
        {
            IsHovered = true;
            HoverStarted?.Invoke();
        }
        public void EndHover()
        {
            IsHovered = false;
            HoverEnded?.Invoke();
        }

        public async void PulseAnimation()
        {
            IsAnimatingUpdate = true;
            await System.Threading.Tasks.Task.Delay(1100);
            IsAnimatingUpdate = false;
        }
    }
}
