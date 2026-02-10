using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SMS_Search.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IConfigService _configService;
        private readonly IQueryHistoryService _historyService;
        private readonly IHotkeyService _hotkeyService;

        public event Action RequestOpenSettings;

        public MainViewModel(
            SearchViewModel searchViewModel,
            ResultsViewModel resultsViewModel,
            ILoggerService logger,
            IDialogService dialogService,
            IConfigService configService,
            IQueryHistoryService historyService,
            IHotkeyService hotkeyService)
        {
            SearchViewModel = searchViewModel;
            ResultsViewModel = resultsViewModel;
            _logger = logger;
            _dialogService = dialogService;
            _configService = configService;
            _historyService = historyService;
            _hotkeyService = hotkeyService;

            ExecuteSearchCommand = new AsyncRelayCommand(ExecuteSearch);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
        }

        public SearchViewModel SearchViewModel { get; }
        public ResultsViewModel ResultsViewModel { get; }

        public IAsyncRelayCommand ExecuteSearchCommand { get; }
        public IRelayCommand OpenSettingsCommand { get; }

        private async Task ExecuteSearch()
        {
             var criteria = SearchViewModel.GetSearchCriteria();

             await ResultsViewModel.ExecuteSearchAsync(criteria);

             if (criteria.Type == SearchType.CustomSql)
             {
                 _historyService.AddQuery(criteria.Mode.ToString(), criteria.Value);
             }
        }

        private void OpenSettings()
        {
            RequestOpenSettings?.Invoke();
        }
    }
}
