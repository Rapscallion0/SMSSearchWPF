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
    public partial class ConnectionSectionViewModel : SettingsSectionViewModel, IDisposable
    {
        private readonly ISettingsRepository _repository;
        private readonly IDataRepository _dataRepository;
        private readonly ILoggerService _logger;
        private CancellationTokenSource? _passwordCts;
        private CancellationTokenSource? _serversCts;

        public override string Title => "Connection";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_Connection");

        private readonly IDialogService _dialogService;
        private readonly IFieldDictionaryService? _fieldDictionaryService;

        public ConnectionSectionViewModel(
            ISettingsRepository repository,
            IDataRepository dataRepository,
            ILoggerService logger,
            IDialogService dialogService,
            IFieldDictionaryService? fieldDictionaryService = null)
        {
            _repository = repository;
            _dataRepository = dataRepository;
            _logger = logger;
            _dialogService = dialogService;
            _fieldDictionaryService = fieldDictionaryService;

            WindowsAuth = new ObservableSetting<bool>(repository, AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth,
                bool.TryParse(repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth), out bool b) ? b : true);

            Server = new ObservableSetting<string>(repository, AppSettings.Sections.Connection, AppSettings.Keys.Server,
                repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Server) ?? "",
                validator: v =>
                {
                    bool isValid = !IsDbRequired || !string.IsNullOrWhiteSpace(v);
                    IsServerInvalid = !isValid;
                    return isValid;
                });

            Database = new ObservableSetting<string>(repository, AppSettings.Sections.Connection, AppSettings.Keys.Database,
                repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database) ?? "",
                validator: v =>
                {
                    bool isValid = !IsDbRequired || (!string.IsNullOrWhiteSpace(v) && Databases.Contains(v));
                    IsDatabaseInvalid = !isValid;
                    return isValid;
                });

            User = new ObservableSetting<string>(repository, AppSettings.Sections.Connection, AppSettings.Keys.SqlUser,
                repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlUser) ?? "");

            SearchNetworkServers = new ObservableSetting<bool>(repository, AppSettings.Sections.Connection, AppSettings.Keys.SearchNetworkServers,
                repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SearchNetworkServers) == "1",
                v => v ? "1" : "0");
            SearchNetworkServers.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Value")
                {
                    if (SearchNetworkServers.Value)
                    {
                        _ = LoadServersAsync();
                    }
                    else
                    {
                        _serversCts?.Cancel();
                        IsLoadingServers = false;
                    }
                }
            };

            DatabasesView = CollectionViewSource.GetDefaultView(Databases);

            LoadDatabasesCommand = new AsyncRelayCommand(LoadDatabasesAsync);
            LoadServersCommand = new AsyncRelayCommand(LoadServersAsync);

            WindowsAuth.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Value")
                {
                    _ = LoadDatabasesCommand.ExecuteAsync(null);
                    WeakReferenceMessenger.Default.Send(new ConnectionSettingsChangedMessage(true));
                }
            };
            Server.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Value")
                {
                    _ = LoadDatabasesCommand.ExecuteAsync(null);
                    WeakReferenceMessenger.Default.Send(new ConnectionSettingsChangedMessage(true));
                }
            };
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

            var dictModeStr = repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionarySourceMode);
            var initialDictMode = Enum.TryParse<DictionarySourceMode>(dictModeStr, true, out var parsedMode)
                ? parsedMode
                : Services.DictionarySourceMode.Auto;
            DictionarySourceMode = new ObservableSetting<DictionarySourceMode>(
                repository, AppSettings.Sections.Connection, AppSettings.Keys.DictionarySourceMode,
                initialDictMode, v => v.ToString());

            DictionaryPath = new ObservableSetting<string>(
                repository, AppSettings.Sections.Connection, AppSettings.Keys.DictionaryPath,
                repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryPath) ?? "");

            var dictLangStr = repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryLanguage);
            var initialDictLang = Enum.TryParse<DictionaryLanguage>(dictLangStr, true, out var parsedLang)
                ? parsedLang
                : Services.DictionaryLanguage.English;
            DictionaryLanguage = new ObservableSetting<DictionaryLanguage>(
                repository, AppSettings.Sections.Connection, AppSettings.Keys.DictionaryLanguage,
                initialDictLang, v => v.ToString());

            string? showDctVal = repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowDct);
            bool initialShowDct = showDctVal != null ? showDctVal == "1" : (_fieldDictionaryService?.GetStatus().IsCsvAvailable ?? true);
            DictionaryShowDct = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowDct,
                initialShowDct, v => v ? "1" : "0");

            DictionaryShowEn = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowEn,
                repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowEn) == "1",
                v => v ? "1" : "0");

            DictionaryShowFr = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowFr,
                repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowFr) == "1",
                v => v ? "1" : "0");

            DictionaryShowEs = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowEs,
                repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowEs) == "1",
                v => v ? "1" : "0");

            DictionarySourceMode.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Value")
                {
                    OnDictionarySettingChanged();
                }
            };
            DictionaryPath.PropertyChanged += (_, e) => { if (e.PropertyName == "Value") OnDictionarySettingChanged(); };
            DictionaryLanguage.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Value")
                {
                    OnDictionarySettingChanged();
                    OnPropertyChanged(nameof(IsEnglishExtraEnabled));
                    OnPropertyChanged(nameof(IsFrenchExtraEnabled));
                    OnPropertyChanged(nameof(IsSpanishExtraEnabled));
                    OnPropertyChanged(nameof(EnglishExtraLabel));
                    OnPropertyChanged(nameof(FrenchExtraLabel));
                    OnPropertyChanged(nameof(SpanishExtraLabel));
                    OnPropertyChanged(nameof(EffectiveShowEn));
                    OnPropertyChanged(nameof(EffectiveShowFr));
                    OnPropertyChanged(nameof(EffectiveShowEs));
                }
            };
            DictionaryShowDct.PropertyChanged += (_, e) => { if (e.PropertyName == "Value") OnDictionarySettingChanged(); };
            DictionaryShowEn.PropertyChanged += (_, e) => { if (e.PropertyName == "Value") { OnDictionarySettingChanged(); OnPropertyChanged(nameof(EffectiveShowEn)); } };
            DictionaryShowFr.PropertyChanged += (_, e) => { if (e.PropertyName == "Value") { OnDictionarySettingChanged(); OnPropertyChanged(nameof(EffectiveShowFr)); } };
            DictionaryShowEs.PropertyChanged += (_, e) => { if (e.PropertyName == "Value") { OnDictionarySettingChanged(); OnPropertyChanged(nameof(EffectiveShowEs)); } };

            if (_fieldDictionaryService != null)
            {
                _fieldDictionaryService.DictionaryChanged += OnExternalDictionaryChanged;
            }
            RefreshDictionaryStatus();
        }

        [ObservableProperty]
        private bool _isServerInvalid;

        [ObservableProperty]
        private bool _isDatabaseInvalid;

        [ObservableProperty]
        private bool _isDictionaryPathInvalid;

        [ObservableProperty]
        private bool _isDbRequired = true;

        [ObservableProperty]
        private bool _isDictionaryRequired;

        partial void OnIsDbRequiredChanged(bool value)
        {
            IsServerInvalid = value && string.IsNullOrWhiteSpace(Server.Value);
            IsDatabaseInvalid = value && (string.IsNullOrWhiteSpace(Database.Value) || !Databases.Contains(Database.Value));
        }

        partial void OnIsDictionaryRequiredChanged(bool value)
        {
            RefreshDictionaryStatus();
        }

        public ObservableSetting<bool> WindowsAuth { get; }
        public ObservableSetting<string> Server { get; }
        public ObservableSetting<string> Database { get; }
        public ObservableSetting<string> User { get; }
        public ObservableSetting<bool> SearchNetworkServers { get; }

        public ObservableSetting<DictionarySourceMode> DictionarySourceMode { get; }
        public ObservableSetting<string> DictionaryPath { get; }
        public ObservableSetting<DictionaryLanguage> DictionaryLanguage { get; }
        public ObservableSetting<bool> DictionaryShowDct { get; }
        public ObservableSetting<bool> DictionaryShowEn { get; }
        public ObservableSetting<bool> DictionaryShowFr { get; }
        public ObservableSetting<bool> DictionaryShowEs { get; }

        public System.Collections.Generic.IEnumerable<DictionarySourceMode> DictionarySourceModes => Enum.GetValues<DictionarySourceMode>();
        public System.Collections.Generic.IEnumerable<DictionaryLanguage> DictionaryLanguages => Enum.GetValues<DictionaryLanguage>();

        public bool IsEnglishExtraEnabled => DictionaryLanguage.Value != Services.DictionaryLanguage.English;
        public bool IsFrenchExtraEnabled => DictionaryLanguage.Value != Services.DictionaryLanguage.French;
        public bool IsSpanishExtraEnabled => DictionaryLanguage.Value != Services.DictionaryLanguage.Spanish;
        public string EnglishExtraLabel => IsEnglishExtraEnabled ? "Show English" : "English (Primary)";
        public string FrenchExtraLabel => IsFrenchExtraEnabled ? "Show French" : "French (Primary)";
        public string SpanishExtraLabel => IsSpanishExtraEnabled ? "Show Spanish (Fields.es)" : "Spanish (Primary)";

        public bool EffectiveShowEn
        {
            get => !IsEnglishExtraEnabled || DictionaryShowEn.Value;
            set { if (IsEnglishExtraEnabled) DictionaryShowEn.Value = value; }
        }

        public bool EffectiveShowFr
        {
            get => !IsFrenchExtraEnabled || DictionaryShowFr.Value;
            set { if (IsFrenchExtraEnabled) DictionaryShowFr.Value = value; }
        }

        public bool EffectiveShowEs
        {
            get => !IsSpanishExtraEnabled || DictionaryShowEs.Value;
            set { if (IsSpanishExtraEnabled) DictionaryShowEs.Value = value; }
        }

        [ObservableProperty]
        private string _dictionaryStatusText = string.Empty;

        [ObservableProperty]
        private bool _hasDictionaryError;

        [RelayCommand]
        private void BrowseDictionary()
        {
            string? file = _dialogService.OpenFileDialog("Fields.csv|Fields.csv|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");
            if (!string.IsNullOrEmpty(file))
            {
                HandleSelectedDictionaryFile(file);
            }
        }

        [RelayCommand]
        private void DropDictionaryFile(string? file)
        {
            if (!string.IsNullOrEmpty(file))
            {
                HandleSelectedDictionaryFile(file);
            }
        }

        public bool CanCopyDictionaryToAppFolder =>
            !string.IsNullOrWhiteSpace(DictionaryPath.Value) &&
            System.IO.File.Exists(DictionaryPath.Value) &&
            !IsInAppDirectory(DictionaryPath.Value);

        [RelayCommand(CanExecute = nameof(CanCopyDictionaryToAppFolder))]
        private void CopyDictionaryToAppFolder()
        {
            if (!string.IsNullOrWhiteSpace(DictionaryPath.Value) && System.IO.File.Exists(DictionaryPath.Value))
            {
                TryCopyToAppDirectory(DictionaryPath.Value);
            }
        }

        private void HandleSelectedDictionaryFile(string filePath)
        {
            if (IsInAppDirectory(filePath) &&
                string.Equals(System.IO.Path.GetFileName(filePath), "Fields.csv", StringComparison.OrdinalIgnoreCase))
            {
                DictionaryPath.Value = string.Empty;
                return;
            }

            DictionaryPath.Value = filePath;

            if (_dialogService.ShowConfirmation(
                "Would you like to copy Fields.csv (and any companion Fields2.csv / Fields.es files) into the application folder so it is auto-detected, or keep the original path?\n\nYes = Copy to application folder\nNo = Keep original path",
                "Copy Dictionary Files"))
            {
                TryCopyToAppDirectory(filePath);
            }
        }

        private void TryCopyToAppDirectory(string sourceFile)
        {
            try
            {
                var existing = FieldDictionaryService.GetExistingDictionaryFiles(sourceFile, AppContext.BaseDirectory);
                if (existing.Count > 0)
                {
                    string fileList = string.Join(", ", existing);
                    if (!_dialogService.ShowConfirmation(
                        $"The following dictionary file(s) already exist in the application folder:\n\n{fileList}\n\nDo you want to overwrite them?",
                        "Overwrite Existing Dictionary Files"))
                    {
                        return;
                    }
                }

                FieldDictionaryService.CopyDictionaryFilesToDirectory(sourceFile, AppContext.BaseDirectory);
                DictionaryPath.Value = string.Empty;
                _fieldDictionaryService?.InvalidateCache();
                RefreshDictionaryStatus();
                _dialogService.ShowToast("Copied dictionary files to application folder.", "Database Dictionary", ToastType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy dictionary files from {Source}", sourceFile);
                _dialogService.ShowError($"Unable to copy dictionary files to application folder: {ex.Message}\n\nThe original path will be kept.", "Copy Failed");
            }
        }

        private static bool IsInAppDirectory(string filePath)
        {
            try
            {
                string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(filePath));
                string appDir = System.IO.Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                return string.Equals(dir?.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar), appDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        [RelayCommand]
        private void ClearDictionaryPath()
        {
            DictionaryPath.Value = string.Empty;
        }

        private void OnDictionarySettingChanged()
        {
            _fieldDictionaryService?.InvalidateCache();
            RefreshDictionaryStatus();
            OnPropertyChanged(nameof(CanCopyDictionaryToAppFolder));
            CopyDictionaryToAppFolderCommand.NotifyCanExecuteChanged();
        }

        private void OnExternalDictionaryChanged(object? sender, EventArgs e)
        {
            var mode = _fieldDictionaryService?.GetSourceMode();
            if (mode.HasValue && DictionarySourceMode.Value != mode.Value)
            {
                DictionarySourceMode.Value = mode.Value;
            }
            RefreshDictionaryStatus();
        }

        private void RefreshDictionaryStatus()
        {
            if (_fieldDictionaryService == null)
            {
                DictionaryStatusText = string.Empty;
                HasDictionaryError = false;
                IsDictionaryPathInvalid = false;
                return;
            }

            var status = _fieldDictionaryService.GetStatus();
            bool requireCsv = IsDictionaryRequired || DictionarySourceMode.Value == Services.DictionarySourceMode.Csv;

            if (!string.IsNullOrEmpty(status.ErrorMessage))
            {
                DictionaryStatusText = status.ErrorMessage;
                HasDictionaryError = true;
                IsDictionaryPathInvalid = requireCsv;
            }
            else if (status.IsCsvAvailable)
            {
                string source = status.IsAutoDetected ? "Auto-detected beside executable" : status.ResolvedFieldsCsvPath ?? "";
                DictionaryStatusText = $"Loaded {status.FieldCount} fields ({source}) • Fields2.csv: {(status.HasFields2Csv ? "Yes" : "No")} • Fields.es: {(status.HasFieldsEs ? "Yes" : "No")}";
                HasDictionaryError = false;
                IsDictionaryPathInvalid = false;
            }
            else
            {
                DictionaryStatusText = "No Fields.csv found beside executable (using RB_FIELDS when connected).";
                HasDictionaryError = false;
                IsDictionaryPathInvalid = requireCsv;
            }
        }

        public ObservableCollection<string> Servers { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ServerRefreshTooltip))]
        private bool _isLoadingServers;

        public string ServerRefreshTooltip => IsLoadingServers
            ? "Searching for network servers..."
            : "Refresh available SQL Server instances";

        public IAsyncRelayCommand LoadServersCommand { get; }

        public async Task LoadServersAsync()
        {
            _serversCts?.Cancel();
            _serversCts?.Dispose();
            _serversCts = new CancellationTokenSource();
            var token = _serversCts.Token;

            // Phase 1: Instant local Registry instances
            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var local in _dataRepository.GetLocalServers())
            {
                if (!string.IsNullOrWhiteSpace(local))
                    merged.Add(local);
            }

            UpdateServersCollection(merged);

            if (!SearchNetworkServers.Value)
            {
                IsLoadingServers = false;
                return;
            }

            // Phase 2: Opt-in network broadcast scan
            IsLoadingServers = true;
            try
            {
                var list = await _dataRepository.GetServersAsync(token);
                if (token.IsCancellationRequested) return;

                foreach (var s in list)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        merged.Add(s);
                }

                UpdateServersCollection(merged);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to discover network SQL Servers: {Message}", ex.Message);
            }
            finally
            {
                IsLoadingServers = false;
            }
        }

        private void UpdateServersCollection(IEnumerable<string> servers)
        {
            string currentServer = Server.Value;
            var sorted = System.Linq.Enumerable.OrderBy(servers, s => s, StringComparer.OrdinalIgnoreCase).ToList();

            // Merge incrementally so existing items (and the active ComboBox selection) are not cleared
            foreach (var s in sorted)
            {
                if (!System.Linq.Enumerable.Any(Servers, existing => string.Equals(existing, s, StringComparison.OrdinalIgnoreCase)))
                {
                    int insertIdx = 0;
                    while (insertIdx < Servers.Count && StringComparer.OrdinalIgnoreCase.Compare(Servers[insertIdx], s) < 0)
                    {
                        insertIdx++;
                    }
                    Servers.Insert(insertIdx, s);
                }
            }

            if (!string.IsNullOrEmpty(currentServer) && !string.Equals(Server.Value, currentServer, StringComparison.Ordinal))
            {
                Server.Value = currentServer;
            }
        }

        public string GetSavedDatabase()
        {
            return _repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database) ?? "";
        }

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private bool _isPasswordSaved;

        [ObservableProperty]
        private bool _isPasswordSaving;

        [ObservableProperty]
        private ObservableCollection<string> _databases = new ObservableCollection<string>();

        public ICollectionView DatabasesView { get; private set; }

        private void UpdateValidationState()
        {
            if (string.IsNullOrWhiteSpace(Server.Value))
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
            _ = DebounceSavePasswordAsync();
        }

        private async Task DebounceSavePasswordAsync()
        {
            _passwordCts?.Cancel();
            _passwordCts?.Dispose();
            _serversCts?.Cancel();
            _serversCts?.Dispose();
            _passwordCts = new CancellationTokenSource();
            var token = _passwordCts.Token;

            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                IsPasswordSaving = true;
                IsPasswordSaved = false;

                var encrypted = GeneralUtils.Encrypt(Password);
                await _repository.SaveAsync(AppSettings.Sections.Connection, AppSettings.Keys.SqlPassword, encrypted);

                if (token.IsCancellationRequested) return;

                IsPasswordSaving = false;
                IsPasswordSaved = true;

                // Password changed, reload databases
                _ = LoadDatabasesCommand.ExecuteAsync(null);
            _ = LoadServersCommand.ExecuteAsync(null);
                WeakReferenceMessenger.Default.Send(new ConnectionSettingsChangedMessage(true));

                await Task.Delay(2000, token);
                IsPasswordSaved = false;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save SQL password", ex);
                IsPasswordSaving = false;
            }
        }

        public void Dispose()
        {
            _passwordCts?.Cancel();
            _passwordCts?.Dispose();
            _serversCts?.Cancel();
            _serversCts?.Dispose();
            _passwordCts = null;
            WindowsAuth.Dispose();
            Server.Dispose();
            Database.Dispose();
            User.Dispose();
            SearchNetworkServers.Dispose();
            DictionarySourceMode.Dispose();
            DictionaryPath.Dispose();
            DictionaryLanguage.Dispose();
            DictionaryShowDct.Dispose();
            DictionaryShowEn.Dispose();
            DictionaryShowFr.Dispose();
            DictionaryShowEs.Dispose();
            if (_fieldDictionaryService != null)
            {
                _fieldDictionaryService.DictionaryChanged -= OnExternalDictionaryChanged;
            }
        }

        public IAsyncRelayCommand LoadDatabasesCommand { get; }

        public async Task LoadDatabasesAsync()
        {
            var server = Server.Value ?? "";
            try
            {
                 var user = WindowsAuth.Value ? null : (User.Value ?? "");
                 var pass = WindowsAuth.Value ? null : _repository.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.SqlPassword);
                 string? decryptedPass = (!WindowsAuth.Value && !string.IsNullOrEmpty(pass)) ? GeneralUtils.Decrypt(pass) : null;

                 if (string.IsNullOrEmpty(server))
                 {
                     IsServerInvalid = true;
                     UpdateValidationState();
                     return;
                 }

                 string? previousSelection = Database.Value;
                 var databases = await _dataRepository.GetDatabasesAsync(server, user, decryptedPass);
                 Databases.Clear();
                 foreach(var db in databases) Databases.Add(db);
                 _logger.LogInfo($"Loaded {Databases.Count} databases for settings view.");

                 if (!string.IsNullOrEmpty(previousSelection) && Databases.Contains(previousSelection))
                 {
                     Database.Value = previousSelection;
                 }

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
             if ("Dictionary".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Fields.csv".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Fields2.csv".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Fields.es".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Language".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
