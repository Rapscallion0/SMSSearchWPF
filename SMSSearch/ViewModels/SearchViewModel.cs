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

        private System.Collections.Generic.List<SqlCleaningRule> _cleanSqlRules = new();

        public SearchViewModel(
            IDataRepository repository,
            IDialogService dialogService,
            IConfigService configService,
            ILoggerService logger,
            IQueryHistoryService historyService,
            IClipboardService clipboardService,
            IIntellisenseService intellisenseService)
        {
            _repository = repository;
            _dialogService = dialogService;
            _configService = configService;
            _logger = logger;
            _historyService = historyService;
            _clipboardService = clipboardService;
            _intellisenseService = intellisenseService;

            LoadTablesCommand = new AsyncRelayCommand(LoadTablesAsync);
            RefreshTablesCommand = new AsyncRelayCommand(RefreshTablesAsync);
            CleanSqlCommand = new RelayCommand(CleanSql);
            BuildSqlCommand = new RelayCommand<string>(BuildSql);
            ShowHistoryCommand = new RelayCommand<System.Windows.Controls.Button>(ShowHistory);
            LoadCleanSqlRules();
            LoadFontSettings();
            LoadAnyMatchConfig();

            if (System.Enum.TryParse(_configService.GetValue("GENERAL", "DEFAULT_TABLE_ACTION"), out DefaultTableAction tableAction))
            {
                if (tableAction == DefaultTableAction.QueryRecords)
                {
                    ShowRecords = true;
                }
                else
                {
                    ShowFields = true;
                }
            }

            if (System.Enum.TryParse(_configService.GetValue("GENERAL", "DEFAULT_TAB"), out DefaultSearchTabMode tabMode))
            {
                if (tabMode == DefaultSearchTabMode.Last)
                {
                    if (System.Enum.TryParse(_configService.GetValue("MAIN", "LAST_TAB"), out SearchMode lastMode))
                    {
                        SelectedMode = lastMode;
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

            InitializeIntellisense();

            TablesView = CollectionViewSource.GetDefaultView(Tables);
        }

        public void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void InitializeIntellisense()
        {
            var server = _configService.GetValue("CONNECTION", "SERVER") ?? "";
            var database = _configService.GetValue("CONNECTION", "DATABASE") ?? "";

            string user = "";
            string? decryptedPass = null;

            bool isWindowsAuth = true;
            if (bool.TryParse(_configService.GetValue("CONNECTION", "WINDOWSAUTH"), out bool b))
            {
                isWindowsAuth = b;
            }

            if (!isWindowsAuth)
            {
                user = _configService.GetValue("CONNECTION", "SQLUSER") ?? "";
                var pass = _configService.GetValue("CONNECTION", "SQLPASSWORD");
                decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;
            }

            // Fire and forget
            Task.Run(() => _intellisenseService.InitializeAsync(server, database, user, decryptedPass));
        }

        private void LoadFontSettings()
        {
            string? font = _configService.GetValue("GENERAL", "SQL_FONT_FAMILY");
            SqlFontFamily = string.IsNullOrEmpty(font) ? "Consolas" : font;

            if (int.TryParse(_configService.GetValue("GENERAL", "SQL_FONT_SIZE"), out int size))
            {
                SqlFontSize = size;
            }
        }

        private void LoadAnyMatchConfig()
        {
            if (bool.TryParse(_configService.GetValue("GENERAL", "ANY_MATCH_DEFAULT"), out bool result))
            {
                AnyMatch = result;
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private SearchMode _selectedMode;

        partial void OnSelectedModeChanged(SearchMode value)
        {
            if (value == SearchMode.Field && LoadTablesCommand != null)
            {
                LoadTablesCommand.Execute(null);
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
        private bool _isFunctionDescription;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private bool _isFunctionCustomSql;

        // Totalizer Tab Properties
        [ObservableProperty]
        private bool _isTotalizerNumber = true;
        [ObservableProperty]
        private bool _isTotalizerDescription;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private bool _isTotalizerCustomSql;

        // Field Tab Properties
        [ObservableProperty]
        private bool _isFieldNumber = true;
        [ObservableProperty]
        private bool _isFieldDescription;
        [ObservableProperty]
        private bool _isFieldTable;

        [ObservableProperty]
        private bool _isRefreshingTables;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private bool _isFieldCustomSql;

        [ObservableProperty]
        private ObservableCollection<string> _tables = new ObservableCollection<string>();

        public ICollectionView TablesView { get; private set; }

        public void FilterTables(string searchText)
        {
            if (TablesView == null) return;

            // When applying filter, we also want to sort the items so that StartsWith items come first.
            // ICollectionView Custom Sorting in WPF is done via CustomSort (ListCollectionView).
            if (TablesView is System.Windows.Data.ListCollectionView lcv)
            {
                lcv.CustomSort = new TableSortComparer(searchText);
            }

            TablesView.Filter = (obj) =>
            {
                if (string.IsNullOrEmpty(searchText)) return true;
                if (obj is string str)
                {
                    return str.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return false;
            };
            TablesView.Refresh();
        }

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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLastTransactionVisible))]
        private bool _showFields = true;

        [ObservableProperty]
        private bool _showRecords;

        partial void OnShowRecordsChanged(bool value)
        {
            if (value)
            {
                ShowFields = false;
                IsFieldTable = true;
                WeakReferenceMessenger.Default.Send(new FocusTableMessage(true));
            }
        }

        partial void OnShowFieldsChanged(bool value)
        {
            if (value)
            {
                ShowRecords = false;
                IsFieldTable = true;
                WeakReferenceMessenger.Default.Send(new FocusTableMessage(true));
            }
        }

        [ObservableProperty]
        private bool _lastTransaction;

        partial void OnLastTransactionChanged(bool value)
        {
            if (value)
            {
                IsFieldTable = true;
                ShowRecords = true;
                if (IsLastTransactionVisible)
                {
                    WeakReferenceMessenger.Default.Send(new FocusTableMessage(true));
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

        public bool IsCustomSqlMode => (SelectedMode == SearchMode.Function && IsFunctionCustomSql) ||
                                       (SelectedMode == SearchMode.Totalizer && IsTotalizerCustomSql) ||
                                       (SelectedMode == SearchMode.Field && IsFieldCustomSql);

        private void LoadCleanSqlRules()
        {
             _cleanSqlRules.Clear();
             string? countStr = _configService.GetValue("CLEAN_SQL", "Count");
             if (int.TryParse(countStr, out int count) && count > 0)
             {
                 for (int i = 0; i < count; i++)
                 {
                     string? pattern = _configService.GetValue("CLEAN_SQL", "Rule_" + i + "_Regex");
                     string? replacement = _configService.GetValue("CLEAN_SQL", "Rule_" + i + "_Replace");
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

        private void ShowHistory(System.Windows.Controls.Button? btn)
        {
            if (btn == null) return;
            string? tag = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            string key = $"{SelectedMode}_{tag}";
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
                        if (SelectedMode == SearchMode.Function)
                        {
                            if (tag == "Number") { FunctionNumberText = fullText; IsFunctionNumber = true; }
                            else if (tag == "Description") { FunctionDescriptionText = fullText; IsFunctionDescription = true; }
                            else if (tag == "CustomSql") { FunctionSqlText = fullText; IsFunctionCustomSql = true; }
                        }
                        else if (SelectedMode == SearchMode.Totalizer)
                        {
                            if (tag == "Number") { TotalizerNumberText = fullText; IsTotalizerNumber = true; }
                            else if (tag == "Description") { TotalizerDescriptionText = fullText; IsTotalizerDescription = true; }
                            else if (tag == "CustomSql") { TotalizerSqlText = fullText; IsTotalizerCustomSql = true; }
                        }
                        else if (SelectedMode == SearchMode.Field)
                        {
                            if (tag == "Number") { FieldNumberText = fullText; IsFieldNumber = true; }
                            else if (tag == "Description") { FieldDescriptionText = fullText; IsFieldDescription = true; }
                            else if (tag == "CustomSql") { FieldSqlText = fullText; IsFieldCustomSql = true; }
                        }
                    };
                    menu.Items.Add(mi);
                }

                if (menu.Items.Count > 0)
                {
                    menu.Items.Add(new System.Windows.Controls.Separator());
                    var clear = new System.Windows.Controls.MenuItem { Header = "Clear History" };
                    clear.Click += (s, e) =>
                    {
                        _logger.LogInfo($"Clearing history for key: {key}");
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

        private void CleanSql()
        {
             if (!IsFunctionCustomSql && !IsTotalizerCustomSql && !IsFieldCustomSql) return;

             string original = "";
             if (SelectedMode == SearchMode.Function) original = FunctionSqlText;
             else if (SelectedMode == SearchMode.Totalizer) original = TotalizerSqlText;
             else if (SelectedMode == SearchMode.Field) original = FieldSqlText;

             if (string.IsNullOrEmpty(original)) return;

             _logger.LogDebug($"Cleaning SQL for mode {SelectedMode}. Length: {original.Length}");

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
                     _logger.LogError($"Error applying clean SQL rule: {rule.Pattern}", ex);
                 }
             }

             // Apply Formatting if enabled
             if (_configService.GetValue("CLEAN_SQL", "BEAUTIFY_SQL") != "0")
             {
                 cleaned = BeautifySql(cleaned);
             }

             if (SelectedMode == SearchMode.Function) FunctionSqlText = cleaned;
             else if (SelectedMode == SearchMode.Totalizer) TotalizerSqlText = cleaned;
             else if (SelectedMode == SearchMode.Field) FieldSqlText = cleaned;

             if (_configService.GetValue("GENERAL", "COPYCLEANSQL") == "1")
             {
                 _clipboardService.SetText(cleaned);
             }
             _logger.LogInfo("SQL cleaned.");
        }

        private string BeautifySql(string sql)
        {
            try
            {
                int indentSpaces = int.TryParse(_configService.GetValue("CLEAN_SQL", "INDENT_STRING_SPACES"), out int val) ? val : 2;
                string indentStr = new string(' ', indentSpaces);

                bool expandCommaLists = _configService.GetValue("CLEAN_SQL", "EXPAND_COMMA_LISTS") != null ? _configService.GetValue("CLEAN_SQL", "EXPAND_COMMA_LISTS") == "1" : true;
                bool expandBooleanExpressions = _configService.GetValue("CLEAN_SQL", "EXPAND_BOOLEAN_EXPRESSIONS") != null ? _configService.GetValue("CLEAN_SQL", "EXPAND_BOOLEAN_EXPRESSIONS") == "1" : true;
                bool expandCaseExpressions = _configService.GetValue("CLEAN_SQL", "EXPAND_CASE_EXPRESSIONS") != null ? _configService.GetValue("CLEAN_SQL", "EXPAND_CASE_EXPRESSIONS") == "1" : true;
                bool expandBetweenConditions = _configService.GetValue("CLEAN_SQL", "EXPAND_BETWEEN_CONDITIONS") != null ? _configService.GetValue("CLEAN_SQL", "EXPAND_BETWEEN_CONDITIONS") == "1" : true;
                bool expandInLists = _configService.GetValue("CLEAN_SQL", "EXPAND_IN_LISTS") != null ? _configService.GetValue("CLEAN_SQL", "EXPAND_IN_LISTS") == "1" : true;
                bool breakJoinOnSections = _configService.GetValue("CLEAN_SQL", "BREAK_JOIN_ON_SECTIONS") != null ? _configService.GetValue("CLEAN_SQL", "BREAK_JOIN_ON_SECTIONS") == "1" : false;
                bool uppercaseKeywords = _configService.GetValue("CLEAN_SQL", "UPPERCASE_KEYWORDS") != null ? _configService.GetValue("CLEAN_SQL", "UPPERCASE_KEYWORDS") == "1" : true;
                bool keywordStandardization = _configService.GetValue("CLEAN_SQL", "KEYWORD_STANDARDIZATION") != null ? _configService.GetValue("CLEAN_SQL", "KEYWORD_STANDARDIZATION") == "1" : false;

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
            _logger.LogDebug($"Building SQL for type: {type ?? "null"}, Mode: {SelectedMode}");

            // Determine search criteria without changing UI state (preventing focus bounce)
            var criteria = new SearchCriteria { Mode = SelectedMode, AnyMatch = AnyMatch };

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
            else
            {
                // Fallback if no type specified
                if (IsCustomSqlMode) return;
                criteria = GetSearchCriteria();
            }

            if (criteria.Type == SearchType.CustomSql) return;

            string fctFields = _configService.GetValue("QUERY", "FUNCTION") ?? "";
            string tlzFields = _configService.GetValue("QUERY", "TOTALIZER") ?? "";
            var qb = new QueryBuilder(fctFields, tlzFields);

            var result = qb.Build(criteria);
            string sql = result.Sql;

            if (result.Parameters is Dapper.DynamicParameters dp)
            {
                // Sort by length descending to prevent substring replacement issues (e.g. replacing @p1 inside @p10)
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

            sql = BeautifySql(sql);

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

            // Only switch to Custom SQL mode if setting is enabled (default: true)
            string? selectCustomSqlStr = _configService.GetValue("GENERAL", "SELECT_CUSTOM_SQL_ON_BUILD");
            bool selectCustomSql = selectCustomSqlStr != "0"; // Default true if null or "1"

            if (selectCustomSql)
            {
                if (SelectedMode == SearchMode.Function) IsFunctionCustomSql = true;
                else if (SelectedMode == SearchMode.Totalizer) IsTotalizerCustomSql = true;
                else if (SelectedMode == SearchMode.Field) IsFieldCustomSql = true;
            }

            _logger.LogInfo("SQL built successfully.");
        }

        private async Task RefreshTablesAsync()
        {
            if (IsRefreshingTables) return;
            try
            {
                IsRefreshingTables = true;
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
                 var server = _configService.GetValue("CONNECTION", "SERVER") ?? "";
                 var database = _configService.GetValue("CONNECTION", "DATABASE") ?? "";
                 string user = "";
                 string? decryptedPass = null;

                 bool isWindowsAuth = true;
                 if (bool.TryParse(_configService.GetValue("CONNECTION", "WINDOWSAUTH"), out bool b))
                 {
                     isWindowsAuth = b;
                 }

                 if (!isWindowsAuth)
                 {
                     user = _configService.GetValue("CONNECTION", "SQLUSER") ?? "";
                     var pass = _configService.GetValue("CONNECTION", "SQLPASSWORD");
                     decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;
                 }

                 var tables = await _repository.GetTablesAsync(server, database, user, decryptedPass);
                 Tables.Clear();
                 foreach(var t in tables) Tables.Add(t);
                 _logger.LogInfo($"Loaded {Tables.Count} tables from database.");

                 // Also reload schema
                 await _intellisenseService.InitializeAsync(server, database, user, decryptedPass);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Failed to load tables", ex);
            }
        }

        public SearchCriteria GetSearchCriteria()
        {
            var criteria = new SearchCriteria { Mode = SelectedMode, AnyMatch = AnyMatch };

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
                if (IsFieldNumber)
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
