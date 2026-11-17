using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Dapper;
using SMS_Search.Services;

namespace SMS_Search.Data
{
    public class VirtualGridContext : IDisposable
    {
        private readonly IDataRepository _repo;
        private readonly IFieldDictionaryService? _fieldDictionaryService;
        private CancellationTokenSource? _queryCts;

        private DataTable? _localTable;
        private List<DataRow>? _localView;

        private string? _server;
        private string? _database;
        private string? _user;
        private string? _pass;

        private string? _baseSql;
        private object? _parameters;

        public int TotalCount { get; private set; }
        public int UnfilteredCount { get; private set; }
        private Dictionary<int, DataRow> _cache;
        private readonly Queue<int> _cacheKeys = new Queue<int>();
        private HashSet<int> _pagesBeingFetched;
        private Dictionary<int, TaskCompletionSource<bool>> _pageCompletionSources;
        private const int PageSize = 1000;
        private const int MaxCacheSize = 5000;
        private int _version = 0;

        public string? SortColumn { get; private set; }
        public string SortDirection { get; private set; } = "ASC";
        public string? FilterText { get; private set; }
        private string? _rawFilterText;
        private List<string>? _filterColumns;

        private Dictionary<string, string?> _columnSqlTypes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SafeStringTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "char", "nchar", "varchar", "nvarchar", "text", "ntext", "sysname"
        };

        public event EventHandler? DataReady;
        public event EventHandler<Exception>? LoadError;

        public VirtualGridContext(IDataRepository repo, IFieldDictionaryService? fieldDictionaryService = null)
        {
            _repo = repo;
            _fieldDictionaryService = fieldDictionaryService;
            _cache = new Dictionary<int, DataRow>();
            _pagesBeingFetched = new HashSet<int>();
            _pageCompletionSources = new Dictionary<int, TaskCompletionSource<bool>>();
        }

        public void SetConnection(string server, string database, string? user, string? pass)
        {
            _server = server;
            _database = database;
            _user = user;
            _pass = pass;
        }

