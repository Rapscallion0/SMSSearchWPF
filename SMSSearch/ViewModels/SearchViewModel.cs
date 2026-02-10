using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        private System.Collections.Generic.List<SqlCleaningRule> _cleanSqlRules = new();

        public SearchViewModel(
            IDataRepository repository,
            IDialogService dialogService,
            IConfigService configService,
            ILoggerService logger,
            IQueryHistoryService historyService,
            IClipboardService clipboardService)
        {
            _repository = repository;
            _dialogService = dialogService;
            _configService = configService;
            _logger = logger;
            _historyService = historyService;
            _clipboardService = clipboardService;

            LoadTablesCommand = new AsyncRelayCommand(LoadTablesAsync);
            CleanSqlCommand = new RelayCommand(CleanSql);
            ShowHistoryCommand = new RelayCommand<System.Windows.Controls.Button>(ShowHistory);
            LoadCleanSqlRules();
            LoadHistory();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomSqlMode))]
        private SearchMode _selectedMode;

        [ObservableProperty]
        private string _searchText;

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
        private string _selectedTable;

        [ObservableProperty]
        private bool _showFields = true;

        [ObservableProperty]
        private bool _showRecords;

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
             string countStr = _configService.GetValue("CLEAN_SQL", "Count");
             if (int.TryParse(countStr, out int count) && count > 0)
             {
                 for (int i = 0; i < count; i++)
                 {
                     string pattern = _configService.GetValue("CLEAN_SQL", "Rule_" + i + "_Regex");
                     string replacement = _configService.GetValue("CLEAN_SQL", "Rule_" + i + "_Replace");
                     if (!string.IsNullOrEmpty(pattern))
                     {
                         _cleanSqlRules.Add(new SqlCleaningRule { Pattern = pattern, Replacement = replacement });
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

        private void ShowHistory(System.Windows.Controls.Button btn)
        {
            if (btn == null) return;

            var menu = new System.Windows.Controls.ContextMenu();
            System.Collections.Generic.IEnumerable<string> history = SelectedMode switch
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
                    mi.Click += (s, e) => SearchText = fullText;
                    menu.Items.Add(mi);
                }

                if (menu.Items.Count > 0)
                {
                    menu.Items.Add(new System.Windows.Controls.Separator());
                    var clear = new System.Windows.Controls.MenuItem { Header = "Clear History" };
                    clear.Click += (s, e) =>
                    {
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

        private void CleanSql()
        {
             if (!IsFunctionCustomSql && !IsTotalizerCustomSql && !IsFieldCustomSql) return;

             string original = SearchText;
             if (string.IsNullOrEmpty(original)) return;

             string cleaned = original;
             foreach(var rule in _cleanSqlRules)
             {
                 try
                 {
                     cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, rule.Pattern, rule.Replacement, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                 }
                 catch { }
             }

             SearchText = cleaned;

             if (_configService.GetValue("GENERAL", "COPYCLEANSQL") == "1")
             {
                 _clipboardService.SetText(cleaned);
             }
        }

        private async Task LoadTablesAsync()
        {
            try
            {
                 var server = _configService.GetValue("CONNECTION", "SERVER");
                 var database = _configService.GetValue("CONNECTION", "DATABASE");
                 var user = _configService.GetValue("CONNECTION", "SQLUSER");
                 var pass = _configService.GetValue("CONNECTION", "SQLPASSWORD");
                 string decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;

                 var tables = await _repository.GetTablesAsync(server, database, user, decryptedPass);
                 Tables.Clear();
                 foreach(var t in tables) Tables.Add(t);
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
                criteria.Value = SearchText;
                if (IsFunctionNumber) criteria.Type = SearchType.Number;
                else if (IsFunctionDescription) criteria.Type = SearchType.Description;
                else if (IsFunctionCustomSql) criteria.Type = SearchType.CustomSql;
            }
            else if (SelectedMode == SearchMode.Totalizer)
            {
                criteria.Value = SearchText;
                if (IsTotalizerNumber) criteria.Type = SearchType.Number;
                else if (IsTotalizerDescription) criteria.Type = SearchType.Description;
                else if (IsTotalizerCustomSql) criteria.Type = SearchType.CustomSql;
            }
            else if (SelectedMode == SearchMode.Field)
            {
                criteria.Value = SearchText;
                if (IsFieldNumber) criteria.Type = SearchType.Number;
                else if (IsFieldDescription) criteria.Type = SearchType.Description;
                else if (IsFieldCustomSql) criteria.Type = SearchType.CustomSql;
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
