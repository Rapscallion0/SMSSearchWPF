using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.Views;

namespace SMS_Search.ViewModels.Settings
{
    public partial class ConnectionSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly IDataRepository _dataRepository;
        private readonly ILoggerService _logger;
        private CancellationTokenSource? _passwordCts;

        public override string Title => "Connection";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_Connection");

        private readonly IDialogService _dialogService;

        public ConnectionSectionViewModel(ISettingsRepository repository, IDataRepository dataRepository, ILoggerService logger, IDialogService dialogService)
        {
            _repository = repository;
            _dataRepository = dataRepository;
            _logger = logger;
            _dialogService = dialogService;

            Server = _repository.GetValue("CONNECTION", "SERVER") ?? "";

            Database = new ObservableSetting<string>(repository, "CONNECTION", "DATABASE",
                repository.GetValue("CONNECTION", "DATABASE") ?? "");

            User = new ObservableSetting<string>(repository, "CONNECTION", "SQLUSER",
                repository.GetValue("CONNECTION", "SQLUSER") ?? "");

            DatabasesView = CollectionViewSource.GetDefaultView(Databases);
            ServersView = CollectionViewSource.GetDefaultView(Servers);

            LoadDatabasesCommand = new AsyncRelayCommand(LoadDatabasesAsync);
            LoadServersCommand = new AsyncRelayCommand(LoadServersAsync);

            User.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Value")
                {
                    _ = LoadDatabasesCommand.ExecuteAsync(null);
                    WeakReferenceMessenger.Default.Send(new ConnectionSettingsChangedMessage(true));
                }
            };
            Database.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Value")
                {
                    UpdateValidationState();
                }
            };

            _ = LoadDatabasesCommand.ExecuteAsync(null);
            _ = LoadServersCommand.ExecuteAsync(null);
        }

        [ObservableProperty]
        private string _server = "";

        [ObservableProperty]
        private bool _isServerSaved;

        [ObservableProperty]
        private bool _isServerInvalid;

        [ObservableProperty]
        private bool _isDatabaseInvalid;

        public ObservableSetting<string> Database { get; }
        public ObservableSetting<string> User { get; }

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private bool _isPasswordSaved;

        [ObservableProperty]
        private bool _isPasswordSaving;

        [ObservableProperty]
        private ObservableCollection<string> _databases = new ObservableCollection<string>();

        [ObservableProperty]
        private ObservableCollection<string> _servers = new ObservableCollection<string>();

        public ICollectionView DatabasesView { get; private set; }
        public ICollectionView ServersView { get; private set; }

        partial void OnServerChanged(string value)
        {
            // _repository.SaveAsync automatically sets the value as well
            _ = SaveServerAsync(value);
        }

        private async Task SaveServerAsync(string value)
        {
            await _repository.SaveAsync("CONNECTION", "SERVER", value);
            IsServerSaved = true;
            _ = Task.Delay(2000).ContinueWith(_ => IsServerSaved = false);

            _ = LoadDatabasesCommand.ExecuteAsync(null);
            WeakReferenceMessenger.Default.Send(new ConnectionSettingsChangedMessage(true));
        }

        private void UpdateValidationState()
        {
            if (string.IsNullOrWhiteSpace(Server))
            {
                IsServerInvalid = true;
            }
            // IsServerInvalid is also set to true in LoadDatabasesAsync if it fails

            if (string.IsNullOrWhiteSpace(Database.Value))
            {
                IsDatabaseInvalid = true;
            }
            else
            {
                IsDatabaseInvalid = !Databases.Contains(Database.Value);
            }
        }

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

                // Password changed, reload databases
                _ = LoadDatabasesCommand.ExecuteAsync(null);
                WeakReferenceMessenger.Default.Send(new ConnectionSettingsChangedMessage(true));

                await Task.Delay(2000, token);
                IsPasswordSaved = false;
            }
            catch (OperationCanceledException) { }
        }

        public IAsyncRelayCommand LoadDatabasesCommand { get; }
        public IAsyncRelayCommand LoadServersCommand { get; }

        public async Task LoadServersAsync()
        {
            try
            {
                var servers = await _dataRepository.GetServersAsync();
                Servers.Clear();
                foreach (var server in servers) Servers.Add(server);
                _logger.LogInfo($"Discovered {Servers.Count} SQL Servers.");
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to load SQL Servers", ex);
            }
        }

        public async Task LoadDatabasesAsync()
        {
            var server = Server ?? "";
            try
            {
                 var user = User.Value ?? "";
                 var pass = _repository.GetValue("CONNECTION", "SQLPASSWORD");
                 string? decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;

                 if (string.IsNullOrEmpty(server))
                 {
                     IsServerInvalid = true;
                     UpdateValidationState();
                     return;
                 }

                 var databases = await _dataRepository.GetDatabasesAsync(server, user, decryptedPass);
                 Databases.Clear();
                 foreach(var db in databases) Databases.Add(db);
                 _logger.LogInfo($"Loaded {Databases.Count} databases for settings view.");

                 IsServerInvalid = false;
                 UpdateValidationState();
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to load databases in settings", ex);
                Databases.Clear();
                IsServerInvalid = true;
                UpdateValidationState();

                if (!string.IsNullOrEmpty(server))
                {
                    _dialogService.ShowToast("Failed to connect to the SQL Server or retrieve databases. Please verify your credentials and server name.", "Connection Error", ToastType.Error);
                }
            }
        }

        public void FilterServers(string searchText)
        {
            if (ServersView == null) return;

            ServersView.Filter = (obj) =>
            {
                if (string.IsNullOrEmpty(searchText)) return true;
                if (obj is string str)
                {
                    return str.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return false;
            };
            ServersView.Refresh();
        }

        public void FilterDatabases(string searchText)
        {
            if (DatabasesView == null) return;

            if (DatabasesView is System.Windows.Data.ListCollectionView lcv)
            {
                lcv.CustomSort = new DatabaseSortComparer(searchText);
            }

            DatabasesView.Filter = (obj) =>
            {
                if (string.IsNullOrEmpty(searchText)) return true;
                if (obj is string str)
                {
                    return str.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return false;
            };
            DatabasesView.Refresh();
        }

        private class DatabaseSortComparer : System.Collections.IComparer
        {
            private readonly string _searchText;

            public DatabaseSortComparer(string searchText)
            {
                _searchText = searchText;
            }

            public int Compare(object? x, object? y)
            {
                string? strX = x as string;
                string? strY = y as string;

                if (strX == null && strY == null) return 0;
                if (strX == null) return -1;
                if (strY == null) return 1;

                if (string.IsNullOrEmpty(_searchText))
                {
                    return string.Compare(strX, strY, System.StringComparison.OrdinalIgnoreCase);
                }

                bool xStarts = strX.StartsWith(_searchText, System.StringComparison.OrdinalIgnoreCase);
                bool yStarts = strY.StartsWith(_searchText, System.StringComparison.OrdinalIgnoreCase);

                if (xStarts && !yStarts) return -1;
                if (!xStarts && yStarts) return 1;

                return string.Compare(strX, strY, System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             if ("Server".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Database".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("User".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Password".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Credentials".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
