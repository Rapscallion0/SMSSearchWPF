using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels.Settings
{
    public partial class ConnectionSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private CancellationTokenSource? _passwordCts;

        public override string Title => "Connection";
        public override Style Icon => (Style)Application.Current.FindResource("Icon_Nav_Connection");

        public ConnectionSectionViewModel(ISettingsRepository repository)
        {
            _repository = repository;

            Server = new ObservableSetting<string>(repository, "CONNECTION", "SERVER",
                repository.GetValue("CONNECTION", "SERVER") ?? "");

            Database = new ObservableSetting<string>(repository, "CONNECTION", "DATABASE",
                repository.GetValue("CONNECTION", "DATABASE") ?? "");

            User = new ObservableSetting<string>(repository, "CONNECTION", "SQLUSER",
                repository.GetValue("CONNECTION", "SQLUSER") ?? "");
        }

        public ObservableSetting<string> Server { get; }
        public ObservableSetting<string> Database { get; }
        public ObservableSetting<string> User { get; }

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private bool _isPasswordSaved;

        [ObservableProperty]
        private bool _isPasswordSaving;

        partial void OnPasswordChanged(string value)
        {
            DebounceSavePassword();
        }

        private async void DebounceSavePassword()
        {
            _passwordCts?.Cancel();
            _passwordCts = new CancellationTokenSource();
            var token = _passwordCts.Token;

            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                IsPasswordSaving = true;
                IsPasswordSaved = false;

                var encrypted = GeneralUtils.Encrypt(Password);
                await _repository.SaveAsync("CONNECTION", "SQLPASSWORD", encrypted);

                if (token.IsCancellationRequested) return;

                IsPasswordSaving = false;
                IsPasswordSaved = true;

                await Task.Delay(2000, token);
                IsPasswordSaved = false;
            }
            catch (OperationCanceledException) { }
        }
    }
}
