using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using SMS_Search.Views;

namespace SMS_Search.ViewModels
{
    public partial class SearchViewModel : ObservableObject, IDisposable
    {
        private readonly IDataRepository _repository;
        private readonly IDialogService _dialogService;
        private readonly IConfigService _configService;
        private readonly ILoggerService _logger;
        private readonly IQueryHistoryService _historyService;
        private readonly IClipboardService _clipboardService; // Assuming we have this service or use System.Windows.Clipboard
        private readonly IIntellisenseService _intellisenseService;
        private readonly IConnectionSettingsService _connectionSettingsService;
        private readonly ISavedQueriesService? _savedQueriesService;

        private System.Collections.Generic.List<SqlCleaningRule> _cleanSqlRules = new();
        private System.Threading.CancellationTokenSource? _intellisenseCts;

        private bool _isInitializing = true;

        public SearchViewModel(
            IDataRepository repository,
            IDialogService dialogService,
            IConfigService configService,
            ILoggerService logger,
            IQueryHistoryService historyService,
            IClipboardService clipboardService,
            IIntellisenseService intellisenseService,
            IConnectionSettingsService connectionSettingsService,
            ISavedQueriesService? savedQueriesService = null)
        {
            _repository = repository;
            _dialogService = dialogService;
            _configService = configService;
            _logger = logger;
            _historyService = historyService;
            _clipboardService = clipboardService;
            _intellisenseService = intellisenseService;
            _connectionSettingsService = connectionSettingsService;
            _savedQueriesService = savedQueriesService;

            LoadTablesCommand = new AsyncRelayCommand(LoadTablesAsync);
            RefreshTablesCommand = new AsyncRelayCommand(RefreshTablesAsync);
            CleanSqlCommand = new RelayCommand(CleanSql);
            BuildSqlCommand = new RelayCommand<string>(BuildSql);
            ShowHistoryCommand = new RelayCommand<System.Windows.Controls.Button>(ShowHistory);
            ShowSavedQueriesCommand = new RelayCommand(ShowSavedQueries);
            LoadCleanSqlRules();
            LoadFontSettings();
            LoadAnyMatchConfig();

            if (System.Enum.TryParse(_configService.GetValue(AppSettings.Sections.Search, AppSettings.Keys.DefaultTab), out DefaultSearchTabMode tabMode))
            {
                if (tabMode == DefaultSearchTabMode.Last)
                {
                    string? lastTabStr = _configService.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastTab);
                    if (string.Equals(lastTabStr, "Records", System.StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedMode = SearchMode.Field;
                        ShowRecords = true;
                    }
                    else if (System.Enum.TryParse(lastTabStr, out SearchMode lastMode))
                    {
                        SelectedMode = lastMode;
                        if (lastMode == SearchMode.Field) ShowFields = true;
                    }
                }
                else
                {
                    SelectedMode = (SearchMode)tabMode;
                }
            }

            WeakReferenceMessenger.Default.Register<SqlFontSettingsChangedMessage>(this, (r, m) =>
            {
                SqlFontFamily = m.Value.Family;
                SqlFontSize = m.Value.Size;
            });

            WeakReferenceMessenger.Default.Register<ConnectionSettingsChangedMessage>(this, (r, m) =>
            {
                _cachedSchema = null;
                AvailableRecordColumns.Clear();
                InitializeIntellisense();
            });

            InitializeIntellisense();

            TablesView = CollectionViewSource.GetDefaultView(Tables);

            _isInitializing = false;
        }

