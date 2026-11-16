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
        private readonly ILoggerService _logger;
        private readonly IQueryHistoryService _historyService; // Restore history service dependency
        private readonly IDataRepository _repository;
        private readonly IConnectionSettingsService _connectionSettingsService;

        private System.Windows.Threading.DispatcherTimer? _dateRolloverTimer;
        private DateTime _lastKnownDate;

        public event Action? RequestOpenSettings;
        public event Action? RequestOpenEncryptionUtility;
        public event Action? RequestOpenGs1Toolkit;
        public event Action<bool>? RequestToggleUnarchiveWindow;
        public event Action<bool>? RequestToggleImportTarget;

        public MainViewModel(
            IConfigService config,
            IDialogService dialogService,
            ILoggerService logger,
            IQueryHistoryService historyService, // Inject it
            IDataRepository repository,
            SearchViewModel searchViewModel,
            ResultsViewModel resultsViewModel,
            ImportViewModel importViewModel,
            IConnectionSettingsService connectionSettingsService)
        {
            _repository = repository;
            _config = config;
            _dialogService = dialogService;
            _logger = logger;
            _historyService = historyService;
            _connectionSettingsService = connectionSettingsService;
            SearchVm = searchViewModel;
            ResultsVm = resultsViewModel;
            ImportVm = importViewModel;

            ImportVm.RequestClose += OnImportRequestClose;
            ImportVm.ImportCompleted += OnImportCompletedAsync;
            ImportVm.DatabasesChanged += OnImportDatabasesChangedAsync;

            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            Title = $"SMS Search - v{version}";

            _lastKnownDate = DateTime.Today;
            GregorianDate = _lastKnownDate;

            _dateRolloverTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _dateRolloverTimer.Tick += DateRolloverTimer_Tick;
            _dateRolloverTimer.Start();

            DatabasesView = CollectionViewSource.GetDefaultView(Databases);

            LoadDatabasesCommand = new AsyncRelayCommand(() => LoadDatabasesAsync());
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
                    _logger.LogInfo("Midnight rollover detected. Updating GregorianDate from {OldDate:yyyy-MM-dd} to {NewDate:yyyy-MM-dd}.", _lastKnownDate, today);
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

            ImportVm.RequestClose -= OnImportRequestClose;
            ImportVm.ImportCompleted -= OnImportCompletedAsync;
            ImportVm.DatabasesChanged -= OnImportDatabasesChangedAsync;
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

                SearchVm.RefreshIntellisense();
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

        public async Task LoadDatabasesAsync(bool force = false)
        {
            if (!force && Databases.Count > 0) return;
            try
            {
                 var conn = _connectionSettingsService.GetConnectionSettings();

                 if (string.IsNullOrEmpty(conn.Server)) return;

                 var databases = await _repository.GetDatabasesAsync(conn.Server, conn.User, conn.Password);
                 Databases.Clear();
                 foreach(var db in databases) Databases.Add(db);
                 _logger.LogInfo("Loaded {DatabaseCount} databases from server.", Databases.Count);

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
            if (value)
            {
                _ = ImportVm.LoadDatabasesAsync(SelectedDatabase);
            }
        }

        private Task OnImportDatabasesChangedAsync()
        {
            return LoadDatabasesAsync(force: true);
        }

        private void OnImportRequestClose()
        {
            IsImportTargetVisible = false;
        }

        private async Task OnImportCompletedAsync(string fullTargetDbName, bool switchToDatabase)
        {
            await LoadDatabasesAsync(force: true);

            SearchVm.SelectedMode = SearchMode.Field;

            if (switchToDatabase)
            {
                string? matchedDb = Databases.FirstOrDefault(
                    d => d.Equals(fullTargetDbName, StringComparison.OrdinalIgnoreCase));
                string targetDb = matchedDb ?? fullTargetDbName;
                bool alreadySelected = string.Equals(SelectedDatabase, targetDb, StringComparison.OrdinalIgnoreCase);

                SelectedDatabase = targetDb;

                if (alreadySelected && SearchVm.LoadTablesCommand != null)
                {
                    SearchVm.Tables.Clear();
                    SearchVm.SelectedTable = null;
                    SearchVm.LoadTablesCommand.Execute(null);
                }
            }
        }

        private bool _isUpdatingDate;

        partial void OnJulianDateTextChanged(string value)
        {
            if (_isUpdatingDate) return;

            var dt = DateUtils.FromJulian(value);
            if (dt != null)
            {
                _logger.LogDebug("Converted Julian date '{JulianValue}' to Gregorian '{GregorianDate}'.", value, dt);
                _isUpdatingDate = true;
                GregorianDate = dt;
                _isUpdatingDate = false;
            }
            else
            {
                _logger.LogDebug("Failed to convert Julian date '{JulianValue}'.", value);
            }
        }

        partial void OnGregorianDateChanged(DateTime? value)
        {
            if (_isUpdatingDate) return;

            _isUpdatingDate = true;
            JulianDateText = DateUtils.ToJulian(value);
            _logger.LogDebug("Converted Gregorian date '{GregorianDate}' to Julian '{JulianDate}'.", value, JulianDateText);
            _isUpdatingDate = false;
        }

        public SearchViewModel SearchVm { get; }
        public ResultsViewModel ResultsVm { get; }
        public ImportViewModel ImportVm { get; }

        [RelayCommand]
        private async Task Search()
        {
             var criteria = SearchVm.GetSearchCriteria();
             _logger.LogInfo("Search initiated. Type: {SearchType}, Mode: {SearchMode}, Value: {SearchValue}, AnyMatch: {AnyMatch}", criteria.Type, criteria.Mode, criteria.Value, criteria.AnyMatch);

             // Basic validation: require input only for Custom SQL and Table record queries
             bool requiresValue = criteria.Type == SearchType.CustomSql || (criteria.Type == SearchType.Table && !criteria.ShowFields);
             if (requiresValue && string.IsNullOrWhiteSpace(criteria.Value))
             {
                 _logger.LogWarning("Search validation failed: Empty search term for {SearchType}.", criteria.Type);
                 string msg = criteria.Type == SearchType.Table ? "Please select a table." : "Please enter a SQL query.";
                 _dialogService.ShowToast(msg, "Search", ToastType.Warning);
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
            _logger.LogInfo("Unarchive window visibility toggled. New State: {IsVisible}", IsUnarchiveTargetVisible);
        }

        [RelayCommand]
        private void OpenImport()
        {
            IsImportTargetVisible = !IsImportTargetVisible;
            _logger.LogInfo("Import overlay visibility toggled. New State: {IsVisible}", IsImportTargetVisible);
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
                RequestOpenEncryptionUtility?.Invoke();
            }
            else
            {
                _logger.LogInfo("Opening Settings.");
                RequestOpenSettings?.Invoke();
            }
        }
    }
}
