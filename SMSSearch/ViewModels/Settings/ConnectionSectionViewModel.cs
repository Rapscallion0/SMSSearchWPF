using System;
using System.Threading;
using System.Threading.Tasks;
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
        public override string IconData => "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm-2-13h4v2h-4v-2zm0 4h4v6h-4V11z"; // Info? No, let's use Database icon path if available.
        // Using a generic database/server icon path M12,3C7.58,3 4,4.79 4,7C4,9.21 7.58,11 12,11C16.42,11 20,9.21 20,7C20,4.79 16.42,3 12,3M4,9V12C4,14.21 7.58,16 12,16C16.42,16 20,14.21 20,12V9C20,11.21 16.42,13 12,13C7.58,13 4,11.21 4,9M4,14V17C4,19.21 7.58,21 12,21C16.42,21 20,19.21 20,17V14C20,16.21 16.42,18 12,18C7.58,18 4,16.21 4,14Z

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
            if (string.IsNullOrEmpty(value)) return;
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
