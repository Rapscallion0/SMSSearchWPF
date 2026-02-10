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

        public SearchViewModel(IDataRepository repository, IDialogService dialogService, IConfigService configService, ILoggerService logger)
        {
            _repository = repository;
            _dialogService = dialogService;
            _configService = configService;
            _logger = logger;
            LoadTablesCommand = new AsyncRelayCommand(LoadTablesAsync);
        }

        [ObservableProperty]
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
        private bool _isFunctionCustomSql;

        // Totalizer Tab Properties
        [ObservableProperty]
        private bool _isTotalizerNumber = true;
        [ObservableProperty]
        private bool _isTotalizerDescription;
        [ObservableProperty]
        private bool _isTotalizerCustomSql;

        // Field Tab Properties
        [ObservableProperty]
        private bool _isFieldNumber = true;
        [ObservableProperty]
        private bool _isFieldDescription;
        [ObservableProperty]
        private bool _isFieldTable;
        [ObservableProperty]
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

        public IAsyncRelayCommand LoadTablesCommand { get; }

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
