using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Utils;
using System;
using System.Windows;

namespace SMS_Search.ViewModels
{
    public partial class EulaViewModel : ObservableObject
    {
        private readonly IConfigService _configService;

        public event Action RequestClose;

        public EulaViewModel(IConfigService configService)
        {
            _configService = configService;
            EulaText = "There is no warranty for the program, to the extent permitted by applicable law. Except when otherwise stated in writing, the copyright holders and/or other parties provide the program “AS IS” without warranty of any kind, either expressed or implied, including, but not limited to, the implied warranties of merchantability and fitness for a particular purpose. The entire risk as to the quality and performance of the program is with you. Should the program prove defective, you assume the cost of all necessary servicing, repair or correction.";
        }

        [ObservableProperty]
        private string _eulaText;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        private bool _isAccepted;

        [RelayCommand(CanExecute = nameof(CanAccept))]
        private void Accept()
        {
            _configService.SetValue("GENERAL", "EULA", "1");
            _configService.Save();
            RequestClose?.Invoke();
        }

        private bool CanAccept() => IsAccepted;

        [RelayCommand]
        private void Cancel()
        {
            Application.Current.Shutdown();
        }
    }
}
