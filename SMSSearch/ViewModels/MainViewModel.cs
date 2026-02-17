using System;
using System.ComponentModel;
using System.Threading.Tasks;
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
    public partial class MainViewModel : ObservableObject
    {
        private readonly IConfigService _config;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService _logger;
        private readonly IQueryHistoryService _historyService; // Restore history service dependency

        public event Action? RequestOpenSettings;
        public event Action<bool>? RequestToggleUnarchiveWindow;

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

            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            Title = $"SMS Search - V{version}";

            GregorianDate = DateTime.Today;
        }

        public string Title { get; }

        [ObservableProperty]
        private string _julianDateText = "";

        [ObservableProperty]
        private DateTime? _gregorianDate;

        [ObservableProperty]
        private bool _isUnarchiveTargetVisible;

        partial void OnIsUnarchiveTargetVisibleChanged(bool value)
        {
            RequestToggleUnarchiveWindow?.Invoke(value);
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
