using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Data;
using SMS_Search.Models;
using SMS_Search.Services;
using SMS_Search.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SMS_Search.ViewModels
{
    public partial class ResultsViewModel : ObservableObject, IDisposable
    {
        private readonly VirtualGridContext _gridContext;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IConfigService _configService;
        private readonly IClipboardService _clipboardService;
        private readonly IConnectionSettingsService _connectionSettingsService;
        private readonly IDataRepository _dataRepository;
        private readonly IFieldDictionaryService _fieldDictionaryService;
        private bool _isSyncingDictionaryMode;
        private CancellationTokenSource? _cts;

        public ResultsViewModel(
            VirtualGridContext gridContext,
            ILoggerService logger,
            IDialogService dialogService,
            IConfigService configService,
            IClipboardService clipboardService,
            IConnectionSettingsService connectionSettingsService,
            IDataRepository dataRepository,
            IFieldDictionaryService fieldDictionaryService)
        {
            _gridContext = gridContext;
            _logger = logger;
            _dialogService = dialogService;
            _configService = configService;
            _clipboardService = clipboardService;
            _connectionSettingsService = connectionSettingsService;
            _dataRepository = dataRepository;
            _fieldDictionaryService = fieldDictionaryService;

            // Initialize HeadersVisibility based on config
            bool showRowNumbers = _configService.GetValue(AppSettings.Sections.Results, AppSettings.Keys.ShowRowNumbers) == "1";
            HeadersVisibility = showRowNumbers ? DataGridHeadersVisibility.All : DataGridHeadersVisibility.Column;

            IsHighlightEnabled = _configService.GetValue(AppSettings.Sections.Results, AppSettings.Keys.HighlightMatches) == "1";
            UpdateFilterNavigationVisibility();
            UpdateHighlightColor(_configService.GetValue(AppSettings.Sections.Results, AppSettings.Keys.HighlightColor));

            if (int.TryParse(_configService.GetValue(AppSettings.Sections.Results, AppSettings.Keys.HorizontalScrollSpeed), out int speed))
            {
                _horizontalScrollSpeed = speed;
            }

            _gridContext.DataReady += OnDataReady;
            _gridContext.PageLoaded += OnPageLoaded;
            _gridContext.LoadError += OnLoadError;
            _fieldDictionaryService.DictionaryChanged += OnDictionaryChanged;
            SyncDictionaryState();

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
            CopyAsMarkdownCommand = new RelayCommand<IList>(CopyAsMarkdown);
            CopyAllMatchesCommand = new AsyncRelayCommand(CopyAllMatchesAsync, CanCopyAllMatches);
            ToggleInspectorCommand = new RelayCommand(() => WeakReferenceMessenger.Default.Send(new ShowPanelMessage("RowInspector")));
            CopyInspectorValueCommand = new RelayCommand<RowInspectorField>(CopyInspectorValue);
            CopyInspectorValuesCommand = new RelayCommand<IList>(CopyInspectorValues);
            CopyInspectorColumnsCommand = new RelayCommand<IList>(CopyInspectorColumns);
            CopyInspectorDescriptionsCommand = new RelayCommand<IList>(CopyInspectorDescriptions);
            CopyInspectorRowsCommand = new RelayCommand<IList>(CopyInspectorRows);
            FindFieldUsageCommand = new RelayCommand<string>(FindFieldUsage);
            CopySelectionStatsCommand = new RelayCommand(CopySelectionStats);

            InspectorFieldsView = CollectionViewSource.GetDefaultView(InspectorFields);
            InspectorFieldsView.Filter = FilterInspectorField;
            BindingOperations.EnableCollectionSynchronization(InspectorFields, _inspectorFieldsLock);

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
            _gridContext.PageLoaded -= OnPageLoaded;
            _gridContext.LoadError -= OnLoadError;
            _fieldDictionaryService.DictionaryChanged -= OnDictionaryChanged;
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _cts?.Dispose();
            _cts = null;
            _refreshCts?.Dispose();
            _refreshCts = null;
            (_gridContext as IDisposable)?.Dispose();
        }

        public IEnumerable<DictionarySourceMode> DictionarySourceModes => Enum.GetValues<DictionarySourceMode>();

        [ObservableProperty]
        private DictionarySourceMode _selectedDictionarySource;

        [ObservableProperty]
        private string _dictionaryStatusTooltip = string.Empty;

        partial void OnSelectedDictionarySourceChanged(DictionarySourceMode value)
        {
            if (_isSyncingDictionaryMode) return;
            _fieldDictionaryService.SetSourceMode(value);
            SyncDictionaryState();
            if (_lastSchema != null)
            {
                _ = UpdateHeadersAsync();
            }
        }

        private void OnDictionaryChanged(object? sender, EventArgs e)
        {
            SyncDictionaryState();
            if (_lastSchema != null)
            {
                _ = UpdateHeadersAsync();
            }
        }

        private void SyncDictionaryState()
        {
            _isSyncingDictionaryMode = true;
            try
            {
                SelectedDictionarySource = _fieldDictionaryService.GetSourceMode();
                var status = _fieldDictionaryService.GetStatus();
                if (status.IsCsvActive)
                {
                    if (!string.IsNullOrEmpty(status.ErrorMessage))
                    {
                        DictionaryStatusTooltip = status.ErrorMessage;
                    }
                    else
                    {
                        string pathInfo = status.IsAutoDetected
                            ? $"Auto-detected: {status.ResolvedFieldsCsvPath}"
                            : $"Path: {status.ResolvedFieldsCsvPath}";
                        DictionaryStatusTooltip = $"{pathInfo} ({status.FieldCount} fields, Fields2: {(status.HasFields2Csv ? "Yes" : "No")}, Fields.es: {(status.HasFieldsEs ? "Yes" : "No")})";
                    }
                }
                else
                {
                    DictionaryStatusTooltip = "Using database RB_FIELDS / RB_TABLES";
                }
            }
            finally
            {
                _isSyncingDictionaryMode = false;
            }
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
        [NotifyCanExecuteChangedFor(nameof(CopyAllMatchesCommand))]
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

        [ObservableProperty]
        private string? _sortColumn;

        [ObservableProperty]
        private ListSortDirection? _sortDirection;

        private string? _toggledColumnName;

        public SearchCriteria? CurrentSearchCriteria { get; private set; }
        public List<string> ColumnOrder { get; } = new();

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
        public IRelayCommand<IList> CopyAsMarkdownCommand { get; }
        public IAsyncRelayCommand CopyAllMatchesCommand { get; }
        public IRelayCommand ToggleInspectorCommand { get; }
        public IRelayCommand<RowInspectorField> CopyInspectorValueCommand { get; }
        public IRelayCommand<IList> CopyInspectorValuesCommand { get; }
        public IRelayCommand<IList> CopyInspectorColumnsCommand { get; }
        public IRelayCommand<IList> CopyInspectorDescriptionsCommand { get; }
        public IRelayCommand<IList> CopyInspectorRowsCommand { get; }
        public IRelayCommand<string> FindFieldUsageCommand { get; }
        public IRelayCommand CopySelectionStatsCommand { get; }

        [ObservableProperty]
        private bool _isInspectorOpen;

        [ObservableProperty]
        private string _inspectorFilterText = string.Empty;

        [ObservableProperty]
        private string _selectionStatsText = string.Empty;

        public ObservableCollection<RowInspectorField> InspectorFields { get; } = new();
        public ICollectionView InspectorFieldsView { get; }
        private readonly object _inspectorFieldsLock = new();

        // Dictionary mapping Column Name -> Header Text (Description or Name)
        public Dictionary<string, string> ColumnHeaders { get; private set; } = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _columnDescriptions = new(StringComparer.OrdinalIgnoreCase);
        private string _lastElapsedText = string.Empty;

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
             _ = UpdateHeadersAsync();
        }

        private async Task UpdateHeadersAsync()
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
                     _columnDescriptions.Clear();
                     for (int i = 0; i < colNames.Count; i++)
                     {
                         string desc = (i < descriptions.Count) ? descriptions[i].Trim() : "";
                         _columnDescriptions[colNames[i]] = desc;
                         ColumnHeaders[colNames[i]] = !string.IsNullOrWhiteSpace(desc) ? desc : colNames[i];
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

            if (IsInspectorOpen)
            {
                await EnsureColumnDescriptionsLoadedAsync();
                UpdateInspectorFields(SelectedRow as VirtualRow);
            }

            HeadersUpdated?.Invoke(this, _toggledColumnName);
            _toggledColumnName = null;
        }

        private CancellationTokenSource? _refreshCts;

        private void OnPageLoaded(object? sender, PageLoadedEventArgs e)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                if (SearchResults is VirtualizingCollection vc)
                {
                    vc.NotifyRangeUpdated(e.StartIndex, e.Count);
                }
                return;
            }

            dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (SearchResults is VirtualizingCollection vc)
                    {
                        vc.NotifyRangeUpdated(e.StartIndex, e.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error updating UI after page load", ex);
                }
            });
        }

        private void OnDataReady(object? sender, EventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                _refreshCts?.Cancel();
                _refreshCts?.Dispose();
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
                        StatusText = BuildFoundStatusText(TotalRecords, _gridContext.FilterText, _gridContext.UnfilteredCount, _lastElapsedText);
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
            System.Windows.Application.Current?.Dispatcher.BeginInvoke((Action)(() =>
            {
                _logger.LogError("Data Load Error", ex);
                _dialogService.ShowError(ex.Message, "Data Load Error");
                StatusText = "Error loading data";
            }));
        }

        public async Task ExecuteSearchAsync(SearchCriteria criteria)
        {
            CurrentSearchCriteria = criteria;
            ColumnOrder.Clear();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsBusy = true;
            StatusText = "Searching...";
            FilterText = string.Empty;
            MatchStatusText = string.Empty;
            SelectionStatsText = string.Empty;
            _lastElapsedText = string.Empty;
            _columnDescriptions.Clear();
            _inspectedRow = null;
            InspectorFields.Clear();
            _lastFoundRowIndex = -1;
            var sw = Stopwatch.StartNew();

            _logger.LogDebug("ExecuteSearchAsync started. Criteria: {CriteriaValue}", criteria.Value);

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
                var conn = _connectionSettingsService.GetConnectionSettings();
                _gridContext.SetConnection(conn.Server, conn.Database, conn.User, conn.Password);

                string fctFields = _configService.GetValue(AppSettings.Sections.Query, AppSettings.Keys.Function) ?? "";
                string tlzFields = _configService.GetValue(AppSettings.Sections.Query, AppSettings.Keys.Totalizer) ?? "";
                var queryBuilder = new QueryBuilder(fctFields, tlzFields);

                if (criteria.Mode == SearchMode.Field &&
                    criteria.Type != SearchType.CustomSql &&
                    !(criteria.Type == SearchType.Table && !criteria.ShowFields) &&
                    _fieldDictionaryService.IsCsvActive())
                {
                    await PerformCsvFieldSearchAsync(criteria, conn, queryBuilder, sw, token);
                    return;
                }

                if (string.IsNullOrWhiteSpace(conn.Server) || string.IsNullOrWhiteSpace(conn.Database))
                {
                    StatusText = "Connection required";
                    _dialogService.ShowWarning(
                        "A SQL Server connection is required for this search mode. Please configure Server and Database in Connection settings.",
                        "Connection Required");
                    return;
                }

                bool hasRbFields = true;
                if (criteria.Mode == SearchMode.Field && criteria.Type != SearchType.CustomSql)
                {
                    hasRbFields = await _gridContext.TableExistsAsync("RB_FIELDS", token);
                }

                var queryResult = queryBuilder.Build(criteria, hasRbFields);

                // Load initial count and cache schema in parallel
                var schemaTask = _gridContext.GetSchemaAsync(queryResult.Sql, queryResult.Parameters, token);
                var loadTask = _gridContext.LoadAsync(queryResult.Sql, queryResult.Parameters, null, token);

                await Task.WhenAll(schemaTask, loadTask);

                _lastSchema = await schemaTask;
                _ = UpdateHeadersAsync(); // This runs async but doesn't block UI much

                SyncSortStateFromContext();

                // Create new collection
                SearchResults = new VirtualizingCollection(_gridContext, _lastSchema);

                sw.Stop();
                _lastElapsedText = FormatElapsed(sw.Elapsed);
                TotalRecords = _gridContext.TotalCount;
                StatusText = BuildFoundStatusText(TotalRecords, null, 0, _lastElapsedText);
                _logger.LogInfo("Search completed successfully. Found {RecordCount} records in {Elapsed}.", TotalRecords, _lastElapsedText);
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
        private async Task PerformCsvFieldSearchAsync(
            SearchCriteria criteria,
            ConnectionSettingsInfo conn,
            QueryBuilder queryBuilder,
            Stopwatch sw,
            CancellationToken token)
        {
            var status = _fieldDictionaryService.GetStatus();
            if (!status.IsCsvAvailable && !string.IsNullOrEmpty(status.ErrorMessage))
            {
                _dialogService.ShowWarning(status.ErrorMessage, "Database Dictionary");
            }

            DataTable resultTable;
            bool isOffline = false;

            if (!string.IsNullOrWhiteSpace(conn.Server) && !string.IsNullOrWhiteSpace(conn.Database))
            {
                try
                {
                    bool hasCompound = criteria.RootGroup != null && criteria.RootGroup.Children.Count > 0;
                    var sqlCriteria = (criteria.Type == SearchType.Description || hasCompound)
                        ? new SearchCriteria
                        {
                            Mode = SearchMode.Field,
                            Type = SearchType.Number,
                            Value = string.Empty,
                            ShowFields = criteria.ShowFields,
                            AnyMatch = criteria.AnyMatch
                        }
                        : criteria;

                    var queryResult = queryBuilder.Build(sqlCriteria, hasRbFields: false);
                    var sqlDt = await _dataRepository.ExecuteQueryAsync(
                        conn.Server, conn.Database, conn.User, conn.Password,
                        queryResult.Sql, queryResult.Parameters, token);

                    resultTable = _fieldDictionaryService.EnrichSqlMetadata(sqlDt, criteria);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SQL metadata query failed ({Message}); falling back to offline Fields.csv search.", ex.Message);
                    resultTable = _fieldDictionaryService.SearchOffline(criteria);
                    isOffline = true;
                }
            }
            else
            {
                resultTable = _fieldDictionaryService.SearchOffline(criteria);
                isOffline = true;
            }

            _gridContext.LoadLocalTable(resultTable);
            _lastSchema = resultTable;
            _ = UpdateHeadersAsync();

            sw.Stop();
            _lastElapsedText = FormatElapsed(sw.Elapsed);
            SyncSortStateFromContext();
            SearchResults = new VirtualizingCollection(_gridContext, _lastSchema);
            TotalRecords = _gridContext.TotalCount;
            StatusText = isOffline
                ? $"Found {TotalRecords} records (offline dictionary • {_lastElapsedText})"
                : BuildFoundStatusText(TotalRecords, null, 0, _lastElapsedText);
            _logger.LogInfo("CSV dictionary field search completed. Found {RecordCount} records (Offline={IsOffline}) in {Elapsed}.", TotalRecords, isOffline, _lastElapsedText);
        }

        public async Task SortAsync(string? columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                IsBusy = true;
                await _gridContext.ApplySortAsync(columnName, token);
                SyncSortStateFromContext();

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
                _dialogService.ShowError($"Failed to sort: {ex.Message}", "Error");
            }
            finally
            {
                SyncSortStateFromContext();
                IsBusy = false;
            }
        }

        private void SyncSortStateFromContext()
        {
            if (string.IsNullOrEmpty(_gridContext.SortColumn))
            {
                SortColumn = null;
                SortDirection = null;
            }
            else
            {
                SortColumn = _gridContext.SortColumn;
                SortDirection = string.Equals(_gridContext.SortDirection, "DESC", StringComparison.OrdinalIgnoreCase)
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
        }


        public async Task ApplyFilterAsync(string? filterText)
        {
             // Cancel previous search/filter
             _cts?.Cancel();
             _cts = new CancellationTokenSource();
             var token = _cts.Token;

             FilterText = filterText ?? "";

             _logger.LogDebug("Applying filter: '{FilterText}'", filterText);
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
                 SyncSortStateFromContext();
                 if (_lastSchema != null)
                 {
                     SearchResults = new VirtualizingCollection(_gridContext, _lastSchema);
                 }

                 if (!string.IsNullOrEmpty(filterText))
                 {
                     long matchedCells = await _gridContext.GetTotalMatchedCellsCountAsync(filterText, columns, token);
                     MatchStatusText = $"Found: {TotalRecords} rows ({matchedCells} matches)";
                     _logger.LogDebug("Filter applied. Rows: {RowCount}, Matches: {MatchCount}", TotalRecords, matchedCells);
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

        private VirtualRow? _inspectedRow;

        public void SetInspectedRow(VirtualRow? row)
        {
            _inspectedRow = row;
            UpdateInspectorFields(row);
            if (_columnDescriptions.Count == 0 && row != null)
            {
                _ = RefreshInspectorAsync();
            }
        }

        partial void OnSelectedRowChanged(object? value)
        {
            if (value is VirtualRow vRow)
            {
                SetCurrentRowIndex(vRow.RowIndex);
                SetInspectedRow(vRow);
            }
        }

        partial void OnIsInspectorOpenChanged(bool value)
        {
            if (value)
            {
                _ = RefreshInspectorAsync();
            }
        }

        partial void OnInspectorFilterTextChanged(string value)
        {
            InspectorFieldsView.Refresh();
        }

        private async Task RefreshInspectorAsync()
        {
            await EnsureColumnDescriptionsLoadedAsync();
            var row = _inspectedRow ?? SelectedRow as VirtualRow;
            if (row == null && _lastFoundRowIndex >= 0 && SearchResults != null && _lastFoundRowIndex < SearchResults.Count)
            {
                row = SearchResults[_lastFoundRowIndex] as VirtualRow;
            }
            UpdateInspectorFields(row);
        }

        private async Task EnsureColumnDescriptionsLoadedAsync()
        {
            if (_lastSchema == null || _columnDescriptions.Count > 0) return;

            var colNames = new List<string>();
            foreach (DataColumn c in _lastSchema.Columns)
            {
                colNames.Add(c.ColumnName);
            }

            if (colNames.Count == 0) return;

            try
            {
                var descriptions = await _gridContext.GetColumnDescriptionsAsync(colNames);
                for (int i = 0; i < colNames.Count; i++)
                {
                    string desc = (i < descriptions.Count) ? descriptions[i].Trim() : string.Empty;
                    _columnDescriptions[colNames[i]] = desc;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load column descriptions for inspector: {Message}", ex.Message);
            }
        }

        private void UpdateInspectorFields(VirtualRow? row)
        {
            InspectorFields.Clear();
            if (row == null || _lastSchema == null) return;

            for (int i = 0; i < _lastSchema.Columns.Count; i++)
            {
                string colName = _lastSchema.Columns[i].ColumnName;
                string fieldNumber = colName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && colName.Length > 1 && int.TryParse(colName.Substring(1), out _)
                    ? colName
                    : string.Empty;

                _columnDescriptions.TryGetValue(colName, out string? desc);
                if (string.IsNullOrWhiteSpace(desc) && ColumnHeaders.TryGetValue(colName, out string? header) && !string.Equals(header, colName, StringComparison.OrdinalIgnoreCase))
                {
                    desc = header;
                }

                var rawVal = row.GetValue(i);
                string valStr = (rawVal == null || rawVal == DBNull.Value) ? string.Empty : rawVal.ToString() ?? string.Empty;

                InspectorFields.Add(new RowInspectorField(fieldNumber, colName, desc ?? string.Empty, valStr));
            }
        }

        private bool FilterInspectorField(object obj)
        {
            if (string.IsNullOrWhiteSpace(InspectorFilterText)) return true;
            if (obj is not RowInspectorField field) return false;

            return field.ColumnName.Contains(InspectorFilterText, StringComparison.OrdinalIgnoreCase)
                || field.Description.Contains(InspectorFilterText, StringComparison.OrdinalIgnoreCase)
                || field.Value.Contains(InspectorFilterText, StringComparison.OrdinalIgnoreCase)
                || field.FieldNumber.Contains(InspectorFilterText, StringComparison.OrdinalIgnoreCase);
        }

        private void CopyInspectorValue(RowInspectorField? field)
        {
            if (field == null) return;
            if (string.IsNullOrEmpty(field.Value))
            {
                _dialogService.ShowToast($"No value to copy for {field.ColumnName}", "Inspector", SMS_Search.Views.ToastType.Info);
                return;
            }

            try
            {
                _clipboardService.SetText(field.Value);
                _dialogService.ShowToast($"Copied {field.ColumnName}", "Inspector", SMS_Search.Views.ToastType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to copy inspector value", ex);
            }
        }

        private void CopyInspectorValues(IList? selectedItems) =>
            CopyInspectorItems(selectedItems, f => f.Value, "No values to copy", "value", "values");

        private void CopyInspectorColumns(IList? selectedItems) =>
            CopyInspectorItems(selectedItems, f => f.ColumnName, "No column names to copy", "column name", "column names");

        private void CopyInspectorDescriptions(IList? selectedItems) =>
            CopyInspectorItems(selectedItems, f => f.Description, "No descriptions to copy", "description", "descriptions");

        private void CopyInspectorRows(IList? selectedItems) =>
            CopyInspectorItems(selectedItems, f => $"{f.ColumnName}\t{f.Description}\t{f.Value}", "No rows to copy", "row", "rows");

        private void CopyInspectorItems(
            IList? selectedItems,
            Func<RowInspectorField, string?> selector,
            string emptyToastMessage,
            string singleLabel,
            string pluralLabel)
        {
            var fields = GetOrderedInspectorFields(selectedItems);
            if (fields.Count == 0) return;

            var values = fields
                .Select(selector)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

            if (values.Count == 0)
            {
                _dialogService.ShowToast(emptyToastMessage, "Inspector", SMS_Search.Views.ToastType.Info);
                return;
            }

            try
            {
                _clipboardService.SetText(string.Join(Environment.NewLine, values));
                string message = values.Count == 1
                    ? $"Copied {singleLabel}"
                    : $"Copied {values.Count} {pluralLabel}";
                _dialogService.ShowToast(message, "Inspector", SMS_Search.Views.ToastType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to copy inspector selection", ex);
            }
        }

        private List<RowInspectorField> GetOrderedInspectorFields(IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                return new List<RowInspectorField>();
            }

            var selectedSet = new HashSet<RowInspectorField>();
            foreach (var item in selectedItems)
            {
                if (item is RowInspectorField field)
                {
                    selectedSet.Add(field);
                }
            }

            if (selectedSet.Count == 0)
            {
                return new List<RowInspectorField>();
            }

            var ordered = new List<RowInspectorField>(selectedSet.Count);
            foreach (var field in InspectorFields)
            {
                if (selectedSet.Contains(field))
                {
                    ordered.Add(field);
                }
            }

            return ordered;
        }

        private void FindFieldUsage(string? columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return;
            WeakReferenceMessenger.Default.Send(new FindFieldUsageMessage(columnName.Trim()));
        }

        private void CopySelectionStats()
        {
            if (string.IsNullOrEmpty(SelectionStatsText)) return;
            try
            {
                _clipboardService.SetText(SelectionStatsText);
                _dialogService.ShowToast("Selection stats copied", "Copy", SMS_Search.Views.ToastType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to copy selection stats", ex);
            }
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalMilliseconds < 1000
                ? $"{Math.Max(1, (int)Math.Round(elapsed.TotalMilliseconds))} ms"
                : $"{elapsed.TotalSeconds:0.00} s";
        }

        private static string BuildFoundStatusText(int totalRecords, string? filterText, int unfilteredCount, string elapsedText)
        {
            string baseText = !string.IsNullOrEmpty(filterText)
                ? $"Found {totalRecords} records (Filtered from {unfilteredCount})"
                : $"Found {totalRecords} records";

            return string.IsNullOrEmpty(elapsedText)
                ? baseText
                : $"{baseText} ({elapsedText})";
        }

        partial void OnTotalRecordsChanged(int value)
        {
            IsFilterVisible = _gridContext.UnfilteredCount > 0;
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
            await PerformExportAsync(token => _gridContext.ExportToCsvAsync(filename, null, true, hidden, token), filename, "CSV");
        }

        private async Task ExportJsonAsync()
        {
            string? filename = _dialogService.SaveFileDialog("JSON files (*.json)|*.json", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            if (string.IsNullOrEmpty(filename)) return;
            if (CurrentSearchCriteria == null) return;
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(token => _gridContext.ExportToJsonAsync(filename, CurrentSearchCriteria, null, hidden, token), filename, "JSON");
        }

        private async Task ExportXmlAsync()
        {
            string? filename = _dialogService.SaveFileDialog("XML files (*.xml)|*.xml", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            if (string.IsNullOrEmpty(filename)) return;
            if (CurrentSearchCriteria == null) return;
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(token => _gridContext.ExportToXmlAsync(filename, CurrentSearchCriteria, hidden, token), filename, "XML");
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

        private async Task PerformExportAsync(Func<CancellationToken, Task<int>> exportAction, string exportedFilePath, string format)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsBusy = true;
            StatusText = "Exporting...";
            _logger.LogInfo("Starting {ExportFormat} export to {FilePath}...", format, exportedFilePath);
            try
            {
                int rowCount = await exportAction(token);
                _dialogService.ShowToast($"Successfully exported {rowCount} records to {format}", "Export", SMS_Search.Views.ToastType.Success, null, exportedFilePath);
                _logger.LogInfo("{ExportFormat} export successful. {RowCount} rows exported.", format, rowCount);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Export cancelled";
                _logger.LogInfo("{ExportFormat} export cancelled.", format);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "Export Error");
                _logger.LogError(ex, "{ExportFormat} export failed", format);
            }
            finally
            {
                IsBusy = false;
                if (StatusText == "Exporting...")
                {
                    StatusText = $"Found {TotalRecords} records";
                }
                _cts?.Dispose();
                _cts = null;
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
            await PerformExportAsync(token => _gridContext.ExportRowsToCsvAsync(filename, rows, hidden, token), filename, "CSV");
        }

        private async Task ExportSelectedJsonAsync(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0 || _lastSchema == null || CurrentSearchCriteria == null) return;
            string? filename = _dialogService.SaveFileDialog("JSON files (*.json)|*.json", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            if (string.IsNullOrEmpty(filename)) return;

            var rows = selectedItems.Cast<VirtualRow>().ToList();
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(token => _gridContext.ExportRowsToJsonAsync(filename, rows, CurrentSearchCriteria, hidden, token), filename, "JSON");
        }

        private async Task ExportSelectedXmlAsync(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0 || _lastSchema == null || CurrentSearchCriteria == null) return;
            string? filename = _dialogService.SaveFileDialog("XML files (*.xml)|*.xml", $"SMS_Search_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
            if (string.IsNullOrEmpty(filename)) return;

            var rows = selectedItems.Cast<VirtualRow>().ToList();
            var hidden = CheckHiddenColumns();
            await PerformExportAsync(token => _gridContext.ExportRowsToXmlAsync(filename, rows, CurrentSearchCriteria, hidden, token), filename, "XML");
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
                _logger.LogInfo("Copied {RowCount} rows to clipboard.", selectedItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError("Copy Row failed", ex);
                _dialogService.ShowError("Failed to copy: " + ex.Message, "Copy Error");
            }
        }

        private void CopyAsMarkdown(IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0 || _lastSchema == null) return;

            var visibleCols = new List<(int Index, string Header)>();
            for (int i = 0; i < _lastSchema.Columns.Count; i++)
            {
                string colName = _lastSchema.Columns[i].ColumnName;
                if (HiddenColumns.Contains(colName)) continue;

                string header = ColumnHeaders.TryGetValue(colName, out string? h) && !string.IsNullOrWhiteSpace(h)
                    ? h
                    : colName;
                visibleCols.Add((i, header));
            }

            if (visibleCols.Count == 0) return;

            var headers = visibleCols.Select(c => c.Header).ToList();
            var rows = new List<IReadOnlyList<string?>>();

            foreach (var item in selectedItems)
            {
                if (item is VirtualRow vRow)
                {
                    var rowValues = new List<string?>(visibleCols.Count);
                    foreach (var col in visibleCols)
                    {
                        var val = vRow.GetValue(col.Index);
                        rowValues.Add((val == null || val == DBNull.Value) ? string.Empty : val.ToString());
                    }
                    rows.Add(rowValues);
                }
            }

            if (rows.Count == 0) return;

            string markdown = MarkdownTableFormatter.Format(headers, rows);
            try
            {
                _clipboardService.SetText(markdown);
                _dialogService.ShowToast($"Copied {rows.Count} row(s) as Markdown", "Copy", SMS_Search.Views.ToastType.Success);
                _logger.LogInfo("Copied {RowCount} rows as Markdown table.", rows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError("Copy as Markdown failed", ex);
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
            _logger.LogInfo("Copied SQL INSERT statements for {RowCount} rows.", selectedItems.Count);
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

        private bool CanCopyAllMatches()
        {
            return !string.IsNullOrEmpty(FilterText) && !string.IsNullOrEmpty(MatchStatusText) && TotalRecords > 0;
        }

        private async Task CopyAllMatchesAsync()
        {
            if (!CanCopyAllMatches()) return;

            IsBusy = true;
            StatusText = "Copying matches...";

            try
            {
                var columns = new List<string>();
                if (SearchResults is VirtualizingCollection vc)
                {
                    var props = vc.GetItemProperties(null);
                    foreach (PropertyDescriptor p in props) columns.Add(p.Name);
                }

                if (columns.Count == 0) return;

                var matches = await _gridContext.GetAllMatchesAsync(FilterText, columns, CancellationToken.None);
                if (matches.Count > 0)
                {
                    _clipboardService.SetText(string.Join(Environment.NewLine, matches));
                    _dialogService.ShowToast($"Successfully copied {matches.Count} matches to clipboard", "Copy Matches", SMS_Search.Views.ToastType.Success);
                    _logger.LogInfo("Copied {MatchCount} matches to clipboard.", matches.Count);
                }
                else
                {
                    _dialogService.ShowMessage("No matches found to copy.", "Copy Matches");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to copy all matches", ex);
                _dialogService.ShowError($"Failed to copy matches: {ex.Message}", "Copy Error");
            }
            finally
            {
                IsBusy = false;
                StatusText = $"Found {TotalRecords} records";
            }
        }
    }
}
