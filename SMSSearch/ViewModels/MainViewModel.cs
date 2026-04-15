using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Services;
using SMS_Search.Views;
using SMS_Search.Data;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IConfigService _config;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService _logger;
        private readonly IQueryHistoryService _historyService; // Restore history service dependency
        private readonly IDataRepository _repository;

        private System.Windows.Threading.DispatcherTimer? _dateRolloverTimer;
        private DateTime _lastKnownDate;

        public event Action? RequestOpenSettings;
        public event Action? RequestOpenGs1Toolkit;
        public event Action<bool>? RequestToggleUnarchiveWindow;
        public event Action<bool>? RequestToggleImportTarget;

        public MainViewModel(
            IConfigService config,
            IDialogService dialogService,
            IServiceProvider serviceProvider,
            ILoggerService logger,
            IQueryHistoryService historyService, // Inject it
            IDataRepository repository,
            SearchViewModel searchViewModel,
            ResultsViewModel resultsViewModel)
        {
            _repository = repository;
            _config = config;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _historyService = historyService;
            SearchVm = searchViewModel;
            ResultsVm = resultsViewModel;

            // ImportVm gets initialized separately since it needs MainViewModel
            // To break circular dependency, we inject it or create it.
            // Actually let's just create it.
            ImportVm = new ImportViewModel(repository, logger, dialogService, config, this);

            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            Title = $"SMS Search - V{version}";

            _lastKnownDate = DateTime.Today;
            GregorianDate = _lastKnownDate;

            _dateRolloverTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _dateRolloverTimer.Tick += DateRolloverTimer_Tick;
            _dateRolloverTimer.Start();

            DatabasesView = CollectionViewSource.GetDefaultView(Databases);

            LoadDatabasesCommand = new AsyncRelayCommand(LoadDatabasesAsync);
            RefreshDatabasesCommand = new AsyncRelayCommand(RefreshDatabasesAsync);

            // Initial load of databases
            SelectedDatabase = _config.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database);
            _ = LoadDatabasesCommand.ExecuteAsync(null);

            WeakReferenceMessenger.Default.Register<ConnectionSettingsChangedMessage>(this, (r, m) =>
            {
                _ = LoadDatabasesCommand.ExecuteAsync(null);
            });
        }

        private void DateRolloverTimer_Tick(object? sender, EventArgs e)
        {
            var today = DateTime.Today;
            if (today > _lastKnownDate)
            {
                if (GregorianDate.HasValue && GregorianDate.Value.Date == _lastKnownDate.Date)
                {
                    _logger.LogInfo($"Midnight rollover detected. Updating GregorianDate from {_lastKnownDate:yyyy-MM-dd} to {today:yyyy-MM-dd}.");
                    GregorianDate = today;
                }
                _lastKnownDate = today;
            }
        }

        public void Dispose()
        {
            if (_dateRolloverTimer != null)
            {
                _dateRolloverTimer.Stop();
                _dateRolloverTimer.Tick -= DateRolloverTimer_Tick;
                _dateRolloverTimer = null;
            }

            WeakReferenceMessenger.Default.UnregisterAll(this);
            if (SearchVm is IDisposable searchDisposable) searchDisposable.Dispose();
            if (ResultsVm is IDisposable resultsDisposable) resultsDisposable.Dispose();
        }

        public string Title { get; }

        [ObservableProperty]
        private string _julianDateText = "";

        [ObservableProperty]
        private DateTime? _gregorianDate;

        [ObservableProperty]
        private bool _isUnarchiveTargetVisible;

        [ObservableProperty]
        private bool _isImportTargetVisible;

        [ObservableProperty]
        private ObservableCollection<string> _databases = new ObservableCollection<string>();

        public ICollectionView DatabasesView { get; private set; }

        [ObservableProperty]
        private string? _selectedDatabase;

        [ObservableProperty]
        private bool _isRefreshingDatabases;

        partial void OnSelectedDatabaseChanged(string? value)
        {
            // Update the connection string database for the repository logic to use this session database
            // This does NOT save to settings, but is used by ResultsViewModel when executing a search
            if (!string.IsNullOrEmpty(value) && _config.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database) != value)
            {
                // To safely pass this to ResultsVm without modifying settings on disk,
                // we'll update it in memory via config service but not call Save.
                _config.SetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database, value);

                // Clear tables in SearchVm so it reloads for the new database
                SearchVm.Tables.Clear();
                SearchVm.SelectedTable = null;
                if (SearchVm.SelectedMode == SearchMode.Field && SearchVm.LoadTablesCommand != null)
                {
                    SearchVm.LoadTablesCommand.Execute(null);
                }
            }
        }

        public IAsyncRelayCommand LoadDatabasesCommand { get; }
        public IAsyncRelayCommand RefreshDatabasesCommand { get; }

        public async Task RefreshDatabasesAsync()
        {
            if (IsRefreshingDatabases) return;
            try
            {
                IsRefreshingDatabases = true;
                string? previousSelection = SelectedDatabase;
                Databases.Clear();
                await LoadDatabasesAsync();
                if (previousSelection != null && Databases.Contains(previousSelection))
                {
                    SelectedDatabase = previousSelection;
                }
                _dialogService.ShowToast("Databases Refreshed", "Refresh Complete", ToastType.Info);
            }
            finally
            {
                IsRefreshingDatabases = false;
            }
        }

        public async Task LoadDatabasesAsync()
        {
            if (Databases.Count > 0) return;
            try
            {
                 var server = _config.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Server) ?? "";
                 string user = "";
                 string? decryptedPass = null;

                 bool isWindowsAuth = true;
                 if (bool.TryParse(_config.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth), out bool b))
                 {
                     isWindowsAuth = b;
                 }

                 if (!isWindowsAuth)
                 {
                     user = _config.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlUser) ?? "";
                     var pass = _config.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlPassword);
                     decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;
                 }

                 if (string.IsNullOrEmpty(server)) return;

                 var databases = await _repository.GetDatabasesAsync(server, user, decryptedPass);
                 Databases.Clear();
                 foreach(var db in databases) Databases.Add(db);
                 _logger.LogInfo($"Loaded {Databases.Count} databases from server.");

                 // If SelectedDatabase isn't in the list, set it to the first one or null
                 if (!string.IsNullOrEmpty(SelectedDatabase) && !Databases.Contains(SelectedDatabase))
                 {
                     SelectedDatabase = Databases.Count > 0 ? Databases[0] : null;
                 }
                 else if (string.IsNullOrEmpty(SelectedDatabase) && Databases.Count > 0)
                 {
                     SelectedDatabase = Databases[0];
                 }
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to load databases", ex);
            }
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

        partial void OnIsUnarchiveTargetVisibleChanged(bool value)
        {
            RequestToggleUnarchiveWindow?.Invoke(value);
        }

        partial void OnIsImportTargetVisibleChanged(bool value)
        {
            RequestToggleImportTarget?.Invoke(value);
        }

        private bool _isUpdatingDate;

        partial void OnJulianDateTextChanged(string value)
        {
            if (_isUpdatingDate) return;

            var dt = DateUtils.FromJulian(value);
            if (dt != null)
            {
                _logger.LogDebug($"Converted Julian date '{value}' to Gregorian '{dt}'.");
                _isUpdatingDate = true;
                GregorianDate = dt;
                _isUpdatingDate = false;
            }
            else
            {
                _logger.LogDebug($"Failed to convert Julian date '{value}'.");
            }
        }

        partial void OnGregorianDateChanged(DateTime? value)
        {
            if (_isUpdatingDate) return;

            _isUpdatingDate = true;
            JulianDateText = DateUtils.ToJulian(value);
            _logger.LogDebug($"Converted Gregorian date '{value}' to Julian '{JulianDateText}'.");
            _isUpdatingDate = false;
        }

        public SearchViewModel SearchVm { get; }
        public ResultsViewModel ResultsVm { get; }
        public ImportViewModel ImportVm { get; }

        [RelayCommand]
        private async Task Search()
        {
             var criteria = SearchVm.GetSearchCriteria();
             _logger.LogInfo($"Search initiated. Type: {criteria.Type}, Mode: {criteria.Mode}, Value: {criteria.Value}, AnyMatch: {criteria.AnyMatch}");

             // Basic validation
             if (criteria.Type != SearchType.Table && string.IsNullOrWhiteSpace(criteria.Value))
             {
                 _logger.LogWarning("Search validation failed: Empty search term.");
                 _dialogService.ShowToast("Please enter a search term.", "Search", ToastType.Warning);
                 return;
             }

             // Add to history
             if (criteria.Type != SearchType.Table && !string.IsNullOrWhiteSpace(criteria.Value))
             {
                 _historyService.AddQuery($"{criteria.Mode}_{criteria.Type}", criteria.Value);
             }

             await ResultsVm.ExecuteSearchAsync(criteria);
             WeakReferenceMessenger.Default.Send(new SearchExecutedMessage(true));
        }

        [RelayCommand]
        private void OpenUnarchive()
        {
            IsUnarchiveTargetVisible = !IsUnarchiveTargetVisible;
            _logger.LogInfo($"Unarchive window visibility toggled. New State: {IsUnarchiveTargetVisible}");
        }

        [RelayCommand]
        private void OpenImport()
        {
            IsImportTargetVisible = !IsImportTargetVisible;
            _logger.LogInfo($"Import overlay visibility toggled. New State: {IsImportTargetVisible}");
        }

        [RelayCommand]
        private void OpenGs1Toolkit()
        {
            _logger.LogInfo("Opening GS1 Toolkit.");
            RequestOpenGs1Toolkit?.Invoke();
        }

        [RelayCommand]
        private void OpenSettings()
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                _logger.LogInfo("Opening Encryption Utility (Hidden Feature).");
                var encryptWin = new EncryptionUtilityWindow();
                encryptWin.DataContext = _serviceProvider.GetRequiredService<EncryptionUtilityViewModel>();
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    encryptWin.Owner = System.Windows.Application.Current.MainWindow;
                }
                encryptWin.ShowDialog();
            }
            else
            {
                _logger.LogInfo("Opening Settings.");
                RequestOpenSettings?.Invoke();
            }
        }
    }
}
