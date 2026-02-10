using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace SMS_Search.ViewModels
{
    public partial class ResultsViewModel : ObservableObject
    {
        private readonly VirtualGridContext _gridContext;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly QueryBuilder _queryBuilder;
        private readonly IConfigService _configService;

        public ResultsViewModel(VirtualGridContext gridContext, ILoggerService logger, IDialogService dialogService, IConfigService configService)
        {
            _gridContext = gridContext;
            _logger = logger;
            _dialogService = dialogService;
            _configService = configService;

            string fctFields = _configService.GetValue("QUERY", "FUNCTION");
            string tlzFields = _configService.GetValue("QUERY", "TOTALIZER");
            _queryBuilder = new QueryBuilder(fctFields, tlzFields);

            _gridContext.DataReady += OnDataReady;
            _gridContext.LoadError += OnLoadError;

            ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
            ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync);
            ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync);
        }

        [ObservableProperty]
        private IList _searchResults;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusText;

        [ObservableProperty]
        private int _totalRecords;

        public IAsyncRelayCommand ExportCsvCommand { get; }
        public IAsyncRelayCommand ExportJsonCommand { get; }
        public IAsyncRelayCommand ExportExcelCommand { get; }

        private void OnDataReady(object sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (SearchResults is VirtualizingCollection vc)
                {
                    vc.Refresh();
                    TotalRecords = _gridContext.TotalCount;
                    StatusText = $"Found {TotalRecords} records";
                }
            });
        }

        private void OnLoadError(object sender, string msg)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _dialogService.ShowError(msg, "Data Load Error");
                StatusText = "Error loading data";
            });
        }

        public async Task ExecuteSearchAsync(SearchCriteria criteria)
        {
            IsBusy = true;
            StatusText = "Searching...";

            try
            {
                var queryResult = _queryBuilder.Build(criteria);

                var server = _configService.GetValue("CONNECTION", "SERVER");
                var database = _configService.GetValue("CONNECTION", "DATABASE");
                var user = _configService.GetValue("CONNECTION", "SQLUSER");
                var pass = _configService.GetValue("CONNECTION", "SQLPASSWORD");
                string decryptedPass = !string.IsNullOrEmpty(pass) ? SMS_Search.Utils.GeneralUtils.Decrypt(pass) : null;

                _gridContext.SetConnection(server, database, user, decryptedPass);

                // Load initial count and cache schema
                var schema = await _gridContext.GetSchemaAsync(queryResult.Sql, queryResult.Parameters);
                await _gridContext.LoadAsync(queryResult.Sql, queryResult.Parameters);

                // Create new collection
                SearchResults = new VirtualizingCollection(_gridContext, schema);

                TotalRecords = _gridContext.TotalCount;
                StatusText = $"Found {TotalRecords} records";
            }
            catch (Exception ex)
            {
                StatusText = "Error";
                _dialogService.ShowError(ex.Message, "Search Failed");
                _logger.LogError("Search failed", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExportCsvAsync()
        {
            string filename = _dialogService.SaveFileDialog("CSV files (*.csv)|*.csv", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (string.IsNullOrEmpty(filename)) return;
            await PerformExportAsync(() => _gridContext.ExportToCsvAsync(filename));
        }

        private async Task ExportJsonAsync()
        {
            string filename = _dialogService.SaveFileDialog("JSON files (*.json)|*.json", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            if (string.IsNullOrEmpty(filename)) return;
            await PerformExportAsync(() => _gridContext.ExportToJsonAsync(filename));
        }

        private async Task ExportExcelAsync()
        {
            string filename = _dialogService.SaveFileDialog("Excel XML (*.xml)|*.xml", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            if (string.IsNullOrEmpty(filename)) return;
            await PerformExportAsync(() => _gridContext.ExportToExcelXmlAsync(filename));
        }

        private async Task PerformExportAsync(Func<Task> exportAction)
        {
            IsBusy = true;
            StatusText = "Exporting...";
            try
            {
                await exportAction();
                _dialogService.ShowMessage("Export successful", "Export");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "Export Error");
                _logger.LogError("Export failed", ex);
            }
            finally
            {
                IsBusy = false;
                StatusText = $"Found {TotalRecords} records";
            }
        }
    }
}
