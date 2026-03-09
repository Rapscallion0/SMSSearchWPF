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
    public partial class ResultsViewModel : ObservableObject, IDisposable
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

            IsHighlightEnabled = _configService.GetValue("GENERAL", "HIGHLIGHT_MATCHES") == "1";
            UpdateFilterNavigationVisibility();
            UpdateHighlightColor(_configService.GetValue("GENERAL", "HIGHLIGHT_COLOR"));

            if (int.TryParse(_configService.GetValue("GENERAL", "HORIZONTAL_SCROLL_SPEED"), out int speed))
            {
                _horizontalScrollSpeed = speed;
            }

            string fctFields = _configService.GetValue("QUERY", "FUNCTION") ?? "";
            string tlzFields = _configService.GetValue("QUERY", "TOTALIZER") ?? "";
            _queryBuilder = new QueryBuilder(fctFields, tlzFields);

            _gridContext.DataReady += OnDataReady;
            _gridContext.LoadError += OnLoadError;

            ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
            ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync);
            ExportXmlCommand = new AsyncRelayCommand(ExportXmlAsync);

            ApplyFilterCommand = new AsyncRelayCommand<string>(ApplyFilterAsync);
            FindNextCommand = new AsyncRelayCommand(() => FindMatchAsync(true));
            SortCommand = new AsyncRelayCommand<string>(SortAsync);
            FindPreviousCommand = new AsyncRelayCommand(() => FindMatchAsync(false));
            CancelCommand = new RelayCommand(Cancel);

            ToggleHeaderDescriptionCommand = new RelayCommand<string>(c => { _toggledColumnName = c; ShowDescriptionHeaders = !ShowDescriptionHeaders; });
            CopyInsertCommand = new RelayCommand<System.Collections.IList>(CopyAsSqlInsert);

            ExportSelectedCsvCommand = new AsyncRelayCommand<System.Collections.IList>(ExportSelectedCsvAsync);
            ExportSelectedJsonCommand = new AsyncRelayCommand<System.Collections.IList>(ExportSelectedJsonAsync);
            ExportSelectedXmlCommand = new AsyncRelayCommand<System.Collections.IList>(ExportSelectedXmlAsync);

            FilterBySelectionCommand = new RelayCommand<string>(FilterBySelection);
            CopyRowCommand = new RelayCommand<IList>(CopyRow);

            // Register for message
            WeakReferenceMessenger.Default.Register<RowNumberVisibilityChangedMessage>(this, (r, m) =>
            {
                HeadersVisibility = m.Value ? DataGridHeadersVisibility.All : DataGridHeadersVisibility.Column;
            });

            WeakReferenceMessenger.Default.Register<HighlightConfigurationChangedMessage>(this, (r, m) =>
            {
                IsHighlightEnabled = m.Value.IsHighlightEnabled;
                UpdateHighlightColor(m.Value.HighlightColor);
                UpdateFilterNavigationVisibility();
                // Re-apply filter/highlight logic if there is text
                if (!string.IsNullOrEmpty(FilterText))
                {
                    ApplyFilterCommand.Execute(FilterText);
                }
            });

            WeakReferenceMessenger.Default.Register<HorizontalScrollSpeedChangedMessage>(this, (r, m) =>
            {
                _horizontalScrollSpeed = m.Speed;
                OnPropertyChanged(nameof(HorizontalScrollSpeed));
            });
        }

        public void Dispose()
        {
            Cancel();
            _gridContext.DataReady -= OnDataReady;
            _gridContext.LoadError -= OnLoadError;
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _cts?.Dispose();
            _refreshCts?.Dispose();
        }

        public int HorizontalScrollSpeed => _horizontalScrollSpeed;
        private int _horizontalScrollSpeed = 16;

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

        [ObservableProperty]
        private bool _isFilterVisible;

        [ObservableProperty]
        private bool _isHeaderToggleEnabled = true;

        [ObservableProperty]
        private string _headerToggleText = "Show Description";

        private string? _toggledColumnName;

        public SearchCriteria? CurrentSearchCriteria { get; private set; }

        public event EventHandler<(int RowIndex, string ColumnName)>? ScrollToCellRequested;
        public event EventHandler<string?>? HeadersUpdated;

        public IAsyncRelayCommand<string> ApplyFilterCommand { get; }
        public IAsyncRelayCommand FindNextCommand { get; }
        public IAsyncRelayCommand<string> SortCommand { get; }
        public IAsyncRelayCommand FindPreviousCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public IAsyncRelayCommand ExportCsvCommand { get; }
        public IAsyncRelayCommand ExportJsonCommand { get; }
        public IAsyncRelayCommand ExportXmlCommand { get; }

        public IRelayCommand<string> ToggleHeaderDescriptionCommand { get; }
        public IRelayCommand<System.Collections.IList> CopyInsertCommand { get; }

        public IAsyncRelayCommand<System.Collections.IList> ExportSelectedCsvCommand { get; }
        public IAsyncRelayCommand<System.Collections.IList> ExportSelectedJsonCommand { get; }
        public IAsyncRelayCommand<System.Collections.IList> ExportSelectedXmlCommand { get; }

        public IRelayCommand<string> FilterBySelectionCommand { get; }
        public IRelayCommand<IList> CopyRowCommand { get; }

        // Dictionary mapping Column Name -> Header Text (Description or Name)
        public Dictionary<string, string> ColumnHeaders { get; private set; } = new Dictionary<string, string>();

        public HashSet<string> HiddenColumns { get; set; } = new HashSet<string>();

        private DataTable? _lastSchema;

        private void Cancel()
        {
            _cts?.Cancel();
            _refreshCts?.Cancel();
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
             HeaderToggleText = value ? "Show Field #" : "Show Description";
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

            HeadersUpdated?.Invoke(this, _toggledColumnName);
            _toggledColumnName = null;
        }

        private CancellationTokenSource? _refreshCts;

        private void OnDataReady(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                _refreshCts?.Cancel();
                _refreshCts = new CancellationTokenSource();
                var token = _refreshCts.Token;

                try
                {
                    await Task.Delay(50, token);
                    if (token.IsCancellationRequested) return;

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
                }
                catch (OperationCanceledException)
                {
                    // Ignore
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error updating UI after data load", ex);
                    StatusText = "Error updating view";
                }
            });
        }

        private void OnLoadError(object? sender, Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _logger.LogError("Data Load Error", ex);
                _dialogService.ShowError(ex.Message, "Data Load Error");
                StatusText = "Error loading data";
            });
        }

        public async Task ExecuteSearchAsync(SearchCriteria criteria)
        {
            CurrentSearchCriteria = criteria;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsBusy = true;
            StatusText = "Searching...";
            FilterText = string.Empty;
            MatchStatusText = string.Empty;
            _lastFoundRowIndex = -1;

            _logger.LogDebug($"ExecuteSearchAsync started. Criteria: {criteria.Value}");

            // Determine if Header Toggle should be enabled
            if (criteria.Mode == SearchMode.Field &&
                (criteria.Type == SearchType.Number ||
                 criteria.Type == SearchType.Description ||
                 (criteria.Type == SearchType.Table && criteria.ShowFields)))
            {
                IsHeaderToggleEnabled = false;
            }
            else
            {
                IsHeaderToggleEnabled = true;
            }

            // Set Table Name if applicable
            TableName = (criteria.Type == SearchType.Table && criteria.Value != null) ? criteria.Value : "ResultTable";

            try
            {
                var queryResult = _queryBuilder.Build(criteria);

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
                    decryptedPass = !string.IsNullOrEmpty(pass) ? SMS_Search.Utils.GeneralUtils.Decrypt(pass) : null;
                }

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
        public async Task SortAsync(string? columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                IsBusy = true;
                await _gridContext.ApplySortAsync(columnName, token);

                if (_lastSchema != null)
                {
                    SearchResults = new VirtualizingCollection(_gridContext, _lastSchema);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo("Sort cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to apply sort", ex);
                var dialogService = (System.Windows.Application.Current as App)?.Services.GetService(typeof(IDialogService)) as IDialogService;
                dialogService?.ShowError($"Failed to sort: {ex.Message}", "Error");
            }
            finally
            {
                IsBusy = false;
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

                 // Explicitly update collection to ensure DataGrid resets correctly
                 // This avoids "index -1" errors when count changes significantly
                 if (_lastSchema != null)
                 {
                     SearchResults = new VirtualizingCollection(_gridContext, _lastSchema);
                 }

                 if (!string.IsNullOrEmpty(filterText))
                 {
                     long matchedCells = await _gridContext.GetTotalMatchedCellsCountAsync(filterText, columns, token);
                     MatchStatusText = $"Found: {TotalRecords} rows ({matchedCells} matches)";
                     _logger.LogDebug($"Filter applied. Rows: {TotalRecords}, Matches: {matchedCells}");
                 }
                 else
                 {
                     MatchStatusText = string.Empty;
                     _logger.LogDebug("Filter cleared.");
                 }

                 UpdateFilterNavigationVisibility();
                 _lastFoundRowIndex = -1;
                 _currentColumnIndex = -1;
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

        [ObservableProperty]
        private object? _selectedRow;

        partial void OnSelectedRowChanged(object? value)
        {
            if (value is VirtualRow vRow)
            {
                SetCurrentRowIndex(vRow.RowIndex);
            }
        }

        partial void OnTotalRecordsChanged(int value)
        {
            IsFilterVisible = value > 0;
        }

        private int _lastFoundRowIndex = -1;
        private int _currentColumnIndex = -1;

        public void SetCurrentRowIndex(int index)
        {
            _lastFoundRowIndex = index;
        }

        public void SetCurrentCell(int rowIndex, string? columnName)
        {
            _lastFoundRowIndex = rowIndex;
            if (_lastSchema != null && !string.IsNullOrEmpty(columnName) && _lastSchema.Columns.Contains(columnName))
            {
                _currentColumnIndex = _lastSchema.Columns[columnName]!.Ordinal;
            }
        }

        private void UpdateFilterNavigationVisibility()
        {
            IsFilterNavigationVisible = IsHighlightEnabled && !string.IsNullOrEmpty(FilterText);
        }

        private async Task FindMatchAsync(bool forward)
        {
            if (IsBusy) return;
            if (string.IsNullOrEmpty(FilterText)) return;
            if (TotalRecords == 0) return;
            if (_lastSchema == null) return;

            int startRow = _lastFoundRowIndex;
            int startCol = _currentColumnIndex;

            int rowCount = TotalRecords;
            int colCount = _lastSchema.Columns.Count;

            if (startRow < 0) startRow = 0;
            if (startCol < 0) startCol = forward ? -1 : colCount;

            if (forward)
            {
                // Current Row
                for (int c = startCol + 1; c < colCount; c++)
                {
                    if (await CheckMatch(startRow, c)) { await FoundMatchAsync(startRow, c); return; }
                }

                // Next Rows
                for (int offset = 1; offset < rowCount; offset++)
                {
                    int r = (startRow + offset) % rowCount;
                    await _gridContext.WaitForRowAsync(r);
                    for (int c = 0; c < colCount; c++)
                    {
                        if (await CheckMatch(r, c)) { await FoundMatchAsync(r, c); return; }
                    }
                }

                // Wrap around to start of startRow
                for (int c = 0; c <= startCol; c++)
                {
                    if (await CheckMatch(startRow, c)) { await FoundMatchAsync(startRow, c); return; }
                }
            }
            else
            {
                // Current Row
                for (int c = startCol - 1; c >= 0; c--)
                {
                    if (await CheckMatch(startRow, c)) { await FoundMatchAsync(startRow, c); return; }
                }

                // Previous Rows
                for (int offset = 1; offset < rowCount; offset++)
                {
                    int r = startRow - offset;
                    if (r < 0) r += rowCount;

                    await _gridContext.WaitForRowAsync(r);

                    for (int c = colCount - 1; c >= 0; c--)
                    {
                        if (await CheckMatch(r, c)) { await FoundMatchAsync(r, c); return; }
                    }
                }

                // Wrap around to end of startRow
                for (int c = colCount - 1; c >= startCol; c--)
                {
                    if (await CheckMatch(startRow, c)) { await FoundMatchAsync(startRow, c); return; }
                }
            }
        }

        private async Task<bool> CheckMatch(int row, int col)
        {
            var val = _gridContext.GetValue(row, col);
            if (val == null)
            {
                await _gridContext.WaitForRowAsync(row);
                val = _gridContext.GetValue(row, col);
            }
            string sVal = val?.ToString() ?? "";
            return sVal.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task FoundMatchAsync(int row, int col)
        {
            _lastFoundRowIndex = row;
            _currentColumnIndex = col;
            if (_lastSchema != null)
            {
                string colName = _lastSchema.Columns[col].ColumnName;
                ScrollToCellRequested?.Invoke(this, (row, colName));
            }

            long totalMatches = 0;
            if (SearchResults is VirtualizingCollection vc)
            {
                var columns = new List<string>();
                var props = vc.GetItemProperties(null);
                foreach(PropertyDescriptor p in props) columns.Add(p.Name);
                totalMatches = await _gridContext.GetTotalMatchedCellsCountAsync(FilterText, columns, CancellationToken.None);
            }

            long currentMatchIndex = 0;
            if (_lastSchema != null && SearchResults is VirtualizingCollection vc2)
            {
                var columns = new List<string>();
                var props = vc2.GetItemProperties(null);
                foreach(PropertyDescriptor p in props) columns.Add(p.Name);

                long precedingMatches = await _gridContext.GetPrecedingMatchedCellsCountAsync(row, CancellationToken.None);

                long matchesInCurrentRow = 0;
                for (int c = 0; c <= col; c++)
                {
                     if (await CheckMatch(row, c)) matchesInCurrentRow++;
                }

                currentMatchIndex = precedingMatches + matchesInCurrentRow;
            }

            MatchStatusText = $"Match {currentMatchIndex} of {totalMatches}";
        }

        private async Task ExportCsvAsync()
        {
            string? filename = _dialogService.SaveFileDialog("CSV files (*.csv)|*.csv", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (string.IsNullOrEmpty(filename)) return;
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(() => _gridContext.ExportToCsvAsync(filename, null, true, hidden), filename, "CSV");
        }

        private async Task ExportJsonAsync()
        {
            string? filename = _dialogService.SaveFileDialog("JSON files (*.json)|*.json", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            if (string.IsNullOrEmpty(filename)) return;
            if (CurrentSearchCriteria == null) return;
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(() => _gridContext.ExportToJsonAsync(filename, CurrentSearchCriteria, null, hidden), filename, "JSON");
        }

        private async Task ExportXmlAsync()
        {
            string? filename = _dialogService.SaveFileDialog("XML files (*.xml)|*.xml", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            if (string.IsNullOrEmpty(filename)) return;
            if (CurrentSearchCriteria == null) return;
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(() => _gridContext.ExportToXmlAsync(filename, CurrentSearchCriteria, hidden), filename, "XML");
        }

        private HashSet<string>? CheckHiddenColumns()
        {
            if (HiddenColumns.Count > 0)
            {
                bool includeHidden = _dialogService.ShowConfirmation("There are hidden columns. Would you like to include the hidden columns in the export?", "Export Hidden Columns");
                if (!includeHidden)
                {
                    return HiddenColumns;
                }
            }
            return null;
        }

        private async Task PerformExportAsync(Func<Task<int>> exportAction, string exportedFilePath, string format)
        {
            IsBusy = true;
            StatusText = "Exporting...";
            _logger.LogInfo($"Starting {format} export to {exportedFilePath}...");
            try
            {
                int rowCount = await exportAction();
                _dialogService.ShowToast($"Successfully exported {rowCount} records to {format}", "Export", SMS_Search.Views.ToastType.Success, null, exportedFilePath);
                _logger.LogInfo($"{format} export successful. {rowCount} rows exported.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "Export Error");
                _logger.LogError($"{format} export failed", ex);
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

        private async Task ExportSelectedCsvAsync(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0 || _lastSchema == null) return;
            string? filename = _dialogService.SaveFileDialog("CSV files (*.csv)|*.csv", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (string.IsNullOrEmpty(filename)) return;

            var rows = selectedItems.Cast<VirtualRow>().ToList();
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(() => _gridContext.ExportRowsToCsvAsync(filename, rows, hidden), filename, "CSV");
        }

        private async Task ExportSelectedJsonAsync(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0 || _lastSchema == null || CurrentSearchCriteria == null) return;
            string? filename = _dialogService.SaveFileDialog("JSON files (*.json)|*.json", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            if (string.IsNullOrEmpty(filename)) return;

            var rows = selectedItems.Cast<VirtualRow>().ToList();
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(() => _gridContext.ExportRowsToJsonAsync(filename, rows, CurrentSearchCriteria, hidden), filename, "JSON");
        }

        private async Task ExportSelectedXmlAsync(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0 || _lastSchema == null || CurrentSearchCriteria == null) return;
            string? filename = _dialogService.SaveFileDialog("XML files (*.xml)|*.xml", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            if (string.IsNullOrEmpty(filename)) return;

            var rows = selectedItems.Cast<VirtualRow>().ToList();
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(() => _gridContext.ExportRowsToXmlAsync(filename, rows, CurrentSearchCriteria, hidden), filename, "XML");
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