        public void Dispose()
        {
            _intellisenseCts?.Cancel();
            _intellisenseCts?.Dispose();
            _intellisenseCts = null;
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        public void RefreshIntellisense()
        {
            InitializeIntellisense();
        }

        private void InitializeIntellisense()
        {
            var conn = _connectionSettingsService.GetConnectionSettings();

            _intellisenseCts?.Cancel();
            _intellisenseCts?.Dispose();
            _intellisenseCts = new System.Threading.CancellationTokenSource();
            var token = _intellisenseCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    await _intellisenseService.InitializeAsync(conn.Server, conn.Database, conn.User, conn.Password);
                }
                catch (System.OperationCanceledException)
                {
                    // Expected on dispose
                }
                catch (System.Exception ex)
                {
                    _logger.LogError("Failed to initialize Intellisense", ex);
                }
            }, token);
        }

        private void LoadFontSettings()
        {
            string? font = _configService.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontFamily);
            SqlFontFamily = string.IsNullOrEmpty(font) ? "Consolas" : font;

            if (int.TryParse(_configService.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontSize), out int size))
            {
                SqlFontSize = size;
            }
        }

        private void LoadAnyMatchConfig()
        {
            bool defaultMatch = true;
            if (bool.TryParse(_configService.GetValue(AppSettings.Sections.Search, AppSettings.Keys.AnyMatchDefault), out bool result))
            {
                defaultMatch = result;
            }
            AnyMatch = defaultMatch;
            IsFunctionAnyMatch = defaultMatch;
            IsTotalizerAnyMatch = defaultMatch;
            IsFieldAnyMatch = defaultMatch;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        [NotifyPropertyChangedFor(nameof(IsFunctionMode))]
        [NotifyPropertyChangedFor(nameof(IsTotalizerMode))]
        [NotifyPropertyChangedFor(nameof(IsFieldMode))]
        [NotifyPropertyChangedFor(nameof(IsFieldsTabActive))]
        [NotifyPropertyChangedFor(nameof(IsRecordsTabActive))]
        [NotifyPropertyChangedFor(nameof(ActiveNumberText))]
        [NotifyPropertyChangedFor(nameof(ActiveDescriptionText))]
        [NotifyPropertyChangedFor(nameof(ActiveAnyMatch))]
        [NotifyPropertyChangedFor(nameof(IsActiveNumber))]
        [NotifyPropertyChangedFor(nameof(IsActiveDescription))]
        [NotifyPropertyChangedFor(nameof(IsActiveFieldTable))]
        [NotifyPropertyChangedFor(nameof(NumberPrefixText))]
        [NotifyPropertyChangedFor(nameof(AvailableClauseFields))]
        [NotifyPropertyChangedFor(nameof(IsQueryRecordsActive))]
        [NotifyPropertyChangedFor(nameof(IsRecordMultiClauseMode))]
        [NotifyPropertyChangedFor(nameof(IsSimpleCriteriaVisible))]
        [NotifyPropertyChangedFor(nameof(IsMultiClauseVisible))]
        private SearchMode _selectedMode;

        partial void OnSelectedModeChanged(SearchMode value)
        {
            if (!_isInitializing)
            {
                string tabKey = (value == SearchMode.Field && ShowRecords) ? "Records" : value.ToString();
                _configService.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastTab, tabKey);
            }

            if (value == SearchMode.Function && !IsFunctionNumber && !IsFunctionDescription)
            {
                IsFunctionNumber = true;
            }
            else if (value == SearchMode.Totalizer && !IsTotalizerNumber && !IsTotalizerDescription)
            {
                IsTotalizerNumber = true;
            }
            else if (value == SearchMode.Field && ShowFields)
            {
                RestoreFieldsTabCriterion();
            }

            if (value == SearchMode.Field && LoadTablesCommand != null)
            {
                LoadTablesCommand.Execute(null);
            }

            if (Clauses != null)
            {
                bool isField = value == SearchMode.Field;
                bool isRecord = IsRecordMultiClauseMode;
                foreach (var clause in Clauses)
                {
                    clause.IsFieldMode = isField;
                    clause.IsRecordQueryMode = isRecord;
                }
            }
            if (IsRecordMultiClauseMode && !string.IsNullOrWhiteSpace(SelectedTable))
            {
                _ = LoadRecordColumnsForTableAsync(SelectedTable);
            }
        }

        // Function Tab Inputs
        [ObservableProperty]
        private string _functionNumberText = "";
        [ObservableProperty]
        private string _functionDescriptionText = "";
        [ObservableProperty]
        private string _functionSqlText = "";
        [ObservableProperty]
        private string _functionSqlSelectedText = "";

        // Totalizer Tab Inputs
        [ObservableProperty]
        private string _totalizerNumberText = "";
        [ObservableProperty]
        private string _totalizerDescriptionText = "";
        [ObservableProperty]
        private string _totalizerSqlText = "";
        [ObservableProperty]
        private string _totalizerSqlSelectedText = "";

        // Field Tab Inputs
        [ObservableProperty]
        private string _fieldNumberText = "";
        [ObservableProperty]
        private string _fieldDescriptionText = "";
        [ObservableProperty]
        private string _fieldSqlText = "";
        [ObservableProperty]
        private string _fieldSqlSelectedText = "";

        [ObservableProperty]
        private string _sqlFontFamily = "Consolas";

        [ObservableProperty]
        private double _sqlFontSize = 14;

        [ObservableProperty]
        private bool _anyMatch;

        // Function Tab Properties
        [ObservableProperty]
        private bool _isFunctionNumber = true;
        [ObservableProperty]
        private bool _isFunctionAnyMatch = true;
        [ObservableProperty]
        private bool _isFunctionDescription;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private bool _isFunctionCustomSql;

        // Totalizer Tab Properties
        [ObservableProperty]
        private bool _isTotalizerNumber = true;
        [ObservableProperty]
        private bool _isTotalizerAnyMatch = true;
        [ObservableProperty]
        private bool _isTotalizerDescription;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private bool _isTotalizerCustomSql;

        // Field Tab Properties
        [ObservableProperty]
        private bool _isFieldNumber = true;
        [ObservableProperty]
        private bool _isFieldAnyMatch = true;
        [ObservableProperty]
        private bool _isFieldDescription;
        [ObservableProperty]
        private bool _isFieldTable;

        private SearchType _fieldsTabCriterion = SearchType.Number;
        private bool _isRestoringFieldsCriterion;

        partial void OnIsFieldNumberChanged(bool value)
        {
            if (value && !_isRestoringFieldsCriterion)
            {
                if (ShowFields) _fieldsTabCriterion = SearchType.Number;
                IsFieldDescription = false;
                IsFieldTable = false;
                OnPropertyChanged(nameof(IsActiveNumber));
                OnPropertyChanged(nameof(IsActiveDescription));
                OnPropertyChanged(nameof(IsActiveFieldTable));
            }
        }

        partial void OnIsFieldDescriptionChanged(bool value)
        {
            if (value && !_isRestoringFieldsCriterion)
            {
                if (ShowFields) _fieldsTabCriterion = SearchType.Description;
                IsFieldNumber = false;
                IsFieldTable = false;
                OnPropertyChanged(nameof(IsActiveNumber));
                OnPropertyChanged(nameof(IsActiveDescription));
                OnPropertyChanged(nameof(IsActiveFieldTable));
            }
        }

        partial void OnIsFieldTableChanged(bool value)
        {
            if (value && !_isRestoringFieldsCriterion)
            {
                if (ShowFields) _fieldsTabCriterion = SearchType.Table;
                IsFieldNumber = false;
                IsFieldDescription = false;
                OnPropertyChanged(nameof(IsActiveNumber));
                OnPropertyChanged(nameof(IsActiveDescription));
            }
            OnPropertyChanged(nameof(IsActiveFieldTable));
        }

        [ObservableProperty]
        private bool _isRefreshingTables;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private bool _isFieldCustomSql;

        [ObservableProperty]
        private ObservableCollection<string> _tables = new ObservableCollection<string>();

        public ICollectionView TablesView { get; private set; }

        private class TableSortComparer : System.Collections.IComparer
        {
            private readonly string _searchText;

            public TableSortComparer(string searchText)
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

                // Both start with or neither start with, sort alphabetically
                return string.Compare(strX, strY, System.StringComparison.OrdinalIgnoreCase);
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLastTransactionVisible))]
        private string? _selectedTable;

        partial void OnSelectedTableChanged(string? value)
        {
            if (IsRecordMultiClauseMode && !string.IsNullOrWhiteSpace(value))
            {
                _ = LoadRecordColumnsForTableAsync(value);
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLastTransactionVisible))]
        [NotifyPropertyChangedFor(nameof(IsQueryRecordsActive))]
        [NotifyPropertyChangedFor(nameof(IsFieldsTabActive))]
        [NotifyPropertyChangedFor(nameof(IsRecordsTabActive))]
        [NotifyPropertyChangedFor(nameof(IsRecordMultiClauseMode))]
        [NotifyPropertyChangedFor(nameof(IsSimpleCriteriaVisible))]
        [NotifyPropertyChangedFor(nameof(IsMultiClauseVisible))]
        private bool _showFields = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsQueryRecordsActive))]
        [NotifyPropertyChangedFor(nameof(IsFieldsTabActive))]
        [NotifyPropertyChangedFor(nameof(IsRecordsTabActive))]
        [NotifyPropertyChangedFor(nameof(IsRecordMultiClauseMode))]
        [NotifyPropertyChangedFor(nameof(IsSimpleCriteriaVisible))]
        [NotifyPropertyChangedFor(nameof(IsMultiClauseVisible))]
        private bool _showRecords;

        partial void OnShowRecordsChanged(bool value)
        {
            if (value)
            {
                ShowFields = false;
                _isRestoringFieldsCriterion = true;
                try
                {
                    IsFieldTable = true;
                    IsFieldNumber = false;
                    IsFieldDescription = false;
                }
                finally
                {
                    _isRestoringFieldsCriterion = false;
                }

                if (!_isInitializing)
                {
                    if (SelectedMode == SearchMode.Field)
                    {
                        _configService.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastTab, "Records");
                    }
                    if (!IsMultiClauseMode)
                    {
                        WeakReferenceMessenger.Default.Send(new FocusTableMessage(true));
                    }
                }
            }
            SyncClauseModes();
        }

        private void RestoreFieldsTabCriterion()
        {
            _isRestoringFieldsCriterion = true;
            try
            {
                IsFieldNumber = _fieldsTabCriterion == SearchType.Number;
                IsFieldDescription = _fieldsTabCriterion == SearchType.Description;
                IsFieldTable = _fieldsTabCriterion == SearchType.Table;
            }
            finally
            {
                _isRestoringFieldsCriterion = false;
            }
            OnPropertyChanged(nameof(IsActiveNumber));
            OnPropertyChanged(nameof(IsActiveDescription));
        }

        partial void OnShowFieldsChanged(bool value)
        {
            if (value)
            {
                ShowRecords = false;
                RestoreFieldsTabCriterion();

                if (!_isInitializing)
                {
                    if (SelectedMode == SearchMode.Field)
                    {
                        _configService.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastTab, SearchMode.Field.ToString());
                    }
                }
            }
            SyncClauseModes();
        }

        private void SyncClauseModes()
        {
            if (Clauses == null) return;
            bool isRecord = IsRecordMultiClauseMode;
            foreach (var clause in Clauses)
            {
                clause.IsRecordQueryMode = isRecord;
            }
            if (isRecord && !string.IsNullOrWhiteSpace(SelectedTable))
            {
                _ = LoadRecordColumnsForTableAsync(SelectedTable);
            }
        }

        [ObservableProperty]
        private bool _lastTransaction;

        partial void OnLastTransactionChanged(bool value)
        {
            if (value)
            {
                if (!_isInitializing)
                {
                    IsFieldTable = true;
                    ShowRecords = true;
                    if (IsLastTransactionVisible)
                    {
                        WeakReferenceMessenger.Default.Send(new FocusTableMessage(true));
                    }
                }
            }
        }

        public bool IsLastTransactionVisible
        {
            get
            {
                if (ShowFields) return false;
                if (string.IsNullOrEmpty(SelectedTable)) return false;
                return SelectedTable.StartsWith("SAL_", System.StringComparison.OrdinalIgnoreCase) ||
                       SelectedTable.StartsWith("REC_", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public IAsyncRelayCommand LoadTablesCommand { get; }
        public IAsyncRelayCommand RefreshTablesCommand { get; }
        public IRelayCommand CleanSqlCommand { get; }
        public IRelayCommand<string> BuildSqlCommand { get; }
        public IRelayCommand<System.Windows.Controls.Button> ShowHistoryCommand { get; }
        public IRelayCommand ShowSavedQueriesCommand { get; }

        public bool IsFunctionMode
        {
            get => SelectedMode == SearchMode.Function;
            set { if (value) SelectedMode = SearchMode.Function; }
        }

        public bool IsTotalizerMode
        {
            get => SelectedMode == SearchMode.Totalizer;
            set { if (value) SelectedMode = SearchMode.Totalizer; }
        }

        public bool IsFieldMode
        {
            get => SelectedMode == SearchMode.Field;
            set { if (value) SelectedMode = SearchMode.Field; }
        }

        public bool IsFieldsTabActive
        {
            get => SelectedMode == SearchMode.Field && ShowFields;
            set
            {
                if (value)
                {
                    ShowFields = true;
                    SelectedMode = SearchMode.Field;
                }
            }
        }

        public bool IsRecordsTabActive
        {
            get => SelectedMode == SearchMode.Field && ShowRecords;
            set
            {
                if (value)
                {
                    ShowRecords = true;
                    SelectedMode = SearchMode.Field;
                    IsFieldTable = true;
                }
            }
        }

        public string NumberPrefixText => SelectedMode == SearchMode.Field ? "F" : string.Empty;

        public string ActiveNumberText
        {
            get => SelectedMode switch
            {
                SearchMode.Function => FunctionNumberText,
                SearchMode.Totalizer => TotalizerNumberText,
                SearchMode.Field => FieldNumberText,
                _ => string.Empty
            };
            set
            {
                switch (SelectedMode)
                {
                    case SearchMode.Function:
                        if (FunctionNumberText != value) { FunctionNumberText = value; OnPropertyChanged(); }
                        break;
                    case SearchMode.Totalizer:
                        if (TotalizerNumberText != value) { TotalizerNumberText = value; OnPropertyChanged(); }
                        break;
                    case SearchMode.Field:
                        if (FieldNumberText != value) { FieldNumberText = value; OnPropertyChanged(); }
                        break;
                }
            }
        }

        public string ActiveDescriptionText
        {
            get => SelectedMode switch
            {
                SearchMode.Function => FunctionDescriptionText,
                SearchMode.Totalizer => TotalizerDescriptionText,
                SearchMode.Field => FieldDescriptionText,
                _ => string.Empty
            };
            set
            {
                switch (SelectedMode)
                {
                    case SearchMode.Function:
                        if (FunctionDescriptionText != value) { FunctionDescriptionText = value; OnPropertyChanged(); }
                        break;
                    case SearchMode.Totalizer:
                        if (TotalizerDescriptionText != value) { TotalizerDescriptionText = value; OnPropertyChanged(); }
                        break;
                    case SearchMode.Field:
                        if (FieldDescriptionText != value) { FieldDescriptionText = value; OnPropertyChanged(); }
                        break;
                }
            }
        }

        public bool ActiveAnyMatch
        {
            get => SelectedMode switch
            {
                SearchMode.Function => IsFunctionAnyMatch,
                SearchMode.Totalizer => IsTotalizerAnyMatch,
                SearchMode.Field => IsFieldAnyMatch,
                _ => true
            };
            set
            {
                switch (SelectedMode)
                {
                    case SearchMode.Function:
                        if (IsFunctionAnyMatch != value) { IsFunctionAnyMatch = value; OnPropertyChanged(); }
                        break;
                    case SearchMode.Totalizer:
                        if (IsTotalizerAnyMatch != value) { IsTotalizerAnyMatch = value; OnPropertyChanged(); }
                        break;
                    case SearchMode.Field:
                        if (IsFieldAnyMatch != value) { IsFieldAnyMatch = value; OnPropertyChanged(); }
                        break;
                }
            }
        }

        public bool IsActiveNumber
        {
            get => SelectedMode switch
            {
                SearchMode.Function => IsFunctionNumber,
                SearchMode.Totalizer => IsTotalizerNumber,
                SearchMode.Field => IsFieldNumber,
                _ => true
            };
            set
            {
                if (!value) return;
                switch (SelectedMode)
                {
                    case SearchMode.Function:
                        IsFunctionNumber = true;
                        IsFunctionDescription = false;
                        break;
                    case SearchMode.Totalizer:
                        IsTotalizerNumber = true;
                        IsTotalizerDescription = false;
                        break;
                    case SearchMode.Field:
                        IsFieldNumber = true;
                        IsFieldDescription = false;
                        IsFieldTable = false;
                        break;
                }
                OnPropertyChanged(nameof(IsActiveNumber));
                OnPropertyChanged(nameof(IsActiveDescription));
                OnPropertyChanged(nameof(IsActiveFieldTable));
            }
        }

        public bool IsActiveDescription
        {
            get => SelectedMode switch
            {
                SearchMode.Function => IsFunctionDescription,
                SearchMode.Totalizer => IsTotalizerDescription,
                SearchMode.Field => IsFieldDescription,
                _ => false
            };
            set
            {
                if (!value) return;
                switch (SelectedMode)
                {
                    case SearchMode.Function:
                        IsFunctionDescription = true;
                        IsFunctionNumber = false;
                        break;
                    case SearchMode.Totalizer:
                        IsTotalizerDescription = true;
                        IsTotalizerNumber = false;
                        break;
                    case SearchMode.Field:
                        IsFieldDescription = true;
                        IsFieldNumber = false;
                        IsFieldTable = false;
                        break;
                }
                OnPropertyChanged(nameof(IsActiveNumber));
                OnPropertyChanged(nameof(IsActiveDescription));
                OnPropertyChanged(nameof(IsActiveFieldTable));
            }
        }

        public bool IsActiveFieldTable
        {
            get => SelectedMode == SearchMode.Field && IsFieldTable;
            set
            {
                if (!value) return;
                IsFieldTable = true;
                IsFieldNumber = false;
                IsFieldDescription = false;
                OnPropertyChanged(nameof(IsActiveNumber));
                OnPropertyChanged(nameof(IsActiveDescription));
                OnPropertyChanged(nameof(IsActiveFieldTable));
            }
        }

        public bool IsQueryRecordsActive => IsFieldMode && ShowRecords;
        public bool IsRecordMultiClauseMode => IsFieldMode && ShowRecords && IsMultiClauseMode;
        public bool IsSimpleCriteriaVisible => !IsMultiClauseMode;
        public bool IsMultiClauseVisible => IsMultiClauseMode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSimpleCriteriaVisible))]
        [NotifyPropertyChangedFor(nameof(IsMultiClauseVisible))]
        [NotifyPropertyChangedFor(nameof(IsRecordMultiClauseMode))]
        private bool _isMultiClauseMode;

        partial void OnIsMultiClauseModeChanged(bool value)
        {
            if (!_isInitializing)
            {
                _configService.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastSearchMode, value ? "Advanced" : "Simple");
            }
            SyncClauseModes();
        }

        public void ApplyLayoutSearchPreferences(LayoutSearchTabPreference tabPref, LayoutSearchModePreference modePref)
        {
            if (tabPref == LayoutSearchTabPreference.Last)
            {
                string? lastTabStr = _configService.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastTab);
                if (string.Equals(lastTabStr, "Records", System.StringComparison.OrdinalIgnoreCase))
                {
                    ShowRecords = true;
                    SelectedMode = SearchMode.Field;
                    IsFieldTable = true;
                }
                else if (System.Enum.TryParse(lastTabStr, out SearchMode lastTab))
                {
                    if (lastTab == SearchMode.Field) ShowFields = true;
                    SelectedMode = lastTab;
                }
            }
            else if (tabPref == LayoutSearchTabPreference.Function)
            {
                SelectedMode = SearchMode.Function;
            }
            else if (tabPref == LayoutSearchTabPreference.Totalizer)
            {
                SelectedMode = SearchMode.Totalizer;
            }
            else if (tabPref == LayoutSearchTabPreference.Field)
            {
                ShowFields = true;
                SelectedMode = SearchMode.Field;
            }
            else if (tabPref == LayoutSearchTabPreference.Records)
            {
                ShowRecords = true;
                SelectedMode = SearchMode.Field;
                IsFieldTable = true;
                IsFieldNumber = false;
                IsFieldDescription = false;
            }

            bool? targetAdvanced = modePref switch
            {
                LayoutSearchModePreference.Simple => false,
                LayoutSearchModePreference.Advanced => true,
                LayoutSearchModePreference.Last => string.Equals(_configService.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastSearchMode), "Advanced", System.StringComparison.OrdinalIgnoreCase),
                _ => null
            };

            if (targetAdvanced.HasValue)
            {
                if (targetAdvanced.Value && !IsMultiClauseMode)
                {
                    PromoteSimpleToClauses();
                    IsMultiClauseMode = true;
                }
                else if (!targetAdvanced.Value && IsMultiClauseMode)
                {
                    IsMultiClauseMode = false;
                }
            }
        }

        public ObservableCollection<string> AvailableRecordColumns { get; } = new();
        private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? _cachedSchema;

        private async Task LoadRecordColumnsForTableAsync(string tableName)
        {
            try
            {
                if (_cachedSchema == null)
                {
                    var conn = _connectionSettingsService.GetConnectionSettings();
                    if (string.IsNullOrWhiteSpace(conn.Server) || string.IsNullOrWhiteSpace(conn.Database)) return;
                    _cachedSchema = await _repository.GetDatabaseSchemaAsync(conn.Server, conn.Database, conn.User, conn.Password);
                }

                AvailableRecordColumns.Clear();
                if (_cachedSchema != null && _cachedSchema.TryGetValue(tableName, out var cols))
                {
                    foreach (var col in cols)
                    {
                        AvailableRecordColumns.Add(col);
                    }
                }

                if (Clauses != null && AvailableRecordColumns.Count > 0)
                {
                    foreach (var row in Clauses)
                    {
                        if (string.IsNullOrWhiteSpace(row.ColumnName))
                        {
                            row.ColumnName = AvailableRecordColumns[0];
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to load columns for table {TableName}", tableName);
            }
        }

        public ObservableCollection<SearchClauseRowViewModel> Clauses { get; } = new();

        private readonly System.Collections.Generic.List<(int Start, int End)> _clauseGroups = new();

        public System.Collections.Generic.IReadOnlyList<SearchFieldTarget> AvailableClauseFields =>
            SelectedMode == SearchMode.Field
                ? new[] { SearchFieldTarget.Number, SearchFieldTarget.Description, SearchFieldTarget.Table }
                : new[] { SearchFieldTarget.Number, SearchFieldTarget.Description };

        public System.Collections.Generic.IReadOnlyList<SearchLogicalOperator> AvailableLogicalOperators { get; } =
            new[] { SearchLogicalOperator.And, SearchLogicalOperator.Or };

        public System.Collections.Generic.IReadOnlyList<SearchComparisonOperator> AvailableComparisonOperators { get; } =
            new[]
            {
                SearchComparisonOperator.Equals,
                SearchComparisonOperator.NotEquals,
                SearchComparisonOperator.Contains,
                SearchComparisonOperator.DoesNotContain,
                SearchComparisonOperator.Wildcard,
                SearchComparisonOperator.In
            };

        [RelayCommand]
        private void ToggleMultiClause()
        {
            if (!IsMultiClauseMode)
            {
                PromoteSimpleToClauses();
                IsMultiClauseMode = true;
            }
            else
            {
                IsMultiClauseMode = false;
            }
        }

        private void PromoteSimpleToClauses()
        {
            Clauses.Clear();
            _clauseGroups.Clear();

            string numVal = ActiveNumberText ?? string.Empty;
            var numOp = numVal.Contains('*') || numVal.Contains('?')
                ? SearchComparisonOperator.Wildcard
                : SearchComparisonOperator.Equals;
            Clauses.Add(CreateClauseRow(SearchFieldTarget.Number, numOp, numVal));

            string descVal = ActiveDescriptionText ?? string.Empty;
            Clauses.Add(CreateClauseRow(SearchFieldTarget.Description, SearchComparisonOperator.Contains, descVal));

            if (IsFieldMode && ShowFields && IsFieldTable && !string.IsNullOrWhiteSpace(SelectedTable))
            {
                var tblOp = SelectedTable.Contains('*') || SelectedTable.Contains('?')
                    ? SearchComparisonOperator.Wildcard
                    : SearchComparisonOperator.Equals;
                Clauses.Add(CreateClauseRow(SearchFieldTarget.Table, tblOp, SelectedTable));
            }

            RefreshClauseMetadata();
        }

        private SearchClauseRowViewModel CreateClauseRow(SearchFieldTarget field, SearchComparisonOperator op, string value)
        {
            var row = new SearchClauseRowViewModel
            {
                Field = field,
                ColumnName = AvailableRecordColumns.Count > 0 ? AvailableRecordColumns[0] : string.Empty,
                Operator = op,
                Value = value,
                IsFieldMode = SelectedMode == SearchMode.Field,
                IsRecordQueryMode = IsRecordMultiClauseMode
            };
            row.PropertyChanged += ClauseRow_PropertyChanged;
            return row;
        }

        private void ClauseRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchClauseRowViewModel.IsSelectedForGroup))
            {
                GroupSelectedClausesCommand.NotifyCanExecuteChanged();
                UngroupSelectedClausesCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private void AddClause()
        {
            var row = CreateClauseRow(SearchFieldTarget.Description, SearchComparisonOperator.Contains, string.Empty);
            Clauses.Add(row);
            RefreshClauseMetadata();
        }

        [RelayCommand]
        private void InsertClauseAfter(SearchClauseRowViewModel? target)
        {
            int idx = target != null ? Clauses.IndexOf(target) : Clauses.Count - 1;
            if (idx < 0) idx = Clauses.Count - 1;

            int insertIdx = idx + 1;
            var row = CreateClauseRow(SearchFieldTarget.Description, SearchComparisonOperator.Contains, string.Empty);
            Clauses.Insert(insertIdx, row);

            for (int i = 0; i < _clauseGroups.Count; i++)
            {
                var g = _clauseGroups[i];
                if (g.Start >= insertIdx)
                {
                    _clauseGroups[i] = (g.Start + 1, g.End + 1);
                }
                else if (g.Start <= idx && g.End >= insertIdx)
                {
                    _clauseGroups[i] = (g.Start, g.End + 1);
                }
            }

            RefreshClauseMetadata();
        }

        [RelayCommand]
        private void RemoveClause(SearchClauseRowViewModel? target)
        {
            if (target == null) return;
            int idx = Clauses.IndexOf(target);
            if (idx < 0) return;

            if (Clauses.Count <= 1)
            {
                target.Value = string.Empty;
                return;
            }

            target.PropertyChanged -= ClauseRow_PropertyChanged;
            Clauses.RemoveAt(idx);

            for (int i = _clauseGroups.Count - 1; i >= 0; i--)
            {
                var g = _clauseGroups[i];
                if (g.Start > idx)
                {
                    _clauseGroups[i] = (g.Start - 1, g.End - 1);
                }
                else if (g.Start <= idx && g.End >= idx)
                {
                    int newEnd = g.End - 1;
                    if (newEnd - g.Start < 1)
                    {
                        _clauseGroups.RemoveAt(i);
                    }
                    else
                    {
                        _clauseGroups[i] = (g.Start, newEnd);
                    }
                }
            }

            RefreshClauseMetadata();
        }

        private bool CanGroupSelectedClauses()
        {
            var selected = GetSelectedClauseIndices();
            if (selected.Count < 2) return false;

            int min = selected[0];
            int max = selected[^1];
            if (max - min + 1 != selected.Count) return false; // must be contiguous

            foreach (var g in _clauseGroups)
            {
                if (g.Start == min && g.End == max) return false; // duplicate
                bool disjoint = max < g.Start || min > g.End;
                bool nested = (min >= g.Start && max <= g.End) || (g.Start >= min && g.End <= max);
                if (!disjoint && !nested) return false; // partial overlap
            }
            return true;
        }

        [RelayCommand(CanExecute = nameof(CanGroupSelectedClauses))]
        private void GroupSelectedClauses()
        {
            var selected = GetSelectedClauseIndices();
            if (selected.Count < 2) return;

            _clauseGroups.Add((selected[0], selected[^1]));
            foreach (var row in Clauses) row.IsSelectedForGroup = false;
            RefreshClauseMetadata();
        }

        private bool CanUngroupSelectedClauses()
        {
            var selected = GetSelectedClauseIndices();
            if (selected.Count == 0) return false;

            int min = selected[0];
            int max = selected[^1];
            return System.Linq.Enumerable.Any(_clauseGroups, g => min >= g.Start && max <= g.End);
        }

        [RelayCommand(CanExecute = nameof(CanUngroupSelectedClauses))]
        private void UngroupSelectedClauses()
        {
            var selected = GetSelectedClauseIndices();
            if (selected.Count == 0) return;

            int min = selected[0];
            int max = selected[^1];

            int bestIdx = -1;
            int bestSpan = int.MaxValue;
            for (int i = 0; i < _clauseGroups.Count; i++)
            {
                var g = _clauseGroups[i];
                if (min >= g.Start && max <= g.End)
                {
                    int span = g.End - g.Start;
                    if (span < bestSpan)
                    {
                        bestSpan = span;
                        bestIdx = i;
                    }
                }
            }

            if (bestIdx >= 0)
            {
                _clauseGroups.RemoveAt(bestIdx);
                foreach (var row in Clauses) row.IsSelectedForGroup = false;
                RefreshClauseMetadata();
            }
        }

        private System.Collections.Generic.List<int> GetSelectedClauseIndices()
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Clauses.Count; i++)
            {
                if (Clauses[i].IsSelectedForGroup) list.Add(i);
            }
            return list;
        }

        [RelayCommand]
        private void MoveClauseUp(SearchClauseRowViewModel? target)
        {
            if (target == null) return;
            int idx = Clauses.IndexOf(target);
            if (idx <= 0) return;
            SwapClauses(idx, idx - 1);
        }

        [RelayCommand]
        private void MoveClauseDown(SearchClauseRowViewModel? target)
        {
            if (target == null) return;
            int idx = Clauses.IndexOf(target);
            if (idx < 0 || idx >= Clauses.Count - 1) return;
            SwapClauses(idx, idx + 1);
        }

        private void SwapClauses(int indexA, int indexB)
        {
            int low = System.Math.Min(indexA, indexB);
            int high = System.Math.Max(indexA, indexB);

            Clauses.Move(high, low);

            for (int i = _clauseGroups.Count - 1; i >= 0; i--)
            {
                var g = _clauseGroups[i];
                bool lowIn = low >= g.Start && low <= g.End;
                bool highIn = high >= g.Start && high <= g.End;

                if (lowIn == highIn) continue;

                if (highIn && g.Start == high)
                {
                    // high moved out above g.Start -> shrink start
                    int newStart = g.Start + 1;
                    if (g.End - newStart < 1) _clauseGroups.RemoveAt(i);
                    else _clauseGroups[i] = (newStart, g.End);
                }
                else if (lowIn && g.End == low)
                {
                    // low moved out below g.End -> shrink end
                    int newEnd = g.End - 1;
                    if (newEnd - g.Start < 1) _clauseGroups.RemoveAt(i);
                    else _clauseGroups[i] = (g.Start, newEnd);
                }
            }

            RefreshClauseMetadata();
        }

        private void RefreshClauseMetadata()
        {
            // Sort groups outermost-first (Start ascending, Span descending)
            var sortedGroups = System.Linq.Enumerable.ToList(
                System.Linq.Enumerable.OrderBy(_clauseGroups, g => g.Start)
                    .ThenByDescending(g => g.End - g.Start));

            for (int i = 0; i < Clauses.Count; i++)
            {
                var row = Clauses[i];
                row.IsFirstRow = (i == 0);
                row.IsLastRow = (i == Clauses.Count - 1);
                row.GroupBrackets.Clear();

                for (int d = 0; d < sortedGroups.Count; d++)
                {
                    var g = sortedGroups[d];
                    if (i < g.Start || i > g.End) continue;

                    GroupBracketPart part;
                    if (g.Start == g.End) part = GroupBracketPart.Single;
                    else if (i == g.Start) part = GroupBracketPart.Top;
                    else if (i == g.End) part = GroupBracketPart.Bottom;
                    else part = GroupBracketPart.Middle;

                    row.GroupBrackets.Add(new GroupBracketIndicator { Depth = d, Part = part });
                }
            }

            GroupSelectedClausesCommand.NotifyCanExecuteChanged();
            UngroupSelectedClausesCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void SaveAdvancedQuery()
        {
            if (_savedQueriesService == null) return;
            if (IsRecordMultiClauseMode && string.IsNullOrWhiteSpace(SelectedTable))
            {
                _dialogService.ShowWarning("Please select a table before saving this query.", "Save Advanced Query");
                return;
            }

            string sqlPreview = GenerateMultiClauseSqlPreview();
            string suggested = IsRecordMultiClauseMode
                ? $"{SelectedTable} ({Clauses.Count})"
                : $"{SelectedMode} ({Clauses.Count})";

            var categories = _savedQueriesService.GetCategories();
            if (!_dialogService.TryPromptSaveQuery(suggested, categories, out string name, out string category, "Save Advanced Query"))
            {
                return;
            }

            var existing = _savedQueriesService.FindDuplicateByNameAndCategory(name, category);
            if (existing != null && !_dialogService.ShowConfirmation(
                $"A saved query named '{existing.Name}' already exists in '{existing.Category}'.\n\nDo you want to overwrite it?",
                "Overwrite Saved Query"))
            {
                return;
            }

            var definition = ExportAdvancedQueryDefinition();
            var saved = _savedQueriesService.Save(name, category, sqlPreview, definition);
            _dialogService.ShowToast($"Saved '{saved.Name}' ({saved.Category})", "Saved Queries", ToastType.Success);
        }

        [RelayCommand]
        private void SaveSqlQuery()
        {
            if (_savedQueriesService == null) return;
            if (string.IsNullOrWhiteSpace(CustomSqlText))
            {
                _dialogService.ShowWarning("The SQL Editor is currently empty.", "Save SQL Query");
                return;
            }

            string suggested = SavedQueriesService.SuggestName(CustomSqlText);
            var categories = _savedQueriesService.GetCategories();
            if (!_dialogService.TryPromptSaveQuery(suggested, categories, out string name, out string category, "Save SQL Query"))
            {
                return;
            }

            var nameMatch = _savedQueriesService.FindDuplicateByNameAndCategory(name, category);
            if (nameMatch != null && !_dialogService.ShowConfirmation(
                $"A saved query named '{nameMatch.Name}' already exists in '{nameMatch.Category}'.\n\nDo you want to overwrite it?",
                "Overwrite Saved Query"))
            {
                return;
            }

            if (nameMatch == null)
            {
                var sqlMatches = _savedQueriesService.FindDuplicatesBySql(CustomSqlText);
                if (sqlMatches.Count > 0)
                {
                    var first = sqlMatches[0];
                    if (!_dialogService.ShowConfirmation(
                        $"This SQL is already saved as '{first.Name}' in '{first.Category}'.\n\nSave a duplicate copy anyway?",
                        "Duplicate SQL Detected"))
                    {
                        return;
                    }
                }
            }

            var saved = _savedQueriesService.Save(name, category, CustomSqlText);
            _dialogService.ShowToast($"Saved '{saved.Name}' ({saved.Category})", "Saved Queries", ToastType.Success);
        }

        public SavedAdvancedQueryDefinition ExportAdvancedQueryDefinition()
        {
            var def = new SavedAdvancedQueryDefinition
            {
                Mode = SelectedMode,
                ShowFields = ShowFields,
                SelectedTable = SelectedTable,
                LastTransaction = LastTransaction
            };

            foreach (var row in Clauses)
            {
                def.Clauses.Add(new SavedAdvancedClause
                {
                    LogicalOperator = row.LogicalOperator,
                    Field = row.Field,
                    ColumnName = row.ColumnName,
                    Operator = row.Operator,
                    Value = row.Value
                });
            }

            foreach (var g in _clauseGroups)
            {
                def.Groups.Add(new SavedClauseGroupSpan { Start = g.Start, End = g.End });
            }

            return def;
        }

        public void LoadAdvancedQueryDefinition(SavedAdvancedQueryDefinition definition)
        {
            if (definition == null) return;

            SelectedMode = definition.Mode;
            if (definition.Mode == SearchMode.Field)
            {
                if (definition.ShowFields) ShowFields = true;
                else ShowRecords = true;

                if (!string.IsNullOrWhiteSpace(definition.SelectedTable))
                {
                    SelectedTable = definition.SelectedTable;
                }
                LastTransaction = definition.LastTransaction;
            }

            foreach (var existing in Clauses)
            {
                existing.PropertyChanged -= ClauseRow_PropertyChanged;
            }
            Clauses.Clear();
            _clauseGroups.Clear();

            foreach (var c in definition.Clauses)
            {
                var row = new SearchClauseRowViewModel
                {
                    LogicalOperator = c.LogicalOperator,
                    Field = c.Field,
                    ColumnName = c.ColumnName ?? string.Empty,
                    Operator = c.Operator,
                    Value = c.Value ?? string.Empty,
                    IsFieldMode = definition.Mode == SearchMode.Field,
                    IsRecordQueryMode = definition.Mode == SearchMode.Field && !definition.ShowFields
                };
                row.PropertyChanged += ClauseRow_PropertyChanged;
                Clauses.Add(row);
            }

            if (Clauses.Count == 0)
            {
                Clauses.Add(CreateClauseRow(SearchFieldTarget.Description, SearchComparisonOperator.Contains, string.Empty));
            }

            foreach (var g in definition.Groups)
            {
                if (g.Start >= 0 && g.End < Clauses.Count && g.End > g.Start)
                {
                    _clauseGroups.Add((g.Start, g.End));
                }
            }

            IsMultiClauseMode = true;
            RefreshClauseMetadata();
        }

        private string GenerateMultiClauseSqlPreview()
        {
            var criteria = new SearchCriteria { Mode = SelectedMode, AnyMatch = AnyMatch };
            if (IsRecordMultiClauseMode)
            {
                criteria.Type = SearchType.Table;
                criteria.Value = SelectedTable;
                criteria.ShowFields = false;
                criteria.LastTransaction = IsLastTransactionVisible && LastTransaction;
            }
            else
            {
                criteria.Type = SearchType.Description;
                criteria.ShowFields = true;
            }
            criteria.RootGroup = BuildRootGroup();

            return RenderCriteriaToSql(criteria);
        }

        private string RenderCriteriaToSql(SearchCriteria criteria)
        {
            string fctFields = _configService.GetValue(AppSettings.Sections.Query, AppSettings.Keys.Function) ?? "";
            string tlzFields = _configService.GetValue(AppSettings.Sections.Query, AppSettings.Keys.Totalizer) ?? "";
            var qb = new QueryBuilder(fctFields, tlzFields);

            var result = qb.Build(criteria);
            string sql = result.Sql;

            if (result.Parameters is Dapper.DynamicParameters dp)
            {
                var names = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderByDescending(dp.ParameterNames, n => n.Length));
                foreach (var name in names)
                {
                    var val = dp.Get<object>(name);
                    string sVal = "NULL";
                    if (val != null)
                    {
                        if (val is string || val is System.DateTime)
                            sVal = $"'{val.ToString()?.Replace("'", "''")}'";
                        else
                            sVal = val.ToString() ?? "NULL";
                    }
                    sql = sql.Replace("@" + name, sVal);
                }
            }

            return BeautifySql(sql);
        }

        public SearchClauseGroup BuildRootGroup()
        {
            var sortedGroups = System.Linq.Enumerable.ToList(
                System.Linq.Enumerable.OrderBy(_clauseGroups, g => g.Start)
                    .ThenByDescending(g => g.End - g.Start));

            return BuildGroupForRange(0, Clauses.Count - 1, sortedGroups, 0);
        }

        private SearchClauseGroup BuildGroupForRange(
            int start,
            int end,
            System.Collections.Generic.List<(int Start, int End)> sortedGroups,
            int minGroupSearchIdx)
        {
            var group = new SearchClauseGroup();
            if (start < Clauses.Count)
            {
                group.LogicalOperator = Clauses[start].LogicalOperator;
            }

            int i = start;
            while (i <= end)
            {
                int matchingGroupIdx = -1;
                for (int gIdx = minGroupSearchIdx; gIdx < sortedGroups.Count; gIdx++)
                {
                    var g = sortedGroups[gIdx];
                    if (g.Start == i && g.End <= end && !(g.Start == start && g.End == end && minGroupSearchIdx > 0 && sortedGroups[minGroupSearchIdx - 1] == g))
                    {
                        matchingGroupIdx = gIdx;
                        break;
                    }
                }

                if (matchingGroupIdx >= 0)
                {
                    var g = sortedGroups[matchingGroupIdx];
                    group.Children.Add(BuildGroupForRange(g.Start, g.End, sortedGroups, matchingGroupIdx + 1));
                    i = g.End + 1;
                }
                else
                {
                    var row = Clauses[i];
                    group.Children.Add(new SearchClause
                    {
                        LogicalOperator = row.LogicalOperator,
                        Field = row.Field,
                        ColumnName = row.ColumnName,
                        Operator = row.Operator,
                        Value = row.Value
                    });
                    i++;
                }
            }

            return group;
        }

        public bool IsCustomSqlMode => (SelectedMode == SearchMode.Function && IsFunctionCustomSql) ||
                                       (SelectedMode == SearchMode.Totalizer && IsTotalizerCustomSql) ||
                                       (SelectedMode == SearchMode.Field && IsFieldCustomSql);

        private void LoadCleanSqlRules()
        {
             _cleanSqlRules.Clear();
             string? countStr = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.CleanSqlCount);
             if (int.TryParse(countStr, out int count) && count > 0)
             {
                 for (int i = 0; i < count; i++)
                 {
                     string? pattern = _configService.GetValue(AppSettings.Sections.CleanSql, "Rule_" + i + "_Regex");
                     string? replacement = _configService.GetValue(AppSettings.Sections.CleanSql, "Rule_" + i + "_Replace");
                     if (!string.IsNullOrEmpty(pattern))
                     {
                         _cleanSqlRules.Add(new SqlCleaningRule { Pattern = pattern, Replacement = replacement ?? "" });
                     }
                 }
             }
             else
             {
                 _cleanSqlRules.AddRange(SqlCleaner.DefaultRules);
             }
        }

        private void ShowSavedQueries()
        {
            WeakReferenceMessenger.Default.Send(new ShowPanelMessage("SavedQueries"));
        }

        [RelayCommand]
        private void ShowClauseHistory(System.Windows.Controls.Button? btn)
        {
            if (btn == null || btn.DataContext is not SearchClauseRowViewModel row) return;
            string key = IsRecordMultiClauseMode
                ? $"Record_{row.ColumnName}"
                : $"{SelectedMode}_{row.Field}";
            var history = _historyService.GetHistory(key);
            var menu = new System.Windows.Controls.ContextMenu();

            if (history != null)
            {
                foreach (var item in history)
                {
                    string display = item.Length > 50 ? item.Substring(0, 47) + "..." : item;
                    display = display.Replace("\r", " ").Replace("\n", " ");
                    var mi = new System.Windows.Controls.MenuItem { Header = display, ToolTip = item };
                    string fullText = item;
                    mi.Click += (s, e) => row.Value = fullText;
                    menu.Items.Add(mi);
                }

                if (menu.Items.Count > 0)
                {
                    menu.Items.Add(new System.Windows.Controls.Separator());
                    var clear = new System.Windows.Controls.MenuItem { Header = "Clear History" };
                    clear.Click += (s, e) => _historyService.ClearHistory(key);
                    menu.Items.Add(clear);
                }
                else
                {
                    menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "No history", IsEnabled = false });
                }

                menu.PlacementTarget = btn;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void ShowHistory(System.Windows.Controls.Button? btn)
        {
            if (btn == null) return;
            string? tag = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            string key = tag == "CustomSql" ? "Field_CustomSql" : $"{SelectedMode}_{tag}";
            var history = _historyService.GetHistory(key);

            var menu = new System.Windows.Controls.ContextMenu();

            if (history != null)
            {
                foreach (var item in history)
                {
                    // Truncate for display
                    string display = item.Length > 50 ? item.Substring(0, 47) + "..." : item;
                    display = display.Replace("\r", " ").Replace("\n", " ");

                    var mi = new System.Windows.Controls.MenuItem { Header = display, ToolTip = item };
                    string fullText = item;
                    mi.Click += (s, e) =>
                    {
                        if (tag == "CustomSql")
                        {
                            CustomSqlText = fullText;
                            return;
                        }

                        if (tag == "Number") { ActiveNumberText = fullText; IsActiveNumber = true; }
                        else if (tag == "Description") { ActiveDescriptionText = fullText; IsActiveDescription = true; }
                    };
                    menu.Items.Add(mi);
                }

                if (menu.Items.Count > 0)
                {
                    menu.Items.Add(new System.Windows.Controls.Separator());
                    var clear = new System.Windows.Controls.MenuItem { Header = "Clear History" };
                    clear.Click += (s, e) =>
                    {
                        _logger.LogInfo("Clearing history for key: {HistoryKey}", key);
                        _historyService.ClearHistory(key);
                    };
                    menu.Items.Add(clear);
                }
                else
                {
                    menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "No history", IsEnabled = false });
                }

                menu.PlacementTarget = btn;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        [ObservableProperty]
        private string _customSqlText = "";

        [ObservableProperty]
        private string _customSqlSelectedText = "";

        public SearchCriteria GetCustomSqlCriteria()
        {
            return new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.CustomSql,
                Value = !string.IsNullOrEmpty(CustomSqlSelectedText) ? CustomSqlSelectedText : CustomSqlText
            };
        }

        private void CleanSql()
        {
             string original = !string.IsNullOrEmpty(CustomSqlText)
                 ? CustomSqlText
                 : (SelectedMode switch
                 {
                     SearchMode.Function => FunctionSqlText,
                     SearchMode.Totalizer => TotalizerSqlText,
                     SearchMode.Field => FieldSqlText,
                     _ => ""
                 });

             if (string.IsNullOrEmpty(original)) return;

             _logger.LogDebug("Cleaning SQL. Length: {SqlLength}", original.Length);

             string cleaned = original;
             foreach(var rule in _cleanSqlRules)
             {
                 try
                 {
                     if (rule.Pattern != null && rule.Replacement != null)
                     {
                         cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, rule.Pattern, rule.Replacement, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                     }
                 }
                 catch (System.Exception ex)
                 {
                     _logger.LogError(ex, "Error applying clean SQL rule: {RulePattern}", rule.Pattern);
                 }
             }

             // Apply Formatting if enabled
             if (_configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.BeautifySql) != "0")
             {
                 cleaned = BeautifySql(cleaned);
             }

             CustomSqlText = cleaned;
             if (SelectedMode == SearchMode.Function) FunctionSqlText = cleaned;
             else if (SelectedMode == SearchMode.Totalizer) TotalizerSqlText = cleaned;
             else if (SelectedMode == SearchMode.Field) FieldSqlText = cleaned;

             if (_configService.GetValue(AppSettings.Sections.General, AppSettings.Keys.CopyCleanSql) == "1")
             {
                 _clipboardService.SetText(cleaned);
             }
             _logger.LogInfo("SQL cleaned.");
        }

        private string BeautifySql(string sql)
        {
            try
            {
                int indentSpaces = int.TryParse(_configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.IndentStringSpaces), out int val) ? val : 2;
                string indentStr = new string(' ', indentSpaces);

                bool expandCommaLists = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCommaLists) != null ? _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCommaLists) == "1" : true;
                bool expandBooleanExpressions = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBooleanExpressions) != null ? _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBooleanExpressions) == "1" : true;
                bool expandCaseExpressions = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCaseExpressions) != null ? _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCaseExpressions) == "1" : true;
                bool expandBetweenConditions = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBetweenConditions) != null ? _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBetweenConditions) == "1" : true;
                bool expandInLists = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandInLists) != null ? _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandInLists) == "1" : true;
                bool breakJoinOnSections = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.BreakJoinOnSections) != null ? _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.BreakJoinOnSections) == "1" : false;
                bool uppercaseKeywords = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.UppercaseKeywords) != null ? _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.UppercaseKeywords) == "1" : true;
                bool keywordStandardization = _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.KeywordStandardization) != null ? _configService.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.KeywordStandardization) == "1" : false;

                var treeFormatter = new PoorMansTSqlFormatterLib.Formatters.TSqlStandardFormatter(
                    indentString: indentStr,
                    spacesPerTab: indentSpaces,
                    maxLineWidth: 999,
                    expandCommaLists: expandCommaLists,
                    trailingCommas: expandCommaLists,
                    spaceAfterExpandedComma: true,
                    expandBooleanExpressions: expandBooleanExpressions,
                    expandCaseStatements: expandCaseExpressions,
                    expandBetweenConditions: expandBetweenConditions,
                    breakJoinOnSections: breakJoinOnSections,
                    uppercaseKeywords: uppercaseKeywords,
                    htmlColoring: false,
                    keywordStandardization: keywordStandardization
                );
                var formatter = new PoorMansTSqlFormatterLib.SqlFormattingManager(treeFormatter);

                return formatter.Format(sql);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to format SQL using PoorMansTSqlFormatter", ex);
                return sql;
            }
        }

        private void BuildSql(string? type)
        {
            _logger.LogDebug("Building SQL for type: {SearchType}, Mode: {SearchMode}", type ?? "null", SelectedMode);

            // Determine search criteria without changing UI state (preventing focus bounce)
            var criteria = new SearchCriteria { Mode = SelectedMode, AnyMatch = AnyMatch };
            if (SelectedMode == SearchMode.Function) criteria.AnyMatch = IsFunctionAnyMatch;
            else if (SelectedMode == SearchMode.Totalizer) criteria.AnyMatch = IsTotalizerAnyMatch;
            else if (SelectedMode == SearchMode.Field) criteria.AnyMatch = IsFieldAnyMatch;

            if (type == "Number")
            {
                criteria.Type = SearchType.Number;
                if (SelectedMode == SearchMode.Function) criteria.Value = FunctionNumberText;
                else if (SelectedMode == SearchMode.Totalizer) criteria.Value = TotalizerNumberText;
                else if (SelectedMode == SearchMode.Field) criteria.Value = FieldNumberText;
            }
            else if (type == "Description")
            {
                criteria.Type = SearchType.Description;
                if (SelectedMode == SearchMode.Function) criteria.Value = FunctionDescriptionText;
                else if (SelectedMode == SearchMode.Totalizer) criteria.Value = TotalizerDescriptionText;
                else if (SelectedMode == SearchMode.Field) criteria.Value = FieldDescriptionText;
            }
            else if (type == "Table" && SelectedMode == SearchMode.Field)
            {
                criteria.Type = SearchType.Table;
                criteria.Value = SelectedTable;
                criteria.ShowFields = ShowFields;
                criteria.LastTransaction = IsLastTransactionVisible && LastTransaction;
            }
            else if (type == "MultiClause" && IsMultiClauseVisible)
            {
                if (IsRecordMultiClauseMode)
                {
                    criteria.Type = SearchType.Table;
                    criteria.Value = SelectedTable;
                    criteria.ShowFields = false;
                    criteria.LastTransaction = IsLastTransactionVisible && LastTransaction;
                }
                else
                {
                    criteria.Type = SearchType.Description;
                    criteria.ShowFields = true;
                }
                criteria.RootGroup = BuildRootGroup();
            }
            else
            {
                // Fallback if no type specified
                if (IsCustomSqlMode && !IsMultiClauseVisible) return;
                criteria = GetSearchCriteria();
            }

            if (criteria.Type == SearchType.CustomSql) return;

            string sql = RenderCriteriaToSql(criteria);

            CustomSqlText = sql;
            if (SelectedMode == SearchMode.Function)
            {
                FunctionSqlText = sql;
            }
            else if (SelectedMode == SearchMode.Totalizer)
            {
                TotalizerSqlText = sql;
            }
            else if (SelectedMode == SearchMode.Field)
            {
                FieldSqlText = sql;
            }

            // Only focus/show SQL Editor panel if setting is enabled (default: true)
            string? selectCustomSqlStr = _configService.GetValue(AppSettings.Sections.Search, AppSettings.Keys.SelectCustomSqlOnBuild);
            bool selectCustomSql = selectCustomSqlStr != "0"; // Default true if null or "1"

            if (selectCustomSql)
            {
                WeakReferenceMessenger.Default.Send(new ShowPanelMessage("SqlEditor"));
            }

            _logger.LogInfo("SQL built successfully.");
        }

        private async Task RefreshTablesAsync()
        {
            if (IsRefreshingTables) return;
            try
            {
                IsRefreshingTables = true;
                _cachedSchema = null;
                string? previousSelection = SelectedTable;
                Tables.Clear();
                await LoadTablesAsync();
                if (previousSelection != null && Tables.Contains(previousSelection))
                {
                    SelectedTable = previousSelection;
                }
                _dialogService.ShowToast("Tables Refreshed", "Refresh Complete", ToastType.Info);
            }
            finally
            {
                IsRefreshingTables = false;
            }
        }

        private async Task LoadTablesAsync()
        {
            if (Tables.Count > 0) return;

            try
            {
                 var conn = _connectionSettingsService.GetConnectionSettings();

                 var tables = await _repository.GetTablesAsync(conn.Server, conn.Database, conn.User, conn.Password);
                 Tables.Clear();
                 foreach(var t in tables) Tables.Add(t);
                 _logger.LogInfo("Loaded {TableCount} tables from database.", Tables.Count);

                 // Also reload schema
                 await _intellisenseService.InitializeAsync(conn.Server, conn.Database, conn.User, conn.Password);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to load tables", ex);
            }
        }

        public SearchCriteria GetSearchCriteria()
        {
            var criteria = new SearchCriteria { Mode = SelectedMode, AnyMatch = AnyMatch };
            if (SelectedMode == SearchMode.Function) criteria.AnyMatch = IsFunctionAnyMatch;
            else if (SelectedMode == SearchMode.Totalizer) criteria.AnyMatch = IsTotalizerAnyMatch;
            else if (SelectedMode == SearchMode.Field) criteria.AnyMatch = IsFieldAnyMatch;

            if (IsMultiClauseVisible)
            {
                if (IsRecordMultiClauseMode)
                {
                    criteria.Type = SearchType.Table;
                    criteria.Value = SelectedTable;
                    criteria.ShowFields = false;
                    criteria.LastTransaction = IsLastTransactionVisible && LastTransaction;
                }
                else
                {
                    criteria.Type = SearchType.Description;
                    criteria.ShowFields = true;
                }
                criteria.RootGroup = BuildRootGroup();
                return criteria;
            }

            if (SelectedMode == SearchMode.Function)
            {
                if (IsFunctionNumber)
                {
                    criteria.Type = SearchType.Number;
                    criteria.Value = FunctionNumberText;
                }
                else if (IsFunctionDescription)
                {
                    criteria.Type = SearchType.Description;
                    criteria.Value = FunctionDescriptionText;
                }
                else if (IsFunctionCustomSql)
                {
                    criteria.Type = SearchType.CustomSql;
                    criteria.Value = !string.IsNullOrEmpty(FunctionSqlSelectedText) ? FunctionSqlSelectedText : FunctionSqlText;
                }
            }
            else if (SelectedMode == SearchMode.Totalizer)
            {
                if (IsTotalizerNumber)
                {
                    criteria.Type = SearchType.Number;
                    criteria.Value = TotalizerNumberText;
                }
                else if (IsTotalizerDescription)
                {
                    criteria.Type = SearchType.Description;
                    criteria.Value = TotalizerDescriptionText;
                }
                else if (IsTotalizerCustomSql)
                {
                    criteria.Type = SearchType.CustomSql;
                    criteria.Value = !string.IsNullOrEmpty(TotalizerSqlSelectedText) ? TotalizerSqlSelectedText : TotalizerSqlText;
                }
            }
            else if (SelectedMode == SearchMode.Field)
            {
                if (ShowRecords)
                {
                    criteria.Type = SearchType.Table;
                    criteria.Value = SelectedTable;
                    criteria.ShowFields = false;
                    criteria.LastTransaction = IsLastTransactionVisible && LastTransaction;
                }
                else if (IsFieldNumber)
                {
                    criteria.Type = SearchType.Number;
                    criteria.Value = FieldNumberText;
                }
                else if (IsFieldDescription)
                {
                    criteria.Type = SearchType.Description;
                    criteria.Value = FieldDescriptionText;
                }
                else if (IsFieldCustomSql)
                {
                    criteria.Type = SearchType.CustomSql;
                    criteria.Value = !string.IsNullOrEmpty(FieldSqlSelectedText) ? FieldSqlSelectedText : FieldSqlText;
                }
                else if (IsFieldTable)
                {
                    criteria.Type = SearchType.Table;
                    criteria.Value = SelectedTable;
                    criteria.ShowFields = ShowFields;
                    criteria.LastTransaction = IsLastTransactionVisible && LastTransaction;
                }
            }

            return criteria;
        }
    }
}
