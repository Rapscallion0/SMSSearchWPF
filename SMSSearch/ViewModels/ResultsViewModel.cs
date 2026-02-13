using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace SMS_Search.ViewModels
{
    public partial class ResultsViewModel : ObservableObject
    {
        private readonly VirtualGridContext _gridContext;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly QueryBuilder _queryBuilder;
        private readonly IConfigService _configService;
        private readonly IClipboardService _clipboardService;
        private CancellationTokenSource? _cts;

        public ResultsViewModel(
            VirtualGridContext gridContext,
            ILoggerService logger,
            IDialogService dialogService,
            IConfigService configService,
            IClipboardService clipboardService)
        {
            _gridContext = gridContext;
            _logger = logger;
            _dialogService = dialogService;
            _configService = configService;
            _clipboardService = clipboardService;

            // Initialize HeadersVisibility based on config
            bool showRowNumbers = _configService.GetValue("GENERAL", "SHOW_ROW_NUMBERS") == "1";
            HeadersVisibility = showRowNumbers ? DataGridHeadersVisibility.All : DataGridHeadersVisibility.Column;

            // Register for message
            WeakReferenceMessenger.Default.Register<RowNumberVisibilityChangedMessage>(this, (r, m) =>
            {
                HeadersVisibility = m.Value ? DataGridHeadersVisibility.All : DataGridHeadersVisibility.Column;
            });

            WeakReferenceMessenger.Default.Register<HighlightConfigurationChangedMessage>(this, (r, m) =>
            {
                IsHighlightEnabled = m.Value.IsHighlightEnabled;
                UpdateHighlightColor(m.Value.HighlightColor);
                IsFilterNavigationVisible = IsHighlightEnabled;
                // Re-apply filter/highlight logic if there is text
                if (!string.IsNullOrEmpty(FilterText))
                {
                    ApplyFilterCommand.Execute(FilterText);
                }
            });

            IsHighlightEnabled = _configService.GetValue("GENERAL", "HIGHLIGHT_MATCHES") == "1";
            IsFilterNavigationVisible = IsHighlightEnabled;
            UpdateHighlightColor(_configService.GetValue("GENERAL", "HIGHLIGHT_COLOR"));

            string fctFields = _configService.GetValue("QUERY", "FUNCTION") ?? "";
            string tlzFields = _configService.GetValue("QUERY", "TOTALIZER") ?? "";
            _queryBuilder = new QueryBuilder(fctFields, tlzFields);

            _gridContext.DataReady += OnDataReady;
            _gridContext.LoadError += OnLoadError;

            ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
            ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync);
            ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync);

            ApplyFilterCommand = new AsyncRelayCommand<string>(ApplyFilterAsync);
            FindNextCommand = new AsyncRelayCommand(() => FindMatchAsync(true));
            FindPreviousCommand = new AsyncRelayCommand(() => FindMatchAsync(false));
            CancelCommand = new RelayCommand(Cancel);

            ToggleHeaderDescriptionCommand = new RelayCommand(() => ShowDescriptionHeaders = !ShowDescriptionHeaders);
            CopyInsertCommand = new RelayCommand<System.Collections.IList>(CopyAsSqlInsert);
            ExportSelectedCsvCommand = new RelayCommand<System.Collections.IList>(_ => { });
            FilterBySelectionCommand = new RelayCommand<string>(FilterBySelection);
            CopyRowCommand = new RelayCommand<IList>(CopyRow);
        }

        [ObservableProperty]
        private IList? _searchResults;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusText = "";

        [ObservableProperty]
        private int _totalRecords;

        [ObservableProperty]
        private string _filterText = "";

        [ObservableProperty]
        private string _matchStatusText = "";

        [ObservableProperty]
        private string _tableName = "";

        [ObservableProperty]
        private bool _showDescriptionHeaders;

        [ObservableProperty]
        private DataGridHeadersVisibility _headersVisibility = DataGridHeadersVisibility.Column;

        [ObservableProperty]
        private bool _isHighlightEnabled;

        [ObservableProperty]
        private System.Windows.Media.Brush? _highlightColor;

        [ObservableProperty]
        private bool _isFilterNavigationVisible;

        public event EventHandler<int>? ScrollToRowRequested;
        public event EventHandler? HeadersUpdated;

        public IAsyncRelayCommand<string> ApplyFilterCommand { get; }
        public IAsyncRelayCommand FindNextCommand { get; }
        public IAsyncRelayCommand FindPreviousCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public IAsyncRelayCommand ExportCsvCommand { get; }
        public IAsyncRelayCommand ExportJsonCommand { get; }
        public IAsyncRelayCommand ExportExcelCommand { get; }

        public IRelayCommand ToggleHeaderDescriptionCommand { get; }
        public IRelayCommand<System.Collections.IList> CopyInsertCommand { get; }
        public IRelayCommand<System.Collections.IList> ExportSelectedCsvCommand { get; }
        public IRelayCommand<string> FilterBySelectionCommand { get; }
        public IRelayCommand<IList> CopyRowCommand { get; }

        // Dictionary mapping Column Name -> Header Text (Description or Name)
        public Dictionary<string, string> ColumnHeaders { get; private set; } = new Dictionary<string, string>();

        private DataTable? _lastSchema;

        private void Cancel()
        {
            _cts?.Cancel();
        }

        private void UpdateHighlightColor(string? colorString)
        {
            try
            {
                if (string.IsNullOrEmpty(colorString)) colorString = "#FFFFE0";
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
                HighlightColor = new System.Windows.Media.SolidColorBrush(color);
                HighlightColor.Freeze();
            }
            catch
            {
                 HighlightColor = System.Windows.Media.Brushes.LightYellow;
            }
        }

        partial void OnShowDescriptionHeadersChanged(bool value)
        {
             UpdateHeadersAsync();
        }

        private async void UpdateHeadersAsync()
        {
            if (_lastSchema == null) return;

            ColumnHeaders.Clear();
            List<string> colNames = new List<string>();
            foreach(DataColumn c in _lastSchema.Columns) colNames.Add(c.ColumnName);

            if (ShowDescriptionHeaders)
            {
                 try
                 {
                     var descriptions = await _gridContext.GetColumnDescriptionsAsync(colNames);
                     for (int i = 0; i < colNames.Count; i++)
                     {
                         string desc = (i < descriptions.Count) ? descriptions[i] : "";
                         ColumnHeaders[colNames[i]] = !string.IsNullOrEmpty(desc) ? desc : colNames[i];
                     }
                 }
                 catch (Exception ex)
                 {
                     _logger.LogError("Failed to load descriptions", ex);
                     // Fallback
                     foreach(var name in colNames) ColumnHeaders[name] = name;
                 }
            }
            else
            {
                 foreach(var name in colNames) ColumnHeaders[name] = name;
            }

            HeadersUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnDataReady(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (SearchResults is VirtualizingCollection vc)
                {
                    vc.Refresh();
                    TotalRecords = _gridContext.TotalCount;
                    if (!string.IsNullOrEmpty(_gridContext.FilterText))
                    {
                        StatusText = $"Found {TotalRecords} records (Filtered from {_gridContext.UnfilteredCount})";
                    }
                    else
                    {
                        StatusText = $"Found {TotalRecords} records";
                    }
                }
            });
        }

        private void OnLoadError(object? sender, string msg)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _dialogService.ShowError(msg, "Data Load Error");
                StatusText = "Error loading data";
            });
        }

        public async Task ExecuteSearchAsync(SearchCriteria criteria)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsBusy = true;
            StatusText = "Searching...";
            FilterText = string.Empty;
            MatchStatusText = string.Empty;
            _lastFoundRowIndex = -1;

            _logger.LogDebug($"ExecuteSearchAsync started. Criteria: {criteria.Value}");

            // Set Table Name if applicable
            TableName = (criteria.Type == SearchType.Table && criteria.Value != null) ? criteria.Value : "ResultTable";

            try
            {
                var queryResult = _queryBuilder.Build(criteria);

                var server = _configService.GetValue("CONNECTION", "SERVER") ?? "";
                var database = _configService.GetValue("CONNECTION", "DATABASE") ?? "";
                var user = _configService.GetValue("CONNECTION", "SQLUSER") ?? "";
                var pass = _configService.GetValue("CONNECTION", "SQLPASSWORD");
                string? decryptedPass = !string.IsNullOrEmpty(pass) ? SMS_Search.Utils.GeneralUtils.Decrypt(pass) : null;

                _gridContext.SetConnection(server, database, user, decryptedPass);

                // Load initial count and cache schema in parallel
                var schemaTask = _gridContext.GetSchemaAsync(queryResult.Sql, queryResult.Parameters, token);
                var loadTask = _gridContext.LoadAsync(queryResult.Sql, queryResult.Parameters, null, token);

                await Task.WhenAll(schemaTask, loadTask);

                _lastSchema = await schemaTask;
                UpdateHeadersAsync(); // This runs async but doesn't block UI much

                // Create new collection
                SearchResults = new VirtualizingCollection(_gridContext, _lastSchema);

                TotalRecords = _gridContext.TotalCount;
                StatusText = $"Found {TotalRecords} records";
                _logger.LogInfo($"Search completed successfully. Found {TotalRecords} records.");
            }
            catch (OperationCanceledException)
            {
                StatusText = "Search cancelled";
                _logger.LogInfo("Search cancelled by user.");
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
                _cts?.Dispose();
                _cts = null;
            }
        }

        public async Task ApplyFilterAsync(string? filterText)
        {
             // Cancel previous search/filter
             _cts?.Cancel();
             _cts = new CancellationTokenSource();
             var token = _cts.Token;

             FilterText = filterText ?? "";

             _logger.LogDebug($"Applying filter: '{filterText}'");
             var columns = new List<string>();
             if (SearchResults is VirtualizingCollection vc)
             {
                 var props = vc.GetItemProperties(null);
                 foreach(PropertyDescriptor p in props) columns.Add(p.Name);
             }

             try
             {
                 IsBusy = true;

                 // Traditional Filter (Always Filter)
                 await _gridContext.ApplyFilterAsync(filterText ?? "", columns, token);
                 TotalRecords = _gridContext.TotalCount;

                 if (!string.IsNullOrEmpty(filterText))
                 {
                     MatchStatusText = $"Found: {TotalRecords} matches";
                     _logger.LogDebug($"Filter applied. Matches: {TotalRecords}");
                 }
                 else
                 {
                     MatchStatusText = string.Empty;
                     _logger.LogDebug("Filter cleared.");
                 }

                 _lastFoundRowIndex = -1;
             }
             catch (OperationCanceledException)
             {
                 // Ignore
             }
             catch (Exception ex)
             {
                 _logger.LogError("Filter failed", ex);
             }
             finally
             {
                 IsBusy = false;
                 _cts?.Dispose();
                 _cts = null;
             }
        }

        private int _lastFoundRowIndex = -1;

        public void SetCurrentRowIndex(int index)
        {
            _lastFoundRowIndex = index;
        }

        private async Task FindMatchAsync(bool forward)
        {
            if (IsBusy) return;
            if (string.IsNullOrEmpty(FilterText)) return;
            if (TotalRecords == 0) return;

            // Always just move to next visible row since we filter now
            int nextRow = _lastFoundRowIndex + (forward ? 1 : -1);

            // Wrap around
            if (nextRow >= TotalRecords) nextRow = 0;
            if (nextRow < 0) nextRow = TotalRecords - 1;

            _lastFoundRowIndex = nextRow;
            ScrollToRowRequested?.Invoke(this, nextRow);

            MatchStatusText = $"Match {nextRow + 1} of {TotalRecords}";

            // Wait, this method signature was async Task, but now it's synchronous logic.
            // We can keep signature async Task and just await Task.CompletedTask or nothing if awaited implicitly.
            // Or return Task.CompletedTask.
            await Task.CompletedTask;
        }

        private async Task ExportCsvAsync()
        {
            string? filename = _dialogService.SaveFileDialog("CSV files (*.csv)|*.csv", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (string.IsNullOrEmpty(filename)) return;
            await PerformExportAsync(() => _gridContext.ExportToCsvAsync(filename));
        }

        private async Task ExportJsonAsync()
        {
            string? filename = _dialogService.SaveFileDialog("JSON files (*.json)|*.json", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            if (string.IsNullOrEmpty(filename)) return;
            await PerformExportAsync(() => _gridContext.ExportToJsonAsync(filename));
        }

        private async Task ExportExcelAsync()
        {
            string? filename = _dialogService.SaveFileDialog("Excel XML (*.xml)|*.xml", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            if (string.IsNullOrEmpty(filename)) return;
            await PerformExportAsync(() => _gridContext.ExportToExcelXmlAsync(filename));
        }

        private async Task PerformExportAsync(Func<Task> exportAction)
        {
            IsBusy = true;
            StatusText = "Exporting...";
            _logger.LogInfo("Starting export...");
            try
            {
                await exportAction();
                _dialogService.ShowMessage("Export successful", "Export");
                _logger.LogInfo("Export successful.");
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

        private void FilterBySelection(string? text)
        {
            if (string.IsNullOrEmpty(text)) return;
            FilterText = text;
            ApplyFilterCommand.Execute(text);
        }

        private void CopyRow(IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;

            var firstItem = selectedItems[0];
            if (firstItem == null) return;

            var sb = new System.Text.StringBuilder();
            var props = TypeDescriptor.GetProperties(firstItem);
            foreach (var item in selectedItems)
            {
                var values = new List<string>();
                foreach (PropertyDescriptor prop in props)
                {
                    values.Add(prop.GetValue(item)?.ToString() ?? "");
                }
                sb.AppendLine(string.Join("\t", values));
            }
            try
            {
                _clipboardService.SetText(sb.ToString());
                _logger.LogInfo($"Copied {selectedItems.Count} rows to clipboard.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Copy Row failed", ex);
                _dialogService.ShowError("Failed to copy: " + ex.Message, "Copy Error");
            }
        }

        private void CopyAsSqlInsert(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0 || _lastSchema == null) return;

            string table = TableName;
            if (string.IsNullOrWhiteSpace(table) || table == "ResultTable") table = "[TableName]";
            if (!table.StartsWith("[") && !table.Contains(" ")) table = "[" + table + "]";

            var sb = new System.Text.StringBuilder();

            var cols = _lastSchema.Columns;
            var colNames = new List<string>();
            foreach(DataColumn c in cols) colNames.Add(c.ColumnName);

            sb.Append($"INSERT INTO {table} (");
            sb.Append(string.Join(", ", System.Linq.Enumerable.Select(colNames, c => $"[{c}]")));
            sb.AppendLine(") VALUES");

            for (int i = 0; i < selectedItems.Count; i++)
            {
                var item = selectedItems[i];
                if (item is VirtualRow row)
                {
                    sb.Append("(");
                    var vals = new List<string>();
                    for (int c = 0; c < colNames.Count; c++)
                    {
                        var val = row.GetValue(c);
                        vals.Add(FormatSqlValue(val));
                    }
                    sb.Append(string.Join(", ", vals));
                    sb.Append(")");
                    if (i < selectedItems.Count - 1) sb.AppendLine(","); else sb.AppendLine(";");
                }
            }

            _clipboardService.SetText(sb.ToString());
            _dialogService.ShowMessage("INSERT statements copied to clipboard", "Copy");
            _logger.LogInfo($"Copied SQL INSERT statements for {selectedItems.Count} rows.");
        }

        private string FormatSqlValue(object? value)
        {
            if (value == null || value == DBNull.Value) return "NULL";
            if (value is bool b) return b ? "1" : "0";
            if (IsNumeric(value)) return value.ToString() ?? "NULL";
            if (value is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";
            return $"'{value.ToString()?.Replace("'", "''") ?? ""}'";
        }

        private bool IsNumeric(object? value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }
    }
}
