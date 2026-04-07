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

        public Gs1ParsedAiViewModel? AssociatedAi { get; set; }

        public Action? HoverStarted { get; set; }
        public Action? HoverEnded { get; set; }

        public void StartHover() => HoverStarted?.Invoke();
        public void EndHover() => HoverEnded?.Invoke();

        public async void PulseAnimation()
        {
            IsAnimatingUpdate = true;
            await System.Threading.Tasks.Task.Delay(1100);
            IsAnimatingUpdate = false;
        }
    }
}
