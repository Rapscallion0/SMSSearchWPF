using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SMS_Search.ViewModels
{
    public partial class ResultTabViewModel : ObservableObject, IDisposable
    {
        private readonly Action<ResultTabViewModel> _closeAction;

        public ResultTabViewModel(ResultsViewModel resultsVm, Action<ResultTabViewModel> closeAction, string title = "Results")
        {
            ResultsVm = resultsVm;
            _closeAction = closeAction;
            _title = title;

            CloseCommand = new RelayCommand(() => _closeAction(this));
            TogglePinCommand = new RelayCommand(() => IsPinned = !IsPinned);
        }

        public ResultsViewModel ResultsVm { get; }

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private bool _isPinned;

        [ObservableProperty]
        private bool _canClose = true;

        public IRelayCommand CloseCommand { get; }
        public IRelayCommand TogglePinCommand { get; }

        public void Dispose()
        {
            ResultsVm.Dispose();
        }
    }
}

