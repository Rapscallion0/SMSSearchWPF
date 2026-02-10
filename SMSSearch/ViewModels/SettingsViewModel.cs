using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Services;
using SMS_Search.Utils;
using System.Windows.Controls;

namespace SMS_Search.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigService _config;
        private readonly IDialogService _dialogService;

        public event System.Action RequestClose;

        public SettingsViewModel(IConfigService config, IDialogService dialogService)
        {
            _config = config;
            _dialogService = dialogService;

            Server = _config.GetValue("CONNECTION", "SERVER");
            Database = _config.GetValue("CONNECTION", "DATABASE");
            User = _config.GetValue("CONNECTION", "SQLUSER");
        }

        [ObservableProperty]
        private string _server;

        [ObservableProperty]
        private string _database;

        [ObservableProperty]
        private string _user;

        [RelayCommand]
        private void Save(object parameter)
        {
            PasswordBox passwordBox = parameter as PasswordBox;

            _config.SetValue("CONNECTION", "SERVER", Server);
            _config.SetValue("CONNECTION", "DATABASE", Database);
            _config.SetValue("CONNECTION", "SQLUSER", User);

            if (passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
            {
                string encrypted = GeneralUtils.Encrypt(passwordBox.Password);
                _config.SetValue("CONNECTION", "SQLPASSWORD", encrypted);
            }

            _config.Save();
            _dialogService.ShowMessage("Settings saved. You may need to restart or reload tables.", "Settings");
            RequestClose?.Invoke();
        }
    }
}
