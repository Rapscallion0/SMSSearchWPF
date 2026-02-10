using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SMS_Search.Utils;

namespace SMS_Search.Data
{
    public class DataRepository : IDataRepository
    {
        private readonly ILoggerService _logger;

        public DataRepository(ILoggerService logger)
        {
            _logger = logger;
        }

        private static readonly HashSet<string> SafeStringTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "char", "nchar", "varchar", "nvarchar", "text", "ntext", "sysname"
        };

        public string GetConnectionString(string server, string database, string user, string pass)
        {
            if (string.IsNullOrEmpty(user))
            {
                return $"Integrated Security=SSPI;Persist Security Info=False;Initial Catalog={database};Data Source={server}";
            }
            else
            {
                return $"Data Source={server};Initial Catalog={database};User ID={user};Password={pass};Persist Security Info=False;";
            }
        }

        public async Task<bool> TestConnectionAsync(string server, string database, string user, string pass, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
                {
                    await conn.OpenAsync(cancellationToken);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool TestConnection(string server, string database, string user, string pass)
        {
            try
            {
                using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
                {
                    conn.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<DataTable> ExecuteQueryAsync(string server, string database, string user, string pass, string sql, object parameters = null, CancellationToken cancellationToken = default)
        {
            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
                using (var reader = await conn.ExecuteReaderAsync(cmdDef))
                {
                    var dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                }
            }
        }

        public async Task<int> GetQueryCountAsync(string server, string database, string user, string pass, string sql, object parameters, string filter = null, CancellationToken cancellationToken = default)
        {
            string finalSql = ApplyFilter(sql, filter);
            string countSql = $"SELECT COUNT(*) FROM ({finalSql}) AS _CountQ";

            LogQuery("GetQueryCountAsync", countSql, parameters);

            try
            {
                using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
                {
                    await conn.OpenAsync(cancellationToken);
                    var cmdDef = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
                    int count = await conn.ExecuteScalarAsync<int>(cmdDef);
                    _logger.LogDebug($"GetQueryCountAsync: Returned {count}");
                    return count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetQueryCountAsync: Error: {ex.Message}");
                throw;
            }
        }

        public async Task<DataTable> GetQueryPageAsync(string server, string database, string user, string pass, string sql, object parameters, int offset, int limit, string sortCol, string sortDir, string filter = null, CancellationToken cancellationToken = default)
        {
            string finalSql = ApplyFilter(sql, filter);

            string orderBy = "(SELECT NULL)";
            if (!string.IsNullOrEmpty(sortCol))
            {
                string safeCol = sortCol.Replace("[", "").Replace("]", "");
                orderBy = $"[{safeCol}] {sortDir}";
            }

            string pageSql = $@"
                SELECT * FROM ({finalSql}) AS _PageQ
                ORDER BY {orderBy}
                OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(pageSql, parameters, cancellationToken: cancellationToken);
                using (var reader = await conn.ExecuteReaderAsync(cmdDef))
                {
                    var dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                }
            }
        }

        public async Task<DataTable> GetQuerySchemaAsync(string server, string database, string user, string pass, string sql, object parameters, CancellationToken cancellationToken = default)
        {
            string schemaSql = $"SELECT TOP 0 * FROM ({sql}) AS _SchemaQ";

            LogQuery("GetQuerySchemaAsync", schemaSql, parameters);

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(schemaSql, parameters, cancellationToken: cancellationToken);
                using (var reader = await conn.ExecuteReaderAsync(cmdDef))
                {
                    var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        typeMap[reader.GetName(i)] = reader.GetDataTypeName(i);
                    }

                    var dt = new DataTable();
                    dt.Load(reader);

                    foreach (DataColumn col in dt.Columns)
                    {
                        if (typeMap.TryGetValue(col.ColumnName, out string sqlType))
                        {
                            col.ExtendedProperties["SqlType"] = sqlType;
                        }
                    }

                    _logger.LogDebug($"GetQuerySchemaAsync: Returned {dt.Columns.Count} columns");
                    return dt;
                }
            }
        }

        private void LogQuery(string method, string sql, object parameters)
        {
            string paramLog = "";
            if (parameters is DynamicParameters dp)
            {
                var list = new List<string>();
                foreach (var name in dp.ParameterNames)
                {
                    var val = dp.Get<object>(name);
                    list.Add($"{name}={val}");
                }
                paramLog = string.Join(", ", list);
            }
            _logger.LogDebug($"{method}: Executing SQL: {sql} | Params: {paramLog}");
        }

        public async Task<DbDataReader> GetQueryDataReaderAsync(string server, string database, string user, string pass, string sql, object parameters, CancellationToken cancellationToken = default)
        {
            var conn = new SqlConnection(GetConnectionString(server, database, user, pass));
            await conn.OpenAsync(cancellationToken);

            using (var cmd = new SqlCommand(sql, conn))
            {
                if (parameters is DynamicParameters dp)
                {
                    foreach (var name in dp.ParameterNames)
                    {
                        var val = dp.Get<object>(name);
                        cmd.Parameters.AddWithValue(name, val ?? DBNull.Value);
                    }
                }

                return await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection, cancellationToken);
            }
        }

        private string ApplyFilter(string sql, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return sql;
            return $"SELECT * FROM ({sql}) AS _FilterQ WHERE {filter}";
        }

        public async Task<IEnumerable<string>> GetDatabasesAsync(string server, string user, string pass, CancellationToken cancellationToken = default)
        {
            using (var conn = new SqlConnection(GetConnectionString(server, "master", user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition("SELECT name FROM sys.databases ORDER BY name", cancellationToken: cancellationToken);
                return await conn.QueryAsync<string>(cmdDef);
            }
        }

        public async Task<IEnumerable<string>> GetTablesAsync(string server, string database, string user, string pass, CancellationToken cancellationToken = default)
        {
            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition("SELECT NAME FROM sys.tables ORDER BY NAME", cancellationToken: cancellationToken);
                return await conn.QueryAsync<string>(cmdDef);
            }
        }

        public async Task<List<string>> GetColumnDescriptionsAsync(string server, string database, string user, string pass, IEnumerable<string> columnNames, CancellationToken cancellationToken = default)
        {
             using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
             {
                 await conn.OpenAsync(cancellationToken);
                 string sql = "SELECT F1453 as Name, F1454 as Description FROM RB_FIELDS WHERE F1453 IN @Names";
                 var cmdDef = new CommandDefinition(sql, new { Names = columnNames }, cancellationToken: cancellationToken);
                 var result = await conn.QueryAsync(cmdDef);

                 var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                 foreach(var r in result)
                 {
                     dict[r.Name.ToString()] = r.Description.ToString();
                 }

                 var list = new List<string>();
                 foreach(var name in columnNames)
                 {
                     if (dict.TryGetValue(name, out string desc))
                         list.Add(desc);
                     else
                         list.Add("");
                 }
                 return list;
             }
        }

        public async Task<long> GetTotalMatchCountAsync(string server, string database, string user, string pass, string sql, object parameters, string filterClause, string filterText, Dictionary<string, string> columnTypes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filterText)) return 0;
            string safeFilter = filterText.Replace("'", "''");

            List<string> sumParts = new List<string>();
            foreach (var kvp in columnTypes)
            {
                string col = kvp.Key;
                string type = kvp.Value;
                if (type != null && SafeStringTypes.Contains(type))
                {
                    sumParts.Add($"(CASE WHEN [{col}] LIKE '%{safeFilter}%' THEN 1 ELSE 0 END)");
                }
                else
                {
                    sumParts.Add($"(CASE WHEN CAST([{col}] AS NVARCHAR(MAX)) LIKE '%{safeFilter}%' THEN 1 ELSE 0 END)");
                }
            }
            string sumExpression = string.Join(" + ", sumParts);

            string finalSql = ApplyFilter(sql, filterClause);
            string countSql = $"SELECT SUM((0 + {sumExpression})) FROM ({finalSql}) AS _CountQ";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
                var result = await conn.ExecuteScalarAsync<object>(cmdDef);
                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt64(result);
            }
        }

        public async Task<long> GetPrecedingMatchCountAsync(string server, string database, string user, string pass, string sql, object parameters, string filterClause, string filterText, Dictionary<string, string> columnTypes, int limitRowIndex, string sortCol, string sortDir, CancellationToken cancellationToken = default)
        {
            if (limitRowIndex <= 0) return 0;
            if (string.IsNullOrWhiteSpace(filterText)) return 0;
            string safeFilter = filterText.Replace("'", "''");

            List<string> sumParts = new List<string>();
            foreach (var kvp in columnTypes)
            {
                string col = kvp.Key;
                string type = kvp.Value;
                if (type != null && SafeStringTypes.Contains(type))
                {
                    sumParts.Add($"(CASE WHEN [{col}] LIKE '%{safeFilter}%' THEN 1 ELSE 0 END)");
                }
                else
                {
                    sumParts.Add($"(CASE WHEN CAST([{col}] AS NVARCHAR(MAX)) LIKE '%{safeFilter}%' THEN 1 ELSE 0 END)");
                }
            }
            string sumExpression = string.Join(" + ", sumParts);

            string finalSql = ApplyFilter(sql, filterClause);

            string orderBy = "(SELECT NULL)";
            if (!string.IsNullOrEmpty(sortCol))
            {
                string safeCol = sortCol.Replace("[", "").Replace("]", "");
                orderBy = $"[{safeCol}] {sortDir}";
            }

            string countSql = $@"
                SELECT SUM((0 + {sumExpression}))
                FROM (
                    SELECT * FROM ({finalSql}) AS _Base
                    ORDER BY {orderBy}
                    OFFSET 0 ROWS FETCH NEXT {limitRowIndex} ROWS ONLY
                ) AS _Preceding";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
                var result = await conn.ExecuteScalarAsync<object>(cmdDef);
                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt64(result);
            }
        }

        public async Task<int> GetMatchRowIndexAsync(string server, string database, string user, string pass, string sql, object parameters, string filterClause, string searchText, Dictionary<string, string> columnTypes, int startRowIndex, string sortCol, string sortDir, bool forward, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return -1;
            string safeFilter = searchText.Replace("'", "''");

            List<string> searchParts = new List<string>();
            foreach (var kvp in columnTypes)
            {
                string col = kvp.Key;
                string type = kvp.Value;
                if (type != null && SafeStringTypes.Contains(type))
                {
                    searchParts.Add($"[{col}] LIKE '%{safeFilter}%'");
                }
                else
                {
                    searchParts.Add($"CAST([{col}] AS NVARCHAR(MAX)) LIKE '%{safeFilter}%'");
                }
            }
            string searchExpression = string.Join(" OR ", searchParts);

            string finalSql = ApplyFilter(sql, filterClause);

            string orderBy = "(SELECT NULL)";
            if (!string.IsNullOrEmpty(sortCol))
            {
                string safeCol = sortCol.Replace("[", "").Replace("]", "");
                orderBy = $"[{safeCol}] {sortDir}";
            }

            string comparison = forward ? ">" : "<";
            string orderDirection = forward ? "ASC" : "DESC";

            string query = $@"
                SELECT TOP 1 RowNum
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY {orderBy}) - 1 as RowNum, *
                    FROM ({finalSql}) AS _Base
                ) AS _Ordered
                WHERE RowNum {comparison} {startRowIndex}
                AND ({searchExpression})
                ORDER BY RowNum {orderDirection}";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(query, parameters, cancellationToken: cancellationToken);
                var result = await conn.ExecuteScalarAsync<object>(cmdDef);
                if (result == null || result == DBNull.Value) return -1;
                return Convert.ToInt32(result);
            }
        }
    }
}