        public async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_server) || string.IsNullOrEmpty(_database))
            {
                return false;
            }

            return await _repo.TableExistsAsync(_server, _database, _user, _pass, tableName, cancellationToken);
        }

        public void LoadLocalTable(DataTable table)
        {
            _queryCts?.Cancel();
            _baseSql = null;
            _parameters = null;
            _localTable = table;
            SortColumn = null;
            SortDirection = "ASC";
            FilterText = null;
            _rawFilterText = null;
            _filterColumns = null;
            _cache.Clear();
            _cacheKeys.Clear();
            _pagesBeingFetched.Clear();
            _pageCompletionSources.Clear();
            _columnSqlTypes.Clear();

            RebuildLocalView();
            UnfilteredCount = TotalCount;
            DataReady?.Invoke(this, EventArgs.Empty);
        }

        private void RebuildLocalView()
        {
            if (_localTable == null)
            {
                _localView = null;
                TotalCount = 0;
                return;
            }

            IEnumerable<DataRow> query = _localTable.Rows.Cast<DataRow>();

            if (!string.IsNullOrWhiteSpace(_rawFilterText) && _filterColumns != null && _filterColumns.Count > 0)
            {
                string term = _rawFilterText.Trim();
                query = query.Where(row => _filterColumns.Any(col =>
                    _localTable.Columns.Contains(col) &&
                    (row[col]?.ToString() ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrEmpty(SortColumn) && _localTable.Columns.Contains(SortColumn))
            {
                string col = SortColumn;
                query = SortDirection == "DESC"
                    ? query.OrderByDescending(r => r[col] == DBNull.Value ? null : r[col], LocalValueComparer.Instance)
                    : query.OrderBy(r => r[col] == DBNull.Value ? null : r[col], LocalValueComparer.Instance);
            }

            _localView = query.ToList();
            TotalCount = _localView.Count;
        }

        private sealed class LocalValueComparer : IComparer<object?>
        {
            public static readonly LocalValueComparer Instance = new();
            public int Compare(object? x, object? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                if (x is IComparable cx && x.GetType() == y.GetType())
                    return cx.CompareTo(y);
                return StringComparer.OrdinalIgnoreCase.Compare(x.ToString(), y.ToString());
            }
        }

        public async Task LoadAsync(string sql, object? parameters, string? initialSortColumn = null, CancellationToken cancellationToken = default)
        {
            _localTable = null;
            _localView = null;
            _baseSql = sql;
            _parameters = parameters;
            SortColumn = initialSortColumn;
            SortDirection = "ASC";
            FilterText = null;
            UnfilteredCount = 0;

            await ReloadAsync(cancellationToken);

            if (string.IsNullOrEmpty(FilterText))
            {
                UnfilteredCount = TotalCount;
            }
        }

        public async Task ApplyFilterAsync(string filterText, IEnumerable<string> columns, CancellationToken cancellationToken = default)
        {
            _rawFilterText = filterText;
            _filterColumns = new List<string>(columns);

            if (string.IsNullOrWhiteSpace(filterText))
            {
                FilterText = null;
            }
            else
            {
                var clauses = new List<string>();
                foreach (var col in columns)
                {
                    string safeCol = col.Replace("[", "").Replace("]", "");
                    if (_columnSqlTypes.TryGetValue(col, out string? type) && type != null && SafeStringTypes.Contains(type))
                    {
                        clauses.Add($"[{safeCol}] LIKE @_SmsFilterPattern");
                    }
                    else
                    {
                        clauses.Add($"CAST([{safeCol}] AS NVARCHAR(MAX)) LIKE @_SmsFilterPattern");
                    }
                }
                FilterText = string.Join(" OR ", clauses);
            }

            await ReloadAsync(cancellationToken);
        }

        private object? GetEffectiveParameters()
        {
            if (string.IsNullOrEmpty(FilterText) || string.IsNullOrWhiteSpace(_rawFilterText))
            {
                return _parameters;
            }

            var dp = new DynamicParameters();
            if (_parameters != null)
            {
                dp.AddDynamicParams(_parameters);
            }
            dp.Add("_SmsFilterPattern", $"%{_rawFilterText}%");
            return dp;
        }

        private long CountLocalMatches(string searchText, IEnumerable<string> columns, bool countCells, int? limitRowIndex = null)
        {
            if (_localView == null || _localTable == null || string.IsNullOrWhiteSpace(searchText)) return 0;
            string term = searchText.Trim();
            var cols = columns.Where(c => _localTable.Columns.Contains(c)).ToList();
            int limit = limitRowIndex.HasValue ? Math.Min(limitRowIndex.Value, _localView.Count) : _localView.Count;
            long count = 0;

            for (int i = 0; i < limit; i++)
            {
                var row = _localView[i];
                int rowMatches = 0;
                foreach (var col in cols)
                {
                    if ((row[col]?.ToString() ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        rowMatches++;
                        if (!countCells) break;
                    }
                }
                count += countCells ? rowMatches : (rowMatches > 0 ? 1 : 0);
            }
            return count;
        }

        public async Task<long> GetTotalMatchCountAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_rawFilterText) || _filterColumns == null || _filterColumns.Count == 0)
                return 0;

            if (_localTable != null) return CountLocalMatches(_rawFilterText, _filterColumns, countCells: false);

            var colTypes = new Dictionary<string, string?>();
            foreach(var col in _filterColumns)
            {
                if(_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetTotalMatchCountAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, _rawFilterText, colTypes, cancellationToken);
        }

        public async Task<long> CountMatchesAsync(string searchText, IEnumerable<string> columns, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText) || columns == null) return 0;
            if (_localTable != null) return CountLocalMatches(searchText, columns, countCells: false);

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in columns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetTotalMatchCountAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, searchText, colTypes, cancellationToken);
        }

        public async Task<long> GetTotalMatchedCellsCountAsync(string searchText, IEnumerable<string> columns, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText) || columns == null) return 0;
            if (_localTable != null) return CountLocalMatches(searchText, columns, countCells: true);

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in columns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetTotalMatchedCellsCountAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, searchText, colTypes, cancellationToken);
        }

        public async Task<long> GetPrecedingMatchedCellsCountAsync(int limitRowIndex, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_rawFilterText) || _filterColumns == null || _filterColumns.Count == 0 || limitRowIndex <= 0)
                return 0;
            if (_localTable != null) return CountLocalMatches(_rawFilterText, _filterColumns, countCells: true, limitRowIndex);

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in _filterColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetPrecedingMatchedCellsCountAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, _rawFilterText, colTypes, limitRowIndex, SortColumn, SortDirection, cancellationToken);
        }

        public async Task<long> GetPrecedingMatchCountAsync(int limitRowIndex, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_rawFilterText) || _filterColumns == null || _filterColumns.Count == 0 || limitRowIndex <= 0)
                return 0;
            if (_localTable != null) return CountLocalMatches(_rawFilterText, _filterColumns, countCells: false, limitRowIndex);

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in _filterColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetPrecedingMatchCountAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, _rawFilterText, colTypes, limitRowIndex, SortColumn, SortDirection, cancellationToken);
        }

        public async Task WaitForRowAsync(int rowIndex)
        {
            if (_localTable != null) return;
            if (_cache.ContainsKey(rowIndex)) return;
            GetValue(rowIndex, 0); // Trigger fetch
            if (_cache.ContainsKey(rowIndex)) return;

            int pageIndex = rowIndex / PageSize;
            TaskCompletionSource<bool>? tcs;

            if (!_pageCompletionSources.TryGetValue(pageIndex, out tcs))
            {
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pageCompletionSources[pageIndex] = tcs;
            }

            var timeoutTask = Task.Delay(5000);
            if (tcs != null)
            {
                await Task.WhenAny(tcs.Task, timeoutTask);
            }
        }

        public async Task EnsureRangeLoadedAsync(int startIndex, int count)
        {
            if (count <= 0) return;
            var tasks = new List<Task>();
            int endRow = startIndex + count;

            for (int i = startIndex; i < endRow; i += PageSize)
            {
                GetValue(i, 0);
            }

            for (int i = startIndex; i < endRow; i += PageSize)
            {
                tasks.Add(WaitForRowAsync(i));
            }

            await Task.WhenAll(tasks);
        }

        public async Task ApplySortAsync(string column, CancellationToken cancellationToken = default)
        {
            if (SortColumn == column)
            {
                SortDirection = SortDirection == "ASC" ? "DESC" : "ASC";
            }
            else
            {
                SortColumn = column;
                SortDirection = "ASC";
            }

            await ReloadAsync(cancellationToken);
        }

        private async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            _version++;
            int currentVersion = _version;

            _queryCts?.Cancel();
            _queryCts?.Dispose();
            _queryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var linkedToken = _queryCts.Token;

            try
            {
                _cache.Clear();
                _cacheKeys.Clear();
                _pagesBeingFetched.Clear();
                _pageCompletionSources.Clear();

                if (_localTable != null)
                {
                    RebuildLocalView();
                    DataReady?.Invoke(this, EventArgs.Empty);
                }
                else if (_server != null && _database != null && _baseSql != null)
                {
                    TotalCount = await _repo.GetQueryCountAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, linkedToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoadError?.Invoke(this, ex);
            }
        }

        public void Dispose()
        {
            _queryCts?.Cancel();
            _queryCts?.Dispose();
            _queryCts = null;
        }

        public object? GetValue(int rowIndex, int colIndex)
        {
            if (_localView != null)
            {
                if (rowIndex >= 0 && rowIndex < _localView.Count)
                {
                    var localRow = _localView[rowIndex];
                    if (colIndex >= 0 && colIndex < localRow.Table.Columns.Count)
                        return localRow[colIndex];
                }
                return "";
            }

            // Prefetch next page logic
            int pageIndex = rowIndex / PageSize;
            int offsetInPage = rowIndex % PageSize;

            // If we are past 70% of the current page, check if we should load the next page
            if (offsetInPage > (PageSize * 0.7))
            {
                int nextPageRowIndex = (pageIndex + 1) * PageSize;
                if (nextPageRowIndex < TotalCount)
                {
                    // Check if the first row of the next page is already loaded
                    if (!_cache.ContainsKey(nextPageRowIndex))
                    {
                        _ = RequestPageAsync(nextPageRowIndex);
                    }
                }
            }

            if (_cache.TryGetValue(rowIndex, out DataRow? row))
            {
                if (row != null && colIndex >= 0 && colIndex < row.Table.Columns.Count)
                    return row[colIndex];
                return "";
            }

            _ = RequestPageAsync(rowIndex);
            return null;
        }

        private async Task RequestPageAsync(int rowIndex)
        {
            int pageIndex = rowIndex / PageSize;

            if (_pagesBeingFetched.Contains(pageIndex)) return;

            int currentVersion = _version;
            var token = _queryCts?.Token ?? CancellationToken.None;
            _pagesBeingFetched.Add(pageIndex);

            try
            {
                int offset = pageIndex * PageSize;

                string? sortCol = SortColumn;
                string sortDir = SortDirection;
                string? filter = FilterText;

                if (_server != null && _database != null && _baseSql != null)
                {
                    var dt = await _repo.GetQueryPageAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), offset, PageSize, sortCol, sortDir, filter, token);

                    if (_version != currentVersion || token.IsCancellationRequested) return;

                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        int absIndex = offset + i;
                        if (!_cache.ContainsKey(absIndex))
                        {
                            while (_cache.Count >= MaxCacheSize && _cacheKeys.TryDequeue(out int oldestKey))
                            {
                                _cache.Remove(oldestKey);
                            }
                            _cacheKeys.Enqueue(absIndex);
                        }
                        _cache[absIndex] = dt.Rows[i];
                    }

                    DataReady?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when query reloads or disposes
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Page fetch error: " + ex.Message);
                LoadError?.Invoke(this, ex);
            }
            finally
            {
                if (_version == currentVersion)
                {
                    _pagesBeingFetched.Remove(pageIndex);

                    if (_pageCompletionSources.TryGetValue(pageIndex, out var tcs))
                    {
                        tcs.TrySetResult(true);
                        _pageCompletionSources.Remove(pageIndex);
                    }
                }
            }
        }

        public async Task<DataTable> GetSchemaAsync(string sql, object? parameters, CancellationToken cancellationToken = default)
        {
             if (_server == null || _database == null) return new DataTable();

             var dt = await _repo.GetQuerySchemaAsync(_server, _database, _user, _pass, sql, parameters, cancellationToken);
             _columnSqlTypes.Clear();
             foreach(DataColumn col in dt.Columns)
             {
                 if(col.ExtendedProperties.ContainsKey("SqlType"))
                 {
                     _columnSqlTypes[col.ColumnName] = col.ExtendedProperties["SqlType"] as string;
                 }
             }
             return dt;
        }

        private string BuildExportSql()
        {
             string finalSql = _baseSql ?? "";
             if (!string.IsNullOrWhiteSpace(FilterText))
             {
                 finalSql = $"SELECT * FROM ({_baseSql}) AS _FilterQ WHERE {FilterText}";
             }

             if (!string.IsNullOrEmpty(SortColumn))
             {
                string safeCol = SortColumn.Replace("[", "").Replace("]", "");
                string orderByExpr = $"[{safeCol}] {SortDirection}";
                if (safeCol.Equals("Field", StringComparison.OrdinalIgnoreCase))
                {
                    orderByExpr = $"TRY_CAST(SUBSTRING([{safeCol}], 2, LEN([{safeCol}])) AS INT) {SortDirection}, [{safeCol}] {SortDirection}";
                }

                if (string.IsNullOrWhiteSpace(FilterText))
                {
                    finalSql = $"SELECT * FROM ({finalSql}) AS _SortQ ORDER BY {orderByExpr}";
                }
                else
                {
                    finalSql += $" ORDER BY {orderByExpr}";
                }
             }
             return finalSql;
        }

        public async Task<int> ExportToCsvAsync(string filename, Dictionary<string, string>? headerMap = null, bool includeHeaders = true, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             if (_localTable != null && _localView != null)
             {
                 return await ExportLocalTableToCsvAsync(filename, headerMap, includeHeaders, hiddenColumns);
             }

             string finalSql = BuildExportSql();
             if (_server == null || _database == null) return 0;
             int rowCount = 0;

             using (var reader = await _repo.GetQueryDataReaderAsync(_server, _database, _user, _pass, finalSql, GetEffectiveParameters(), cancellationToken))
             {
                 using (var writer = new StreamWriter(filename))
                 {
                     if (includeHeaders)
                     {
                         bool first = true;
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             string colName = reader.GetName(i);
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             if (!first) await writer.WriteAsync(",");
                             first = false;

                             string header = (headerMap != null && headerMap.ContainsKey(colName)) ? headerMap[colName] : colName;
                             WriteCsvCell(writer, header);
                         }
                         await writer.WriteLineAsync();
                     }

                     while (await reader.ReadAsync(cancellationToken))
                     {
                         rowCount++;
                         bool first = true;
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             string colName = reader.GetName(i);
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             if (!first) await writer.WriteAsync(",");
                             first = false;

                             var val = reader.GetValue(i);
                             string sVal = val == DBNull.Value ? "" : val.ToString() ?? "";
                             WriteCsvCell(writer, sVal);
                         }
                         await writer.WriteLineAsync();
                     }
                 }
             }
             return rowCount;
        }

        public async Task<int> ExportToJsonAsync(string filename, SearchCriteria criteria, Dictionary<string, string>? headerMap = null, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();
             if (_server == null || _database == null) return 0;
             int rowCount = 0;

             int exportRecordCount = 0;
             if (_baseSql != null)
             {
                 exportRecordCount = await _repo.GetQueryCountAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, cancellationToken);
             }

             using (var reader = await _repo.GetQueryDataReaderAsync(_server, _database, _user, _pass, finalSql, GetEffectiveParameters(), cancellationToken))
             {
                 using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                 using (var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true }))
                 {
                     writer.WriteStartObject();

                     if (criteria.Mode == SearchMode.Function)
                     {
                         writer.WriteString("SearchType", "Function");
                         if (!string.IsNullOrEmpty(criteria.Value))
                             writer.WriteString("Number", criteria.Value);
                     }
                     else if (criteria.Mode == SearchMode.Totalizer)
                     {
                         writer.WriteString("SearchType", "Totalizer");
                         if (!string.IsNullOrEmpty(criteria.Value))
                             writer.WriteString("Number", criteria.Value);
                     }
                     else if (criteria.Mode == SearchMode.Field)
                     {
                         writer.WriteString("SearchType", "Field");
                         if (!string.IsNullOrEmpty(criteria.Value))
                         {
                             if (criteria.Type == SearchType.Number)
                                 writer.WriteString("Number", criteria.Value);
                             else if (criteria.Type == SearchType.Description)
                                 writer.WriteString("Description", criteria.Value);
                             else if (criteria.Type == SearchType.CustomSql)
                                 writer.WriteString("SQL", criteria.Value);
                             else if (criteria.Type == SearchType.Table)
                                 writer.WriteString("Table", criteria.Value);
                         }
                     }

                     writer.WriteNumber("RecordCount", exportRecordCount);

                     writer.WritePropertyName("Rows");
                     writer.WriteStartArray();
                     while (await reader.ReadAsync(cancellationToken))
                     {
                         rowCount++;
                         writer.WriteStartObject();
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             string colName = reader.GetName(i);
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             string header = (headerMap != null && headerMap.ContainsKey(colName)) ? headerMap[colName] : colName;
                             var val = reader.GetValue(i);

                             writer.WritePropertyName(header);

                             if (val == DBNull.Value) writer.WriteNullValue();
                             else if (val is bool b) writer.WriteBooleanValue(b);
                             else if (IsNumeric(val)) writer.WriteNumberValue(Convert.ToDecimal(val));
                             else writer.WriteStringValue(val.ToString());
                         }
                         writer.WriteEndObject();
                     }
                     writer.WriteEndArray();

                     writer.WriteEndObject();
                 }
             }
             return rowCount;
        }

        public async Task<int> ExportToXmlAsync(string filename, SearchCriteria criteria, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();
             if (_server == null || _database == null) return 0;
             int rowCount = 0;

             int exportRecordCount = 0;
             if (_baseSql != null)
             {
                 exportRecordCount = await _repo.GetQueryCountAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, cancellationToken);
             }

             string rootElement = "SearchResults";
             string rootAttr = "";

             if (criteria.Mode == SearchMode.Function)
             {
                 rootAttr += " SearchType=\"Function\"";
                 if (!string.IsNullOrEmpty(criteria.Value))
                     rootAttr += $" Number=\"{EscapeXml(criteria.Value)}\"";
             }
             else if (criteria.Mode == SearchMode.Totalizer)
             {
                 rootAttr += " SearchType=\"Totalizer\"";
                 if (!string.IsNullOrEmpty(criteria.Value))
                     rootAttr += $" Number=\"{EscapeXml(criteria.Value)}\"";
             }
             else if (criteria.Mode == SearchMode.Field)
             {
                 rootAttr += " SearchType=\"Field\"";
                 if (!string.IsNullOrEmpty(criteria.Value))
                 {
                     if (criteria.Type == SearchType.Number)
                         rootAttr += $" Number=\"{EscapeXml(criteria.Value)}\"";
                     else if (criteria.Type == SearchType.Description)
                         rootAttr += $" Description=\"{EscapeXml(criteria.Value)}\"";
                     else if (criteria.Type == SearchType.CustomSql)
                         rootAttr += $" SQL=\"{EscapeXml(criteria.Value)}\"";
                     else if (criteria.Type == SearchType.Table)
                         rootAttr += $" Table=\"{EscapeXml(criteria.Value)}\"";
                 }
             }

             rootAttr += $" RecordCount=\"{exportRecordCount}\"";

             using (var reader = await _repo.GetQueryDataReaderAsync(_server, _database, _user, _pass, finalSql, GetEffectiveParameters(), cancellationToken))
             {
                 using (var writer = new StreamWriter(filename))
                 {
                     writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                     writer.WriteLine($"<{rootElement}{rootAttr}>");

                     List<string> safeColNames = new List<string>();
                     List<string> actualColNames = new List<string>();
                     for (int i = 0; i < reader.FieldCount; i++)
                     {
                         actualColNames.Add(reader.GetName(i));
                         safeColNames.Add(MakeValidXmlName(reader.GetName(i)));
                     }

                     while (await reader.ReadAsync(cancellationToken))
                     {
                         rowCount++;
                         await writer.WriteLineAsync("  <Row>");
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             string colName = actualColNames[i];
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             var val = reader.GetValue(i);
                             if (val != DBNull.Value)
                             {
                                 string tag = safeColNames[i];
                                 string? s = val.ToString();
                                 await writer.WriteLineAsync($"    <{tag}>{EscapeXml(s ?? "")}</{tag}>");
                             }
                         }
                         await writer.WriteLineAsync("  </Row>");
                     }

                     await writer.WriteLineAsync($"</{rootElement}>");
                 }
             }
             return rowCount;
        }

        private string MakeValidXmlName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Column";
            string safeName = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w_.-]", "_");
            if (!char.IsLetter(safeName[0]) && safeName[0] != '_') safeName = "_" + safeName;
            return safeName;
        }

        public async Task<int> ExportRowsToCsvAsync(string filename, List<VirtualRow> rows, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             int rowCount = 0;
             await Task.Run(() =>
             {
                 using (var writer = new StreamWriter(filename))
                 {
                     if (rows.Count > 0)
                     {
                         var row = rows[0];
                         var props = row.GetProperties();
                         bool first = true;
                         for (int i = 0; i < props.Count; i++)
                         {
                             if (hiddenColumns != null && hiddenColumns.Contains(props[i].Name)) continue;

                             if (!first) writer.Write(",");
                             first = false;

                             WriteCsvCell(writer, props[i].Name);
                         }
                         writer.WriteLine();
                     }

                     foreach (var row in rows)
                     {
                         cancellationToken.ThrowIfCancellationRequested();
                         rowCount++;
                         var props = row.GetProperties();
                         bool first = true;
                         for (int i = 0; i < props.Count; i++)
                         {
                             if (hiddenColumns != null && hiddenColumns.Contains(props[i].Name)) continue;

                             if (!first) writer.Write(",");
                             first = false;

                             var val = row.GetValue(i);
                             string sVal = val == DBNull.Value ? "" : val?.ToString() ?? "";
                             WriteCsvCell(writer, sVal);
                         }
                         writer.WriteLine();
                     }
                 }
             }, cancellationToken);
             return rowCount;
        }

        public async Task<int> ExportRowsToJsonAsync(string filename, List<VirtualRow> rows, SearchCriteria criteria, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             int rowCount = 0;
             await Task.Run(() =>
             {
                 using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                 using (var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true }))
                 {
                     writer.WriteStartObject();

                     if (criteria.Mode == SearchMode.Function)
                     {
                         writer.WriteString("SearchType", "Function");
                         if (!string.IsNullOrEmpty(criteria.Value))
                             writer.WriteString("Number", criteria.Value);
                     }
                     else if (criteria.Mode == SearchMode.Totalizer)
                     {
                         writer.WriteString("SearchType", "Totalizer");
                         if (!string.IsNullOrEmpty(criteria.Value))
                             writer.WriteString("Number", criteria.Value);
                     }
                     else if (criteria.Mode == SearchMode.Field)
                     {
                         writer.WriteString("SearchType", "Field");
                         if (!string.IsNullOrEmpty(criteria.Value))
                         {
                             if (criteria.Type == SearchType.Number)
                                 writer.WriteString("Number", criteria.Value);
                             else if (criteria.Type == SearchType.Description)
                                 writer.WriteString("Description", criteria.Value);
                             else if (criteria.Type == SearchType.CustomSql)
                                 writer.WriteString("SQL", criteria.Value);
                             else if (criteria.Type == SearchType.Table)
                                 writer.WriteString("Table", criteria.Value);
                         }
                     }

                     writer.WriteNumber("RecordCount", rows.Count);

                     writer.WritePropertyName("Rows");
                     writer.WriteStartArray();
                     foreach (var row in rows)
                     {
                         cancellationToken.ThrowIfCancellationRequested();
                         rowCount++;
                         writer.WriteStartObject();
                         var props = row.GetProperties();
                         for (int i = 0; i < props.Count; i++)
                         {
                             string colName = props[i].Name;
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             var val = row.GetValue(i);

                             writer.WritePropertyName(colName);

                             if (val == null || val == DBNull.Value) writer.WriteNullValue();
                             else if (val is bool b) writer.WriteBooleanValue(b);
                             else if (IsNumeric(val)) writer.WriteNumberValue(Convert.ToDecimal(val));
                             else writer.WriteStringValue(val.ToString());
                         }
                         writer.WriteEndObject();
                     }
                     writer.WriteEndArray();

                     writer.WriteEndObject();
                 }
             }, cancellationToken);
             return rowCount;
        }

        public async Task<int> ExportRowsToXmlAsync(string filename, List<VirtualRow> rows, SearchCriteria criteria, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             int rowCount = 0;
             await Task.Run(() =>
             {
                 string rootElement = "SearchResults";
                 string rootAttr = "";

                 if (criteria.Mode == SearchMode.Function)
                 {
                     rootAttr += " SearchType=\"Function\"";
                     if (!string.IsNullOrEmpty(criteria.Value))
                         rootAttr += $" Number=\"{EscapeXml(criteria.Value)}\"";
                 }
                 else if (criteria.Mode == SearchMode.Totalizer)
                 {
                     rootAttr += " SearchType=\"Totalizer\"";
                     if (!string.IsNullOrEmpty(criteria.Value))
                         rootAttr += $" Number=\"{EscapeXml(criteria.Value)}\"";
                 }
                 else if (criteria.Mode == SearchMode.Field)
                 {
                     rootAttr += " SearchType=\"Field\"";
                     if (!string.IsNullOrEmpty(criteria.Value))
                     {
                         if (criteria.Type == SearchType.Number)
                             rootAttr += $" Number=\"{EscapeXml(criteria.Value)}\"";
                         else if (criteria.Type == SearchType.Description)
                             rootAttr += $" Description=\"{EscapeXml(criteria.Value)}\"";
                         else if (criteria.Type == SearchType.CustomSql)
                             rootAttr += $" SQL=\"{EscapeXml(criteria.Value)}\"";
                         else if (criteria.Type == SearchType.Table)
                             rootAttr += $" Table=\"{EscapeXml(criteria.Value)}\"";
                     }
                 }

                 rootAttr += $" RecordCount=\"{rows.Count}\"";

                 using (var writer = new StreamWriter(filename))
                 {
                     writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                     writer.WriteLine($"<{rootElement}{rootAttr}>");

                     List<string> safeColNames = new List<string>();
                     List<string> actualColNames = new List<string>();
                     if (rows.Count > 0)
                     {
                         var props = rows[0].GetProperties();
                         for (int i = 0; i < props.Count; i++)
                         {
                             actualColNames.Add(props[i].Name);
                             safeColNames.Add(MakeValidXmlName(props[i].Name));
                         }
                     }

                     foreach (var row in rows)
                     {
                         cancellationToken.ThrowIfCancellationRequested();
                         rowCount++;
                         writer.WriteLine("  <Row>");
                         var props = row.GetProperties();
                         for (int i = 0; i < props.Count; i++)
                         {
                             string colName = actualColNames[i];
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             var val = row.GetValue(i);
                             if (val != null && val != DBNull.Value)
                             {
                                 string tag = safeColNames[i];
                                 string? s = val.ToString();
                                 writer.WriteLine($"    <{tag}>{EscapeXml(s ?? "")}</{tag}>");
                             }
                         }
                         writer.WriteLine("  </Row>");
                     }

                     writer.WriteLine($"</{rootElement}>");
                 }
             }, cancellationToken);
             return rowCount;
        }

        private string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private bool IsNumeric(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }

        private void WriteCsvCell(StreamWriter writer, string s)
        {
            writer.Write('"');
            if (s.IndexOf('"') == -1)
            {
                writer.Write(s);
            }
            else
            {
                ReadOnlySpan<char> span = s.AsSpan();
                int index;
                while ((index = span.IndexOf('"')) != -1)
                {
                    writer.Write(span.Slice(0, index));
                    writer.Write("\"\"");
                    span = span.Slice(index + 1);
                }
                writer.Write(span);
            }
            writer.Write('"');
        }

        private async Task<int> ExportLocalTableToCsvAsync(string filename, Dictionary<string, string>? headerMap, bool includeHeaders, HashSet<string>? hiddenColumns)
        {
            if (_localTable == null || _localView == null) return 0;
            int rowCount = 0;

            using var writer = new StreamWriter(filename);
            if (includeHeaders)
            {
                bool first = true;
                for (int i = 0; i < _localTable.Columns.Count; i++)
                {
                    string colName = _localTable.Columns[i].ColumnName;
                    if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;
                    if (!first) await writer.WriteAsync(",");
                    first = false;
                    string header = (headerMap != null && headerMap.ContainsKey(colName)) ? headerMap[colName] : colName;
                    WriteCsvCell(writer, header);
                }
                await writer.WriteLineAsync();
            }

            foreach (var row in _localView)
            {
                rowCount++;
                bool first = true;
                for (int i = 0; i < _localTable.Columns.Count; i++)
                {
                    string colName = _localTable.Columns[i].ColumnName;
                    if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;
                    if (!first) await writer.WriteAsync(",");
                    first = false;
                    var val = row[i];
                    string sVal = val == DBNull.Value ? "" : val.ToString() ?? "";
                    WriteCsvCell(writer, sVal);
                }
                await writer.WriteLineAsync();
            }
            return rowCount;
        }

        public async Task<List<string>> GetAllMatchesAsync(string searchText, IEnumerable<string> searchColumns, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText) || searchColumns == null) return new List<string>();
            if (_localTable != null && _localView != null)
            {
                string term = searchText.Trim();
                var cols = searchColumns.Where(c => _localTable.Columns.Contains(c)).ToList();
                var matches = new List<string>();
                foreach (var row in _localView)
                {
                    foreach (var col in cols)
                    {
                        string val = row[col]?.ToString() ?? "";
                        if (val.Contains(term, StringComparison.OrdinalIgnoreCase))
                            matches.Add(val);
                    }
                }
                return matches;
            }

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in searchColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return new List<string>();

            return await _repo.GetAllMatchesAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, searchText, colTypes, cancellationToken);
        }

        public async Task<int> FindMatchRowAsync(string searchText, IEnumerable<string> searchColumns, int startRowIndex, bool forward, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText) || searchColumns == null) return -1;
            if (_localTable != null && _localView != null)
            {
                string term = searchText.Trim();
                var cols = searchColumns.Where(c => _localTable.Columns.Contains(c)).ToList();
                if (_localView.Count == 0 || cols.Count == 0) return -1;

                int step = forward ? 1 : -1;
                for (int offset = 0; offset < _localView.Count; offset++)
                {
                    int idx = ((startRowIndex + offset * step) % _localView.Count + _localView.Count) % _localView.Count;
                    var row = _localView[idx];
                    if (cols.Any(c => (row[c]?.ToString() ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)))
                        return idx;
                }
                return -1;
            }

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in searchColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return -1;

            return await _repo.GetMatchRowIndexAsync(_server, _database, _user, _pass, _baseSql, GetEffectiveParameters(), FilterText, searchText, colTypes, startRowIndex, SortColumn, SortDirection, forward, cancellationToken);
        }

        public async Task<List<string>> GetColumnDescriptionsAsync(IEnumerable<string> columns, CancellationToken cancellationToken = default)
        {
            if (_fieldDictionaryService != null && _fieldDictionaryService.IsCsvActive())
            {
                return _fieldDictionaryService.GetColumnDescriptions(columns);
            }

            if (_server == null || _database == null) return new List<string>();
            return await _repo.GetColumnDescriptionsAsync(_server, _database, _user, _pass, columns, cancellationToken);
        }
    }
}
