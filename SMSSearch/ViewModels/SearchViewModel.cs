using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SMS_Search.ViewModels
{
    public partial class SearchViewModel : ObservableObject
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
            CleanSqlCommand = new RelayCommand(CleanSql);
            BuildSqlCommand = new RelayCommand<string>(BuildSql);
            ShowHistoryCommand = new RelayCommand<System.Windows.Controls.Button>(ShowHistory);
            LoadCleanSqlRules();
            LoadHistory();
            LoadFontSettings();
            LoadAnyMatchConfig();

            if (System.Enum.TryParse(_configService.GetValue("GENERAL", "DEFAULT_TAB"), out SearchMode tabMode))
            {
                SelectedMode = tabMode;
            }

            WeakReferenceMessenger.Default.Register<SqlFontSettingsChangedMessage>(this, (r, m) =>
            {
                SqlFontFamily = m.Value.Family;
                SqlFontSize = m.Value.Size;
            });

            InitializeIntellisense();
        }

        private void InitializeIntellisense()
        {
            var server = _configService.GetValue("CONNECTION", "SERVER") ?? "";
            var database = _configService.GetValue("CONNECTION", "DATABASE") ?? "";
            var user = _configService.GetValue("CONNECTION", "SQLUSER") ?? "";
            var pass = _configService.GetValue("CONNECTION", "SQLPASSWORD");
            string? decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;

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

        // Function Tab Inputs
        [ObservableProperty]
        private string _functionNumberText = "";
        [ObservableProperty]
        private string _functionDescriptionText = "";
        [ObservableProperty]
        private string _functionSqlText = "";

        // Totalizer Tab Inputs
        [ObservableProperty]
        private string _totalizerNumberText = "";
        [ObservableProperty]
        private string _totalizerDescriptionText = "";
        [ObservableProperty]
        private string _totalizerSqlText = "";

        // Field Tab Inputs
        [ObservableProperty]
        private string _fieldNumberText = "";
        [ObservableProperty]
        private string _fieldDescriptionText = "";
        [ObservableProperty]
        private string _fieldSqlText = "";

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
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private bool _isFieldCustomSql;

        [ObservableProperty]
        private ObservableCollection<string> _tables = new ObservableCollection<string>();

        [ObservableProperty]
        private string _selectedTable = "";

        [ObservableProperty]
        private bool _showFields = true;

        [ObservableProperty]
        private bool _showRecords;

        partial void OnShowRecordsChanged(bool value)
        {
            if (value)
            {
                ShowFields = false;
                IsFieldTable = true;
            }
        }

        partial void OnShowFieldsChanged(bool value)
        {
            if (value)
            {
                ShowRecords = false;
                IsFieldTable = true;
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
            }
        }

        public IAsyncRelayCommand LoadTablesCommand { get; }
        public IRelayCommand CleanSqlCommand { get; }
        public IRelayCommand<string> BuildSqlCommand { get; }
        public IRelayCommand<System.Windows.Controls.Button> ShowHistoryCommand { get; }

        public bool IsCustomSqlMode => (SelectedMode == SearchMode.Function && IsFunctionCustomSql) ||
                                       (SelectedMode == SearchMode.Totalizer && IsTotalizerCustomSql) ||
                                       (SelectedMode == SearchMode.Field && IsFieldCustomSql);

        [ObservableProperty]
        private ObservableCollection<string> _functionHistory = new();
        [ObservableProperty]
        private ObservableCollection<string> _totalizerHistory = new();
        [ObservableProperty]
        private ObservableCollection<string> _fieldHistory = new();

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

        private void LoadHistory()
        {
             FunctionHistory = new ObservableCollection<string>(_historyService.GetHistory("Function"));
             TotalizerHistory = new ObservableCollection<string>(_historyService.GetHistory("Totalizer"));
             FieldHistory = new ObservableCollection<string>(_historyService.GetHistory("Field"));
        }

        private void ShowHistory(System.Windows.Controls.Button? btn)
        {
            if (btn == null) return;

            var menu = new System.Windows.Controls.ContextMenu();
            System.Collections.Generic.IEnumerable<string>? history = SelectedMode switch
            {
                SearchMode.Function => FunctionHistory,
                SearchMode.Totalizer => TotalizerHistory,
                SearchMode.Field => FieldHistory,
                _ => null
            };

            if (history != null)
            {
                foreach (var item in history)
                {
                    // Truncate for display
                    string display = item.Length > 50 ? item.Substring(0, 47) + "..." : item;
                    display = display.Replace("\r", " ").Replace("\n", " ");

                    var mi = new System.Windows.Controls.MenuItem { Header = display, ToolTip = item };
                    string fullText = item;
                    mi.Click += (s, e) => SetCurrentSearchText(fullText);
                    menu.Items.Add(mi);
                }

                if (menu.Items.Count > 0)
                {
                    menu.Items.Add(new System.Windows.Controls.Separator());
                    var clear = new System.Windows.Controls.MenuItem { Header = "Clear History" };
                    clear.Click += (s, e) =>
                    {
                        _logger.LogInfo($"Clearing history for mode: {SelectedMode}");
                        _historyService.ClearHistory(SelectedMode.ToString());
                        LoadHistory();
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

        private void SetCurrentSearchText(string value)
        {
            if (SelectedMode == SearchMode.Function)
            {
                if (IsFunctionCustomSql) FunctionSqlText = value;
                else if (IsFunctionNumber) FunctionNumberText = value;
                else if (IsFunctionDescription) FunctionDescriptionText = value;
            }
            else if (SelectedMode == SearchMode.Totalizer)
            {
                if (IsTotalizerCustomSql) TotalizerSqlText = value;
                else if (IsTotalizerNumber) TotalizerNumberText = value;
                else if (IsTotalizerDescription) TotalizerDescriptionText = value;
            }
            else if (SelectedMode == SearchMode.Field)
            {
                if (IsFieldCustomSql) FieldSqlText = value;
                else if (IsFieldNumber) FieldNumberText = value;
                else if (IsFieldDescription) FieldDescriptionText = value;
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

             if (SelectedMode == SearchMode.Function) FunctionSqlText = cleaned;
             else if (SelectedMode == SearchMode.Totalizer) TotalizerSqlText = cleaned;
             else if (SelectedMode == SearchMode.Field) FieldSqlText = cleaned;

             if (_configService.GetValue("GENERAL", "COPYCLEANSQL") == "1")
             {
                 _clipboardService.SetText(cleaned);
             }
             _logger.LogInfo("SQL cleaned.");
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
                criteria.LastTransaction = LastTransaction;
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

        private async Task LoadTablesAsync()
        {
            try
            {
                 var server = _configService.GetValue("CONNECTION", "SERVER") ?? "";
                 var database = _configService.GetValue("CONNECTION", "DATABASE") ?? "";
                 var user = _configService.GetValue("CONNECTION", "SQLUSER") ?? "";
                 var pass = _configService.GetValue("CONNECTION", "SQLPASSWORD");
                 string? decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;

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
                    criteria.Value = FunctionSqlText;
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
                    criteria.Value = TotalizerSqlText;
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
                    criteria.Value = FieldSqlText;
                }
                else if (IsFieldTable)
                {
                    criteria.Type = SearchType.Table;
                    criteria.Value = SelectedTable;
                    criteria.ShowFields = ShowFields;
                    criteria.LastTransaction = LastTransaction;
                }
            }

            return criteria;
        }
    }
}
