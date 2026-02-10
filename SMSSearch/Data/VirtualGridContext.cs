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

        private string _server;
        private string _database;
        private string _user;
        private string _pass;

        private string _baseSql;
        private object _parameters;

        public int TotalCount { get; private set; }
        public int UnfilteredCount { get; private set; }
        private Dictionary<int, DataRow> _cache;
        private HashSet<int> _pagesBeingFetched;
        private Dictionary<int, TaskCompletionSource<bool>> _pageCompletionSources;
        private const int PageSize = 100;
        private int _version = 0;

        public string SortColumn { get; private set; }
        public string SortDirection { get; private set; } = "ASC";
        public string FilterText { get; private set; }
        private string _rawFilterText;
        private List<string> _filterColumns;

        private Dictionary<string, string> _columnSqlTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SafeStringTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "char", "nchar", "varchar", "nvarchar", "text", "ntext", "sysname"
        };

        public event EventHandler DataReady;
        public event EventHandler<string> LoadError;

        public VirtualGridContext(IDataRepository repo)
        {
            _repo = repo;
            _cache = new Dictionary<int, DataRow>();
            _pagesBeingFetched = new HashSet<int>();
            _pageCompletionSources = new Dictionary<int, TaskCompletionSource<bool>>();
        }

        public void SetConnection(string server, string database, string user, string pass)
        {
            _server = server;
            _database = database;
            _user = user;
            _pass = pass;
        }

        public async Task LoadAsync(string sql, object parameters, string initialSortColumn = null, CancellationToken cancellationToken = default)
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
                    if (_columnSqlTypes.TryGetValue(col, out string type) && SafeStringTypes.Contains(type))
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

            var colTypes = new Dictionary<string, string>();
            foreach(var col in _filterColumns)
            {
                if(_columnSqlTypes.TryGetValue(col, out string type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            return await _repo.GetTotalMatchCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, _rawFilterText, colTypes, cancellationToken);
        }

        public async Task<long> GetPrecedingMatchCountAsync(int limitRowIndex, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_rawFilterText) || _filterColumns == null || _filterColumns.Count == 0 || limitRowIndex <= 0)
                return 0;

            var colTypes = new Dictionary<string, string>();
            foreach (var col in _filterColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            return await _repo.GetPrecedingMatchCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, _rawFilterText, colTypes, limitRowIndex, SortColumn, SortDirection, cancellationToken);
        }

        public async Task WaitForRowAsync(int rowIndex)
        {
            if (_cache.ContainsKey(rowIndex)) return;
            GetValue(rowIndex, 0); // Trigger fetch
            if (_cache.ContainsKey(rowIndex)) return;

            int pageIndex = rowIndex / PageSize;
            TaskCompletionSource<bool> tcs;

            if (!_pageCompletionSources.TryGetValue(pageIndex, out tcs))
            {
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pageCompletionSources[pageIndex] = tcs;
            }

            var timeoutTask = Task.Delay(5000);
            await Task.WhenAny(tcs.Task, timeoutTask);
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

                TotalCount = await _repo.GetQueryCountAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, cancellationToken);

                if (_version == currentVersion)
                {
                    DataReady?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoadError?.Invoke(this, ex.Message);
            }
        }

        public object GetValue(int rowIndex, int colIndex)
        {
            if (_cache.TryGetValue(rowIndex, out DataRow row))
            {
                if (colIndex >= 0 && colIndex < row.Table.Columns.Count)
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

                string sortCol = SortColumn;
                string sortDir = SortDirection;
                string filter = FilterText;

                var dt = await _repo.GetQueryPageAsync(_server, _database, _user, _pass, _baseSql, _parameters, offset, PageSize, sortCol, sortDir, filter);

                if (_version != currentVersion) return;

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    int absIndex = offset + i;
                    _cache[absIndex] = dt.Rows[i];
                }

                DataReady?.Invoke(this, EventArgs.Empty);
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

        public async Task<DataTable> GetSchemaAsync(string sql, object parameters, CancellationToken cancellationToken = default)
        {
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
             string finalSql = _baseSql;
             if (!string.IsNullOrWhiteSpace(FilterText))
             {
                 finalSql = $"SELECT * FROM ({_baseSql}) AS _FilterQ WHERE {FilterText}";
             }

             if (!string.IsNullOrEmpty(SortColumn))
             {
                string safeCol = SortColumn.Replace("[", "").Replace("]", "");
                if (string.IsNullOrWhiteSpace(FilterText))
                {
                    finalSql = $"SELECT * FROM ({finalSql}) AS _SortQ ORDER BY [{safeCol}] {SortDirection}";
                }
                else
                {
                    finalSql += $" ORDER BY [{safeCol}] {SortDirection}";
                }
             }
             return finalSql;
        }

        public async Task ExportToCsvAsync(string filename, Dictionary<string, string> headerMap = null, bool includeHeaders = true, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();

             using (var reader = await _repo.GetQueryDataReaderAsync(_server, _database, _user, _pass, finalSql, _parameters, cancellationToken))
             {
                 using (var writer = new StreamWriter(filename))
                 {
                     if (includeHeaders)
                     {
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             if (i > 0) writer.Write(",");
                             string colName = reader.GetName(i);
                             string header = (headerMap != null && headerMap.ContainsKey(colName)) ? headerMap[colName] : colName;
                             writer.Write("\"" + header.Replace("\"", "\"\"") + "\"");
                         }
                         writer.WriteLine();
                     }

                     while (await reader.ReadAsync(cancellationToken))
                     {
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             if (i > 0) writer.Write(",");
                             var val = reader.GetValue(i);
                             string sVal = val == DBNull.Value ? "" : val.ToString();
                             writer.Write("\"" + sVal.Replace("\"", "\"\"") + "\"");
                         }
                         writer.WriteLine();
                     }
                 }
             }
        }

        public async Task ExportToJsonAsync(string filename, Dictionary<string, string> headerMap = null, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();

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

        public async Task ExportToExcelXmlAsync(string filename, Dictionary<string, string> headerMap = null, CancellationToken cancellationToken = default)
        {
             string finalSql = BuildExportSql();

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
                     for (int i = 0; i < reader.FieldCount; i++)
                     {
                         string colName = reader.GetName(i);
                         string header = (headerMap != null && headerMap.ContainsKey(colName)) ? headerMap[colName] : colName;
                         writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(header)}</Data></Cell>");
                     }
                     writer.WriteLine("   </Row>");

                     while (await reader.ReadAsync(cancellationToken))
                     {
                         writer.WriteLine("   <Row>");
                         for (int i = 0; i < reader.FieldCount; i++)
                         {
                             var val = reader.GetValue(i);
                             if (val == DBNull.Value)
                             {
                                 writer.WriteLine("    <Cell></Cell>");
                             }
                             else
                             {
                                 writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(val.ToString())}</Data></Cell>");
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

            var colTypes = new Dictionary<string, string>();
            foreach (var col in searchColumns)
            {
                if (_columnSqlTypes.TryGetValue(col, out string type)) colTypes[col] = type;
                else colTypes[col] = null;
            }

            return await _repo.GetMatchRowIndexAsync(_server, _database, _user, _pass, _baseSql, _parameters, FilterText, searchText, colTypes, startRowIndex, SortColumn, SortDirection, forward, cancellationToken);
        }
    }
}
