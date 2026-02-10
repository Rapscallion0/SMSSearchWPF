using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Services;
using SMS_Search.Views;
using SMS_Search.Data;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IConfigService _config;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService _logger;
        private readonly IQueryHistoryService _historyService; // Restore history service dependency

        public MainViewModel(
            IConfigService config,
            IDialogService dialogService,
            IServiceProvider serviceProvider,
            ILoggerService logger,
            IQueryHistoryService historyService, // Inject it
            SearchViewModel searchViewModel,
            ResultsViewModel resultsViewModel)
        {
            _config = config;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _historyService = historyService;
            SearchVm = searchViewModel;
            ResultsVm = resultsViewModel;
        }

        public SearchViewModel SearchVm { get; }
        public ResultsViewModel ResultsVm { get; }

        [RelayCommand]
        private async Task Search()
        {
             var criteria = SearchVm.GetSearchCriteria();

             // Basic validation
             if (criteria.Type != SearchType.Table && string.IsNullOrWhiteSpace(criteria.Value))
             {
                 _dialogService.ShowToast("Please enter a search term.", "Search", ToastType.Warning);
                 return;
             }

             // Add to history
             if (criteria.Type != SearchType.Table && !string.IsNullOrWhiteSpace(criteria.Value))
             {
                 _historyService.AddQuery(SearchVm.SelectedMode.ToString(), criteria.Value);
             }

             await ResultsVm.ExecuteSearchAsync(criteria);
        }

        [RelayCommand]
        private void OpenSettings()
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                var encryptWin = new EncryptionUtilityWindow();
                encryptWin.DataContext = _serviceProvider.GetRequiredService<EncryptionUtilityViewModel>();
                encryptWin.Owner = System.Windows.Application.Current.MainWindow;
                encryptWin.ShowDialog();
            }
            else
            {
                var settingsWin = _serviceProvider.GetRequiredService<SettingsWindow>();
                settingsWin.DataContext = _serviceProvider.GetRequiredService<SettingsViewModel>();
                settingsWin.Owner = System.Windows.Application.Current.MainWindow;
                settingsWin.ShowDialog();
            }
        }
    }
}
