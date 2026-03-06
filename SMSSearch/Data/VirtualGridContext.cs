using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace SMS_Search.Data
{
    public class VirtualGridContext
    {
        private readonly IDataRepository _repo;

        private string? _server;
        private string? _database;
        private string? _user;
        private string? _pass;

        private string? _baseSql;
        private object? _parameters;

        public int TotalCount { get; private set; }
        public int UnfilteredCount { get; private set; }
        private Dictionary<int, DataRow> _cache;
        private HashSet<int> _pagesBeingFetched;
        private Dictionary<int, TaskCompletionSource<bool>> _pageCompletionSources;
        private const int PageSize = 1000;
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

        public VirtualGridContext(IDataRepository repo)
        {
            _repo = repo;
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

        public async Task LoadAsync(string sql, object? parameters, string? initialSortColumn = null, CancellationToken cancellationToken = default)
        {
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
                string safeFilter = filterText.Replace("'", "''");
                foreach (var col in columns)
                {
                    if (_columnSqlTypes.TryGetValue(col, out string? type) && type != null && SafeStringTypes.Contains(type))
                    {
                        clauses.Add($"[{col}] LIKE '%{safeFilter}%'");
                    }
                    else
                    {
                        clauses.Add($"CAST([{col}] AS NVARCHAR(MAX)) LIKE '%{safeFilter}%'");
                    }
                }
                FilterText = string.Join(" OR ", clauses);
            }

            await ReloadAsync(cancellationToken);
        }

        public async Task<long> GetTotalMatchCountAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_rawFilterText) || _filterColumns == null || _filterColumns.Count == 0)
                return 0;

            var colTypes = new Dictionary<string, string?>();
            foreach(var col in _filterColumns)
            {
                if(_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetTotalMatchCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, _rawFilterText, colTypes, cancellationToken);
        }

        public async Task<long> CountMatchesAsync(string searchText, IEnumerable<string> columns, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText) || columns == null) return 0;

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in columns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetTotalMatchCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, searchText, colTypes, cancellationToken);
        }

        public async Task<long> GetTotalMatchedCellsCountAsync(string searchText, IEnumerable<string> columns, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText) || columns == null) return 0;

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in columns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetTotalMatchedCellsCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, searchText, colTypes, cancellationToken);
        }

        public async Task<long> GetPrecedingMatchedCellsCountAsync(int limitRowIndex, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_rawFilterText) || _filterColumns == null || _filterColumns.Count == 0 || limitRowIndex <= 0)
                return 0;

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in _filterColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetPrecedingMatchedCellsCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, _rawFilterText, colTypes, limitRowIndex, SortColumn, SortDirection, cancellationToken);
        }

        public async Task<long> GetPrecedingMatchCountAsync(int limitRowIndex, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_rawFilterText) || _filterColumns == null || _filterColumns.Count == 0 || limitRowIndex <= 0)
                return 0;

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in _filterColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return 0;

            return await _repo.GetPrecedingMatchCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, _rawFilterText, colTypes, limitRowIndex, SortColumn, SortDirection, cancellationToken);
        }

        public async Task WaitForRowAsync(int rowIndex)
        {
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

            try
            {
                _cache.Clear();
                _pagesBeingFetched.Clear();
                _pageCompletionSources.Clear();

                if (_server != null && _database != null && _baseSql != null)
                {
                    TotalCount = await _repo.GetQueryCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoadError?.Invoke(this, ex);
            }
        }

        public object? GetValue(int rowIndex, int colIndex)
        {
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
                        RequestPage(nextPageRowIndex);
                    }
                }
            }

            if (_cache.TryGetValue(rowIndex, out DataRow? row))
            {
                if (row != null && colIndex >= 0 && colIndex < row.Table.Columns.Count)
                    return row[colIndex];
                return "";
            }

            RequestPage(rowIndex);
            return null;
        }

        private async void RequestPage(int rowIndex)
        {
            int pageIndex = rowIndex / PageSize;

            if (_pagesBeingFetched.Contains(pageIndex)) return;

            int currentVersion = _version;
            _pagesBeingFetched.Add(pageIndex);

            try
            {
                int offset = pageIndex * PageSize;

                string? sortCol = SortColumn;
                string sortDir = SortDirection;
                string? filter = FilterText;

                if (_server != null && _database != null && _baseSql != null)
                {
                    var dt = await _repo.GetQueryPageAsync(_server, _database, _user, _pass, _baseSql, _parameters, offset, PageSize, sortCol, sortDir, filter);

                    if (_version != currentVersion) return;

                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        int absIndex = offset + i;
                        _cache[absIndex] = dt.Rows[i];
                    }

                    DataReady?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Page fetch error: " + ex.Message);
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

        public async Task ExportToCsvAsync(string filename, Dictionary<string, string>? headerMap = null, bool includeHeaders = true, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();
             if (_server == null || _database == null) return;

             using (var reader = await _repo.GetQueryDataReaderAsync(_server, _database, _user, _pass, finalSql, _parameters, cancellationToken))
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

                             if (!first) writer.Write(",");
                             first = false;

                             string header = (headerMap != null && headerMap.ContainsKey(colName)) ? headerMap[colName] : colName;
                             writer.Write("\"" + header.Replace("\"", "\"\"") + "\"");
                         }
                         writer.WriteLine();
                     }

                     while (await reader.ReadAsync(cancellationToken))
                     {
                         bool first = true;
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             string colName = reader.GetName(i);
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             if (!first) writer.Write(",");
                             first = false;

                             var val = reader.GetValue(i);
                             string sVal = val == DBNull.Value ? "" : val.ToString() ?? "";
                             writer.Write("\"" + sVal.Replace("\"", "\"\"") + "\"");
                         }
                         writer.WriteLine();
                     }
                 }
             }
        }

        public async Task ExportToJsonAsync(string filename, Dictionary<string, string>? headerMap = null, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();
             if (_server == null || _database == null) return;

             using (var reader = await _repo.GetQueryDataReaderAsync(_server, _database, _user, _pass, finalSql, _parameters, cancellationToken))
             {
                 using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                 using (var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true }))
                 {
                     writer.WriteStartArray();
                     while (await reader.ReadAsync(cancellationToken))
                     {
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
                 }
             }
        }

        public async Task ExportToXmlAsync(string filename, SearchCriteria criteria, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();
             if (_server == null || _database == null) return;

             string rootElement = "SearchResults";
             string rootAttr = "";

             if (criteria.Mode == SearchMode.Function)
             {
                 rootElement = "Function";
                 if (!string.IsNullOrEmpty(criteria.Value))
                     rootAttr = $" Number=\"{EscapeXml(criteria.Value)}\"";
             }
             else if (criteria.Mode == SearchMode.Totalizer)
             {
                 rootElement = "Totalizer";
                 if (!string.IsNullOrEmpty(criteria.Value))
                     rootAttr = $" Number=\"{EscapeXml(criteria.Value)}\"";
             }
             else if (criteria.Mode == SearchMode.Field && criteria.Type == SearchType.CustomSql)
             {
                 rootElement = "CustomQuery";
             }
             else if (criteria.Mode == SearchMode.Field && criteria.Type == SearchType.Table)
             {
                 rootElement = "Table";
                 if (!string.IsNullOrEmpty(criteria.Value))
                     rootAttr = $" Name=\"{EscapeXml(criteria.Value)}\"";
             }

             using (var reader = await _repo.GetQueryDataReaderAsync(_server, _database, _user, _pass, finalSql, _parameters, cancellationToken))
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
                         writer.WriteLine("  <Row>");
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             string colName = actualColNames[i];
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             var val = reader.GetValue(i);
                             if (val != DBNull.Value)
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
             }
        }

        private string MakeValidXmlName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Column";
            string safeName = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w_.-]", "_");
            if (!char.IsLetter(safeName[0]) && safeName[0] != '_') safeName = "_" + safeName;
            return safeName;
        }

        public async Task ExportToExcelXmlAsync(string filename, Dictionary<string, string>? headerMap = null, HashSet<string>? hiddenColumns = null, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();
             if (_server == null || _database == null) return;

             using (var reader = await _repo.GetQueryDataReaderAsync(_server, _database, _user, _pass, finalSql, _parameters, cancellationToken))
             {
                 using (var writer = new StreamWriter(filename))
                 {
                     writer.WriteLine("<?xml version=\"1.0\"?>");
                     writer.WriteLine("<?mso-application progid=\"Excel.Sheet\"?>");
                     writer.WriteLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                     writer.WriteLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
                     writer.WriteLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
                     writer.WriteLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                     writer.WriteLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
                     writer.WriteLine(" <Worksheet ss:Name=\"Sheet1\">");
                     writer.WriteLine("  <Table>");

                     writer.WriteLine("   <Row>");
                     List<string> actualColNames = new List<string>();
                     for (int i = 0; i < reader.FieldCount; i++)
                     {
                         string colName = reader.GetName(i);
                         actualColNames.Add(colName);
                         if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                         string header = (headerMap != null && headerMap.ContainsKey(colName)) ? headerMap[colName] : colName;
                         writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(header)}</Data></Cell>");
                     }
                     writer.WriteLine("   </Row>");

                     while (await reader.ReadAsync(cancellationToken))
                     {
                         writer.WriteLine("   <Row>");
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             string colName = actualColNames[i];
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             var val = reader.GetValue(i);
                             if (val == DBNull.Value)
                             {
                                 writer.WriteLine("    <Cell></Cell>");
                             }
                             else
                             {
                                 string? s = val.ToString();
                                 writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(s ?? "")}</Data></Cell>");
                             }
                         }
                         writer.WriteLine("   </Row>");
                     }

                     writer.WriteLine("  </Table>");
                     writer.WriteLine(" </Worksheet>");
                     writer.WriteLine("</Workbook>");
                 }
             }
        }

        public async Task ExportRowsToCsvAsync(string filename, List<VirtualRow> rows, HashSet<string>? hiddenColumns = null)
        {
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

                             writer.Write("\"" + props[i].Name.Replace("\"", "\"\"") + "\"");
                         }
                         writer.WriteLine();
                     }

                     foreach (var row in rows)
                     {
                         var props = row.GetProperties();
                         bool first = true;
                         for (int i = 0; i < props.Count; i++)
                         {
                             if (hiddenColumns != null && hiddenColumns.Contains(props[i].Name)) continue;

                             if (!first) writer.Write(",");
                             first = false;

                             var val = row.GetValue(i);
                             string sVal = val == DBNull.Value ? "" : val?.ToString() ?? "";
                             writer.Write("\"" + sVal.Replace("\"", "\"\"") + "\"");
                         }
                         writer.WriteLine();
                     }
                 }
             });
        }

        public async Task ExportRowsToJsonAsync(string filename, List<VirtualRow> rows, HashSet<string>? hiddenColumns = null)
        {
             await Task.Run(() =>
             {
                 using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                 using (var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true }))
                 {
                     writer.WriteStartArray();
                     foreach (var row in rows)
                     {
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
                 }
             });
        }

        public async Task ExportRowsToExcelXmlAsync(string filename, List<VirtualRow> rows, HashSet<string>? hiddenColumns = null)
        {
             await Task.Run(() =>
             {
                 using (var writer = new StreamWriter(filename))
                 {
                     writer.WriteLine("<?xml version=\"1.0\"?>");
                     writer.WriteLine("<?mso-application progid=\"Excel.Sheet\"?>");
                     writer.WriteLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                     writer.WriteLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
                     writer.WriteLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
                     writer.WriteLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                     writer.WriteLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
                     writer.WriteLine(" <Worksheet ss:Name=\"Sheet1\">");
                     writer.WriteLine("  <Table>");

                     if (rows.Count > 0)
                     {
                         writer.WriteLine("   <Row>");
                         var props = rows[0].GetProperties();
                         for (int i = 0; i < props.Count; i++)
                         {
                             string colName = props[i].Name;
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(colName)}</Data></Cell>");
                         }
                         writer.WriteLine("   </Row>");
                     }

                     foreach (var row in rows)
                     {
                         writer.WriteLine("   <Row>");
                         var props = row.GetProperties();
                         for (int i = 0; i < props.Count; i++)
                         {
                             string colName = props[i].Name;
                             if (hiddenColumns != null && hiddenColumns.Contains(colName)) continue;

                             var val = row.GetValue(i);
                             if (val == null || val == DBNull.Value)
                             {
                                 writer.WriteLine("    <Cell></Cell>");
                             }
                             else
                             {
                                 string? s = val.ToString();
                                 writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(s ?? "")}</Data></Cell>");
                             }
                         }
                         writer.WriteLine("   </Row>");
                     }

                     writer.WriteLine("  </Table>");
                     writer.WriteLine(" </Worksheet>");
                     writer.WriteLine("</Workbook>");
                 }
             });
        }

        public async Task ExportRowsToXmlAsync(string filename, List<VirtualRow> rows, SearchCriteria criteria, HashSet<string>? hiddenColumns = null)
        {
             await Task.Run(() =>
             {
                 string rootElement = "SearchResults";
                 string rootAttr = "";

                 if (criteria.Mode == SearchMode.Function)
                 {
                     rootElement = "Function";
                     if (!string.IsNullOrEmpty(criteria.Value))
                         rootAttr = $" Number=\"{EscapeXml(criteria.Value)}\"";
                 }
                 else if (criteria.Mode == SearchMode.Totalizer)
                 {
                     rootElement = "Totalizer";
                     if (!string.IsNullOrEmpty(criteria.Value))
                         rootAttr = $" Number=\"{EscapeXml(criteria.Value)}\"";
                 }
                 else if (criteria.Mode == SearchMode.Field && criteria.Type == SearchType.CustomSql)
                 {
                     rootElement = "CustomQuery";
                 }
                 else if (criteria.Mode == SearchMode.Field && criteria.Type == SearchType.Table)
                 {
                     rootElement = "Table";
                     if (!string.IsNullOrEmpty(criteria.Value))
                         rootAttr = $" Name=\"{EscapeXml(criteria.Value)}\"";
                 }

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
             });
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

        public async Task<int> FindMatchRowAsync(string searchText, IEnumerable<string> searchColumns, int startRowIndex, bool forward, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText) || searchColumns == null) return -1;

            var colTypes = new Dictionary<string, string?>();
            foreach (var col in searchColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string? type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            if (_server == null || _database == null || _baseSql == null) return -1;

            return await _repo.GetMatchRowIndexAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, searchText, colTypes, startRowIndex, SortColumn, SortDirection, forward, cancellationToken);
        }

        public async Task<List<string>> GetColumnDescriptionsAsync(IEnumerable<string> columns, CancellationToken cancellationToken = default)
        {
            if (_server == null || _database == null) return new List<string>();
            return await _repo.GetColumnDescriptionsAsync(_server, _database, _user, _pass, columns, cancellationToken);
        }
    }
}
