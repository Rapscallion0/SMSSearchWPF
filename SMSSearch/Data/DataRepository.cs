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

        public string GetConnectionString(string server, string database, string? user, string? pass)
        {
            if (string.IsNullOrEmpty(user))
            {
                return $"Integrated Security=SSPI;Persist Security Info=False;Initial Catalog={database};Data Source={server};Encrypt=False";
            }
            else
            {
                return $"Data Source={server};Initial Catalog={database};User ID={user};Password={pass};Persist Security Info=False;Encrypt=False";
            }
        }

        public async Task<bool> TestConnectionAsync(string server, string database, string? user, string? pass, CancellationToken cancellationToken = default)
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

        public bool TestConnection(string server, string database, string? user, string? pass)
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

        public async Task<DataTable> ExecuteQueryAsync(string server, string database, string? user, string? pass, string sql, object? parameters = null, CancellationToken cancellationToken = default)
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

        public async Task<int> GetQueryCountAsync(string server, string database, string? user, string? pass, string sql, object? parameters, string? filter = null, CancellationToken cancellationToken = default)
        {
            string finalSql = ApplyFilter(sql, filter);
            string countSql = $"SELECT COUNT(*) FROM ({finalSql}) AS _CountQ";

            try
            {
                using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
                {
                    await conn.OpenAsync(cancellationToken);
                    var cmdDef = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);

                    int count = 0;
                    try
                    {
                        count = await conn.ExecuteScalarAsync<int>(cmdDef);
                    }
                    catch (SqlException ex) when (ex.Number == 1033)
                    {
                        // 1033: The ORDER BY clause is invalid in views, inline functions, derived tables, subqueries...
                        // This means the user provided an ORDER BY clause in their query.
                        // We can bypass this by executing the query as-is and wrapping it with an OFFSET.
                        string offsetFinalSql = ApplyFilter(sql, filter, true);
                        string offsetSql = $"SELECT COUNT(*) FROM ({offsetFinalSql}) AS _CountQ";
                        var offsetCmdDef = new CommandDefinition(offsetSql, parameters, cancellationToken: cancellationToken);
                        count = await conn.ExecuteScalarAsync<int>(offsetCmdDef);
                        countSql = offsetSql; // Update for logging
                    }

                    string paramLog = GetParamString(parameters);
                    _logger.LogInfo($"Query Executed. SQL: {countSql} | Params: {paramLog} | Results: {count}");

                    return count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetQueryCountAsync: Error: {ex.Message}");
                throw;
            }
        }

        public async Task<DataTable> GetQueryPageAsync(string server, string database, string? user, string? pass, string sql, object? parameters, int offset, int limit, string? sortCol, string? sortDir, string? filter = null, CancellationToken cancellationToken = default)
        {
            string finalSql = ApplyFilter(sql, filter);

            string orderBy = "(SELECT NULL)";
            if (!string.IsNullOrEmpty(sortCol))
            {
                string safeCol = sortCol.Replace("[", "").Replace("]", "");
                if (safeCol.Equals("Field", StringComparison.OrdinalIgnoreCase))
                {
                    orderBy = $"TRY_CAST(SUBSTRING([{safeCol}], 2, LEN([{safeCol}])) AS INT) {sortDir}, [{safeCol}] {sortDir}";
                }
                else
                {
                    orderBy = $"[{safeCol}] {sortDir}";
                }
            }

            string pageSql = $@"
                SELECT * FROM ({finalSql}) AS _PageQ
                ORDER BY {orderBy}
                OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(pageSql, parameters, cancellationToken: cancellationToken);
                DbDataReader reader;
                try
                {
                    reader = await conn.ExecuteReaderAsync(cmdDef);
                }
                catch (SqlException ex) when (ex.Number == 1033)
                {
                    string offsetFinalSql = ApplyFilter(sql, filter, true);
                    string offsetPageSql = $@"
                        SELECT * FROM ({offsetFinalSql}) AS _PageQ
                        ORDER BY {orderBy}
                        OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";
                    var offsetCmdDef = new CommandDefinition(offsetPageSql, parameters, cancellationToken: cancellationToken);
                    reader = await conn.ExecuteReaderAsync(offsetCmdDef);
                }

                using (reader)
                {
                    var dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                }
            }
        }

        public async Task<DataTable> GetQuerySchemaAsync(string server, string database, string? user, string? pass, string sql, object? parameters, CancellationToken cancellationToken = default)
        {
            string schemaSql = $"SELECT TOP 0 * FROM ({sql}) AS _SchemaQ";

            LogQuery("GetQuerySchemaAsync", schemaSql, parameters);

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(schemaSql, parameters, cancellationToken: cancellationToken);

                DbDataReader reader;
                try
                {
                    reader = await conn.ExecuteReaderAsync(cmdDef);
                }
                catch (SqlException ex) when (ex.Number == 1033)
                {
                    string offsetFinalSql = ApplyFilter(sql, null, true);
                    string offsetSchemaSql = $"SELECT TOP 0 * FROM ({offsetFinalSql}) AS _SchemaQ";
                    var offsetCmdDef = new CommandDefinition(offsetSchemaSql, parameters, cancellationToken: cancellationToken);
                    reader = await conn.ExecuteReaderAsync(offsetCmdDef);
                }

                using (reader)
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
                        if (typeMap.TryGetValue(col.ColumnName, out string? sqlType))
                        {
                            col.ExtendedProperties["SqlType"] = sqlType;
                        }
                    }

                    _logger.LogDebug($"GetQuerySchemaAsync: Returned {dt.Columns.Count} columns");
                    return dt;
                }
            }
        }

        private void LogQuery(string method, string sql, object? parameters)
        {
            string paramLog = GetParamString(parameters);
            _logger.LogDebug($"{method}: Executing SQL: {sql} | Params: {paramLog}");
        }

        private string GetParamString(object? parameters)
        {
            if (parameters is DynamicParameters dp)
            {
                var list = new List<string>();
                foreach (var name in dp.ParameterNames)
                {
                    var val = dp.Get<object>(name);
                    list.Add($"{name}={val}");
                }
                return string.Join(", ", list);
            }
            return "";
        }

        public async Task<DbDataReader> GetQueryDataReaderAsync(string server, string database, string? user, string? pass, string sql, object? parameters, CancellationToken cancellationToken = default)
        {
            var conn = new SqlConnection(GetConnectionString(server, database, user, pass));
            await conn.OpenAsync(cancellationToken);

            var cmd = new SqlCommand(sql, conn);
            try
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
            catch
            {
                cmd.Dispose();
                conn.Dispose();
                throw;
            }
        }

        private string ApplyFilter(string sql, string? filter, bool useOffset = false)
        {
            string innerSql = sql;
            if (useOffset)
            {
                innerSql = $"{sql} OFFSET 0 ROWS";
            }
            if (string.IsNullOrWhiteSpace(filter)) return innerSql;
            return $"SELECT * FROM ({innerSql}) AS _FilterQ WHERE {filter}";
        }

        public async Task<IEnumerable<string>> GetServersAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var servers = new List<string>();
                try
                {
                    var instance = Microsoft.Data.Sql.SqlDataSourceEnumerator.Instance;
                    var table = instance.GetDataSources();
                    foreach (System.Data.DataRow row in table.Rows)
                    {
                        string serverName = row["ServerName"]?.ToString() ?? "";
                        string instanceName = row["InstanceName"]?.ToString() ?? "";

                        if (string.IsNullOrEmpty(instanceName))
                        {
                            servers.Add(serverName);
                        }
                        else
                        {
                            servers.Add($"{serverName}\\{instanceName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error discovering SQL Servers", ex);
                }
                return servers.OrderBy(s => s).Distinct();
            }, cancellationToken);
        }

        public async Task<IEnumerable<string>> GetDatabasesAsync(string server, string? user, string? pass, CancellationToken cancellationToken = default)
        {
            using (var conn = new SqlConnection(GetConnectionString(server, "master", user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition("SELECT name FROM sys.databases ORDER BY name", cancellationToken: cancellationToken);
                return await conn.QueryAsync<string>(cmdDef);
            }
        }

        public async Task<IEnumerable<string>> GetTablesAsync(string server, string database, string? user, string? pass, CancellationToken cancellationToken = default)
        {
            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition("SELECT NAME FROM sys.tables ORDER BY NAME", cancellationToken: cancellationToken);
                return await conn.QueryAsync<string>(cmdDef);
            }
        }

        public async Task<List<string>> GetColumnDescriptionsAsync(string server, string database, string? user, string? pass, IEnumerable<string> columnNames, CancellationToken cancellationToken = default)
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
                     if (dict.TryGetValue(name, out string? desc))
                         list.Add(desc ?? "");
                     else
                         list.Add("");
                 }
                 return list;
             }
        }

        public async Task<Dictionary<string, List<string>>> GetDatabaseSchemaAsync(string server, string database, string? user, string? pass, CancellationToken cancellationToken = default)
        {
            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                string sql = @"
                    SELECT t.name as TableName, c.name as ColumnName
                    FROM sys.tables t
                    INNER JOIN sys.columns c ON t.object_id = c.object_id
                    ORDER BY t.name, c.name";

                var cmdDef = new CommandDefinition(sql, cancellationToken: cancellationToken);
                var rows = await conn.QueryAsync(cmdDef);

                var schema = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    string tableName = row.TableName;
                    string columnName = row.ColumnName;

                    if (!schema.TryGetValue(tableName, out var columns))
                    {
                        columns = new List<string>();
                        schema[tableName] = columns;
                    }
                    columns.Add(columnName);
                }
                return schema;
            }
        }

        public async Task<long> GetTotalMatchCountAsync(string server, string database, string? user, string? pass, string sql, object? parameters, string? filterClause, string? filterText, Dictionary<string, string?> columnTypes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filterText)) return 0;
            string safeFilter = filterText.Replace("'", "''");

            List<string> whereParts = new List<string>();
            foreach (var kvp in columnTypes)
            {
                string col = kvp.Key;
                string? type = kvp.Value;
                if (type != null && SafeStringTypes.Contains(type))
                {
                    whereParts.Add($"[{col}] LIKE '%{safeFilter}%'");
                }
                else
                {
                    whereParts.Add($"CAST([{col}] AS NVARCHAR(MAX)) LIKE '%{safeFilter}%'");
                }
            }
            string whereExpression = string.Join(" OR ", whereParts);

            string finalSql = ApplyFilter(sql, filterClause);
            string countSql = $"SELECT COUNT(*) FROM ({finalSql}) AS _CountQ WHERE ({whereExpression})";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
                object? result;
                try
                {
                    result = await conn.ExecuteScalarAsync<object>(cmdDef);
                }
                catch (SqlException ex) when (ex.Number == 1033)
                {
                    string offsetFinalSql = ApplyFilter(sql, filterClause, true);
                    string offsetSql = $"SELECT COUNT(*) FROM ({offsetFinalSql}) AS _CountQ WHERE ({whereExpression})";
                    var offsetCmdDef = new CommandDefinition(offsetSql, parameters, cancellationToken: cancellationToken);
                    result = await conn.ExecuteScalarAsync<object>(offsetCmdDef);
                }

                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt64(result);
            }
        }

        public async Task<long> GetTotalMatchedCellsCountAsync(string server, string database, string? user, string? pass, string sql, object? parameters, string? filterClause, string? filterText, Dictionary<string, string?> columnTypes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filterText)) return 0;
            string safeFilter = filterText.Replace("'", "''");

            List<string> whereParts = new List<string>();
            List<string> sumParts = new List<string>();

            foreach (var kvp in columnTypes)
            {
                string col = kvp.Key;
                string? type = kvp.Value;
                string condition;

                if (type != null && SafeStringTypes.Contains(type))
                {
                    condition = $"[{col}] LIKE '%{safeFilter}%'";
                }
                else
                {
                    condition = $"CAST([{col}] AS NVARCHAR(MAX)) LIKE '%{safeFilter}%'";
                }

                whereParts.Add(condition);
                sumParts.Add($"(CASE WHEN {condition} THEN 1 ELSE 0 END)");
            }
            string whereExpression = string.Join(" OR ", whereParts);
            string sumExpression = string.Join(" + ", sumParts);

            string finalSql = ApplyFilter(sql, filterClause);
            string countSql = $"SELECT SUM({sumExpression}) FROM ({finalSql}) AS _CountQ WHERE ({whereExpression})";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
                object? result;
                try
                {
                    result = await conn.ExecuteScalarAsync<object>(cmdDef);
                }
                catch (SqlException ex) when (ex.Number == 1033)
                {
                    string offsetFinalSql = ApplyFilter(sql, filterClause, true);
                    string offsetSql = $"SELECT SUM({sumExpression}) FROM ({offsetFinalSql}) AS _CountQ WHERE ({whereExpression})";
                    var offsetCmdDef = new CommandDefinition(offsetSql, parameters, cancellationToken: cancellationToken);
                    result = await conn.ExecuteScalarAsync<object>(offsetCmdDef);
                }

                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt64(result);
            }
        }

        public async Task<long> GetPrecedingMatchedCellsCountAsync(string server, string database, string? user, string? pass, string sql, object? parameters, string? filterClause, string? filterText, Dictionary<string, string?> columnTypes, int limitRowIndex, string? sortCol, string? sortDir, CancellationToken cancellationToken = default)
        {
            if (limitRowIndex <= 0) return 0;
            if (string.IsNullOrWhiteSpace(filterText)) return 0;
            string safeFilter = filterText.Replace("'", "''");

            List<string> whereParts = new List<string>();
            List<string> sumParts = new List<string>();

            foreach (var kvp in columnTypes)
            {
                string col = kvp.Key;
                string? type = kvp.Value;
                string condition;

                if (type != null && SafeStringTypes.Contains(type))
                {
                    condition = $"[{col}] LIKE '%{safeFilter}%'";
                }
                else
                {
                    condition = $"CAST([{col}] AS NVARCHAR(MAX)) LIKE '%{safeFilter}%'";
                }

                whereParts.Add(condition);
                sumParts.Add($"(CASE WHEN {condition} THEN 1 ELSE 0 END)");
            }
            string whereExpression = string.Join(" OR ", whereParts);
            string sumExpression = string.Join(" + ", sumParts);

            string finalSql = ApplyFilter(sql, filterClause);

            string orderBy = "(SELECT NULL)";
            if (!string.IsNullOrEmpty(sortCol))
            {
                string safeCol = sortCol.Replace("[", "").Replace("]", "");
                if (safeCol.Equals("Field", StringComparison.OrdinalIgnoreCase))
                {
                    orderBy = $"TRY_CAST(SUBSTRING([{safeCol}], 2, LEN([{safeCol}])) AS INT) {sortDir}, [{safeCol}] {sortDir}";
                }
                else
                {
                    orderBy = $"[{safeCol}] {sortDir}";
                }
            }

            string countSql = $@"
                SELECT SUM({sumExpression})
                FROM (
                    SELECT * FROM ({finalSql}) AS _Base
                    ORDER BY {orderBy}
                    OFFSET 0 ROWS FETCH NEXT {limitRowIndex} ROWS ONLY
                ) AS _Preceding
                WHERE ({whereExpression})";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
                object? result;
                try
                {
                    result = await conn.ExecuteScalarAsync<object>(cmdDef);
                }
                catch (SqlException ex) when (ex.Number == 1033)
                {
                    string offsetFinalSql = ApplyFilter(sql, filterClause, true);
                    string offsetSql = $@"
                        SELECT SUM({sumExpression})
                        FROM (
                            SELECT * FROM ({offsetFinalSql}) AS _Base
                            ORDER BY {orderBy}
                            OFFSET 0 ROWS FETCH NEXT {limitRowIndex} ROWS ONLY
                        ) AS _Preceding
                        WHERE ({whereExpression})";
                    var offsetCmdDef = new CommandDefinition(offsetSql, parameters, cancellationToken: cancellationToken);
                    result = await conn.ExecuteScalarAsync<object>(offsetCmdDef);
                }

                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt64(result);
            }
        }

        public async Task<long> GetPrecedingMatchCountAsync(string server, string database, string? user, string? pass, string sql, object? parameters, string? filterClause, string? filterText, Dictionary<string, string?> columnTypes, int limitRowIndex, string? sortCol, string? sortDir, CancellationToken cancellationToken = default)
        {
            if (limitRowIndex <= 0) return 0;
            if (string.IsNullOrWhiteSpace(filterText)) return 0;
            string safeFilter = filterText.Replace("'", "''");

            List<string> whereParts = new List<string>();
            foreach (var kvp in columnTypes)
            {
                string col = kvp.Key;
                string? type = kvp.Value;
                if (type != null && SafeStringTypes.Contains(type))
                {
                    whereParts.Add($"[{col}] LIKE '%{safeFilter}%'");
                }
                else
                {
                    whereParts.Add($"CAST([{col}] AS NVARCHAR(MAX)) LIKE '%{safeFilter}%'");
                }
            }
            string whereExpression = string.Join(" OR ", whereParts);

            string finalSql = ApplyFilter(sql, filterClause);

            string orderBy = "(SELECT NULL)";
            if (!string.IsNullOrEmpty(sortCol))
            {
                string safeCol = sortCol.Replace("[", "").Replace("]", "");
                if (safeCol.Equals("Field", StringComparison.OrdinalIgnoreCase))
                {
                    orderBy = $"TRY_CAST(SUBSTRING([{safeCol}], 2, LEN([{safeCol}])) AS INT) {sortDir}, [{safeCol}] {sortDir}";
                }
                else
                {
                    orderBy = $"[{safeCol}] {sortDir}";
                }
            }

            string countSql = $@"
                SELECT COUNT(*)
                FROM (
                    SELECT * FROM ({finalSql}) AS _Base
                    ORDER BY {orderBy}
                    OFFSET 0 ROWS FETCH NEXT {limitRowIndex} ROWS ONLY
                ) AS _Preceding
                WHERE ({whereExpression})";

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmdDef = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
                object? result;
                try
                {
                    result = await conn.ExecuteScalarAsync<object>(cmdDef);
                }
                catch (SqlException ex) when (ex.Number == 1033)
                {
                    string offsetFinalSql = ApplyFilter(sql, filterClause, true);
                    string offsetSql = $@"
                        SELECT COUNT(*)
                        FROM (
                            SELECT * FROM ({offsetFinalSql}) AS _Base
                            ORDER BY {orderBy}
                            OFFSET 0 ROWS FETCH NEXT {limitRowIndex} ROWS ONLY
                        ) AS _Preceding
                        WHERE ({whereExpression})";
                    var offsetCmdDef = new CommandDefinition(offsetSql, parameters, cancellationToken: cancellationToken);
                    result = await conn.ExecuteScalarAsync<object>(offsetCmdDef);
                }

                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt64(result);
            }
        }

        public async Task<List<string>> GetAllMatchesAsync(string server, string database, string? user, string? pass, string sql, object? parameters, string? filterClause, string searchText, Dictionary<string, string?> columnTypes, CancellationToken cancellationToken = default)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(searchText)) return results;

            string finalSql = ApplyFilter(sql, filterClause);

            using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                var cmd = new SqlCommand(finalSql, conn);
                if (parameters is DynamicParameters dp)
                {
                    foreach (var name in dp.ParameterNames)
                    {
                        var val = dp.Get<object>(name);
                        cmd.Parameters.AddWithValue(name, val ?? DBNull.Value);
                    }
                }

                try
                {
                    using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (!columnTypes.ContainsKey(colName)) continue;

                                var val = reader.GetValue(i);
                                if (val != DBNull.Value)
                                {
                                    string sVal = val.ToString() ?? "";
                                    if (sVal.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        results.Add(sVal);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (SqlException ex) when (ex.Number == 1033)
                {
                    string offsetFinalSql = ApplyFilter(sql, filterClause, true);
                    cmd.CommandText = $"SELECT * FROM ({offsetFinalSql}) AS _Base";
                    using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (!columnTypes.ContainsKey(colName)) continue;

                                var val = reader.GetValue(i);
                                if (val != DBNull.Value)
                                {
                                    string sVal = val.ToString() ?? "";
                                    if (sVal.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        results.Add(sVal);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }

        public async Task<string?> GetDatabaseTemplateAsync(string server, string database, string? user, string? pass, CancellationToken cancellationToken = default)
        {
            try
            {
                string sql = "SELECT CAST(value AS NVARCHAR(MAX)) FROM sys.extended_properties WHERE name = 'OriginalTemplateDB'";
                using (var conn = new SqlConnection(GetConnectionString(server, database, user, pass)))
                {
                    await conn.OpenAsync(cancellationToken);
                    return await conn.ExecuteScalarAsync<string>(new CommandDefinition(sql, cancellationToken: cancellationToken));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetDatabaseTemplateAsync failed for db {database}", ex);
                return null;
            }
        }

        public async Task DropDatabaseAsync(string server, string database, string? user, string? pass, CancellationToken cancellationToken = default)
        {
            string safeDb = database.Replace("]", "]]");
            string sql = $@"
                ALTER DATABASE [{safeDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{safeDb}];
            ";
            using (var conn = new SqlConnection(GetConnectionString(server, "master", user, pass)))
            {
                await conn.OpenAsync(cancellationToken);
                await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            }
        }

        public async Task PerformImportProcessAsync(string server, string? user, string? pass, string targetDatabase, string templateDatabase, List<string> sqlFiles, Action<ViewModels.ImportProgressInfo> progressCallback, Func<string, Task<Services.ExistingTableAction>> tableExistsPromptCallback, Func<string, List<Models.MissingColumnInfo>, Task<Models.MissingColumnDialogResult>> missingColumnsPromptCallback, CancellationToken cancellationToken = default)
        {
            try
            {
                // Sanitize database names to prevent SQL injection in DDL statements
                string safeTargetDb = targetDatabase.Replace("]", "]]");
                string safeTemplateDb = templateDatabase.Replace("]", "]]");

                progressCallback(new ViewModels.ImportProgressInfo { IsIndeterminate = true, Message = $"Connecting to server..." });

                // 1. Check if Target DB exists
                bool dbExists = false;
                using (var connMaster = new SqlConnection(GetConnectionString(server, "master", user, pass)))
                {
                    await connMaster.OpenAsync(cancellationToken);
                    dbExists = await connMaster.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM sys.databases WHERE name = @name",
                        new { name = targetDatabase }) > 0;

                    if (!dbExists)
                    {
                        progressCallback(new ViewModels.ImportProgressInfo { IsIndeterminate = true, Message = $"Creating database '{targetDatabase}'..." });
                        // Cannot parameterize CREATE DATABASE
                        await connMaster.ExecuteAsync(new CommandDefinition($"CREATE DATABASE [{safeTargetDb}]", cancellationToken: cancellationToken));
                    }
                }

                if (!dbExists)
                {
                    // Add extended property for OriginalTemplateDB
                    using (var connTarget = new SqlConnection(GetConnectionString(server, targetDatabase, user, pass)))
                    {
                        await connTarget.OpenAsync(cancellationToken);
                        string addPropSql = @"
                            EXEC sys.sp_addextendedproperty
                                @name = N'OriginalTemplateDB',
                                @value = @TemplateDbName;";
                        await connTarget.ExecuteAsync(new CommandDefinition(addPropSql, new { TemplateDbName = templateDatabase }, cancellationToken: cancellationToken));
                    }
                }

                // 2. Base tables to copy from Template DB to Target DB
                string[] baseTables = new[] { "RB_FIELDS", "RB_TABLES", "INFORMATION_SCHEMA.KEY_COLUMN_USAGE" };

                int totalSteps = baseTables.Length + sqlFiles.Count;
                int currentStep = 0;

                foreach (var table in baseTables)
                {
                    progressCallback(new ViewModels.ImportProgressInfo
                    {
                        IsIndeterminate = false,
                        Percentage = ((double)currentStep / totalSteps) * 100,
                        Message = $"Copying base schema: {table}"
                    });

                    // We need to copy these into the target database.
                    // For standard tables we can use SELECT * INTO
                    // For INFORMATION_SCHEMA we can't do SELECT * INTO Target.INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    // So we skip system views if requested, but user specifically asked for INFORMATION_SCHEMA.KEY_COLUMN_USAGE.
                    // Let's create a snapshot of it in dbo if they want it.
                    string targetTableName = table.Contains(".") ? table.Split('.')[1] : table;
                    string sourceTableName = table.Contains(".") ? table : $"[dbo].[{table}]";

                    using (var targetConn = new SqlConnection(GetConnectionString(server, targetDatabase, user, pass)))
                    {
                        await targetConn.OpenAsync(cancellationToken);

                        // Check if it already exists
                        bool tableExists = await targetConn.ExecuteScalarAsync<int>(
                            "SELECT COUNT(*) FROM sys.tables WHERE name = @name",
                            new { name = targetTableName }) > 0;

                        if (!tableExists)
                        {
                            try
                            {
                                string copySql = $"SELECT * INTO [{safeTargetDb}].[dbo].[{targetTableName}] FROM [{safeTemplateDb}].{sourceTableName}";
                                await targetConn.ExecuteAsync(new CommandDefinition(copySql, cancellationToken: cancellationToken));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to copy base table {table}: {ex.Message}");
                            }
                        }
                    }
                    currentStep++;
                }

                // 3. Process each SQL file
                bool skipAll = false;
                bool recreateAll = false;

                foreach (var file in sqlFiles)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    progressCallback(new ViewModels.ImportProgressInfo
                    {
                        IsIndeterminate = false,
                        Percentage = ((double)currentStep / totalSteps) * 100,
                        Message = $"Processing {fileName}..."
                    });

                    string fileContent = await System.IO.File.ReadAllTextAsync(file, cancellationToken);

                    // Parse the file
                    var parsed = ParseSqlImportFile(fileContent);
                    bool skipFileExecution = false;

                    using (var targetConn = new SqlConnection(GetConnectionString(server, targetDatabase, user, pass)))
                    {
                        await targetConn.OpenAsync(cancellationToken);

                        if (!string.IsNullOrEmpty(parsed.TableName))
                        {
                            // Copy table structure from template DB if it doesn't exist
                            bool tableExists = await targetConn.ExecuteScalarAsync<int>(
                                "SELECT COUNT(*) FROM sys.tables WHERE name = @name",
                                new { name = parsed.TableName }) > 0;

                            if (tableExists)
                            {
                                Services.ExistingTableAction action;
                                if (skipAll)
                                {
                                    action = Services.ExistingTableAction.Skip;
                                }
                                else if (recreateAll)
                                {
                                    action = Services.ExistingTableAction.Recreate;
                                }
                                else
                                {
                                    action = await tableExistsPromptCallback(parsed.TableName);
                                    if (action == Services.ExistingTableAction.SkipAll)
                                    {
                                        skipAll = true;
                                        action = Services.ExistingTableAction.Skip;
                                    }
                                    else if (action == Services.ExistingTableAction.RecreateAll)
                                    {
                                        recreateAll = true;
                                        action = Services.ExistingTableAction.Recreate;
                                    }
                                }

                                if (action == Services.ExistingTableAction.Skip)
                                {
                                    skipFileExecution = true;
                                }
                                else if (action == Services.ExistingTableAction.Recreate)
                                {
                                    progressCallback(new ViewModels.ImportProgressInfo
                                    {
                                        IsIndeterminate = false,
                                        Percentage = ((double)currentStep / totalSteps) * 100,
                                        Message = $"Recreating structure for {parsed.TableName}..."
                                    });

                                    // Drop the existing table
                                    await targetConn.ExecuteAsync(new CommandDefinition($"DROP TABLE [{safeTargetDb}].[dbo].[{parsed.TableName}]", cancellationToken: cancellationToken));

                                    // Re-copy from template
                                    try
                                    {
                                        string copyStructSql = $"SELECT * INTO [{safeTargetDb}].[dbo].[{parsed.TableName}] FROM [{safeTemplateDb}].[dbo].[{parsed.TableName}] WHERE 1=0";
                                        await targetConn.ExecuteAsync(new CommandDefinition(copyStructSql, cancellationToken: cancellationToken));
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning($"Failed to recreate structure for {parsed.TableName}: {ex.Message}");
                                        throw new Exception($"Could not copy the required structure for table {parsed.TableName} from template database. Ensure the table exists in {templateDatabase}.");
                                    }
                                }
                            }
                            else
                            {
                                progressCallback(new ViewModels.ImportProgressInfo
                                {
                                    IsIndeterminate = false,
                                    Percentage = ((double)currentStep / totalSteps) * 100,
                                    Message = $"Copying structure for {parsed.TableName}..."
                                });

                                try
                                {
                                    string copyStructSql = $"SELECT * INTO [{safeTargetDb}].[dbo].[{parsed.TableName}] FROM [{safeTemplateDb}].[dbo].[{parsed.TableName}] WHERE 1=0";
                                    await targetConn.ExecuteAsync(new CommandDefinition(copyStructSql, cancellationToken: cancellationToken));
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning($"Failed to copy structure for {parsed.TableName}: {ex.Message}");
                                    throw new Exception($"Could not copy the required structure for table {parsed.TableName} from template database. Ensure the table exists in {templateDatabase}.");
                                }
                            }
                        }

                        // Execute the sanitized SQL
                        if (parsed.Statements.Count > 0 && !skipFileExecution)
                        {
                            progressCallback(new ViewModels.ImportProgressInfo
                            {
                                IsIndeterminate = false,
                                Percentage = ((double)currentStep / totalSteps) * 100,
                                Message = $"Executing script for {fileName}..."
                            });

                            // Cache for table column types
                            var tableColumnDateTypesCache = new Dictionary<string, List<bool>>();
                            var tableColumnNamesCache = new Dictionary<string, List<string>>();

                            foreach (var stmt in parsed.Statements)
                            {
                                if (string.IsNullOrWhiteSpace(stmt)) continue;

                                string processedStmt = stmt;

                                // If it's an INSERT statement, process Julian dates based on column types
                                if (processedStmt.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(processedStmt, @"INSERT\s+INTO\s+([^\s\(]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    if (match.Success)
                                    {
                                        string tableName = match.Groups[1].Value.Replace("[", "").Replace("]", "");

                                        if (!tableColumnDateTypesCache.TryGetValue(tableName, out List<bool>? isDateColumn))
                                        {
                                            isDateColumn = new List<bool>();
                                            var columnNames = new List<string>();
                                            try
                                            {
                                                var cmdDef = new CommandDefinition($"SELECT TOP 0 * FROM [{tableName}]", cancellationToken: cancellationToken);
                                                using (var reader = await targetConn.ExecuteReaderAsync(cmdDef))
                                                {
                                                    for (int i = 0; i < reader.FieldCount; i++)
                                                    {
                                                        string typeName = reader.GetDataTypeName(i).ToLowerInvariant();
                                                        isDateColumn.Add(typeName == "datetime" || typeName == "date" || typeName == "datetime2" || typeName == "smalldatetime");
                                                        columnNames.Add(reader.GetName(i));
                                                    }
                                                }
                                                tableColumnDateTypesCache[tableName] = isDateColumn;
                                                tableColumnNamesCache[tableName] = columnNames;
                                            }
                                            catch
                                            {
                                                // If table doesn't exist or other error, just don't do Julian conversions
                                                tableColumnDateTypesCache[tableName] = new List<bool>();
                                                tableColumnNamesCache[tableName] = new List<string>();
                                            }
                                        }

                                        if (isDateColumn != null && isDateColumn.Count > 0)
                                        {
                                            // Handle potential column mapping: INSERT INTO TABLE (col1, col2) VALUES (...)
                                            List<bool> mappedIsDateColumn = isDateColumn;

                                            var colMatch = System.Text.RegularExpressions.Regex.Match(processedStmt, @"INSERT\s+INTO\s+[^\s\(]+\s*\((.*?)\)\s*VALUES", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                            if (colMatch.Success && tableColumnNamesCache.TryGetValue(tableName, out var columnNames) && columnNames.Count > 0)
                                            {
                                                string colsStr = colMatch.Groups[1].Value;
                                                var cols = colsStr.Split(',').Select(c => c.Trim().Trim('[', ']', '"', '\'')).ToList();

                                                mappedIsDateColumn = new List<bool>();
                                                foreach (var c in cols)
                                                {
                                                    int idx = columnNames.FindIndex(tc => tc.Equals(c, StringComparison.OrdinalIgnoreCase));
                                                    if (idx >= 0 && idx < isDateColumn.Count)
                                                    {
                                                        mappedIsDateColumn.Add(isDateColumn[idx]);
                                                    }
                                                    else
                                                    {
                                                        mappedIsDateColumn.Add(false);
                                                    }
                                                }
                                            }

                                            processedStmt = ProcessJulianDatesInInsert(processedStmt, mappedIsDateColumn);
                                        }
                                    }
                                }

                                try
                                {
                                    await targetConn.ExecuteAsync(new CommandDefinition(processedStmt, cancellationToken: cancellationToken));
                                }
                                catch (SqlException sqlEx) when (sqlEx.Number == 207)
                                {
                                    // Error 207: Invalid column name.
                                    var viewMatch = System.Text.RegularExpressions.Regex.Match(processedStmt, @"CREATE\s+VIEW\s+(?:\[?[a-zA-Z0-9_]+\]?)\s+AS\s+SELECT\s+(.+?)\s+FROM\s+\[?([a-zA-Z0-9_]+)\]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    if (viewMatch.Success)
                                    {
                                        string viewColsStr = viewMatch.Groups[1].Value;
                                        string tableName = viewMatch.Groups[2].Value;
                                        var viewCols = viewColsStr.Split(',').Select(c => c.Trim().Trim('[', ']')).ToList();

                                        var existingCols = new List<string>();
                                        try
                                        {
                                            var cmdDef = new CommandDefinition($"SELECT TOP 0 * FROM [{tableName}]", cancellationToken: cancellationToken);
                                            using (var reader = await targetConn.ExecuteReaderAsync(cmdDef))
                                            {
                                                for (int i = 0; i < reader.FieldCount; i++)
                                                {
                                                    existingCols.Add(reader.GetName(i));
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // Table doesn't exist or we can't read it, just throw
                                            throw new Exception($"Statement failed: {processedStmt}. Error: {sqlEx.Message}", sqlEx);
                                        }

                                        var missingColsInfo = new List<Models.MissingColumnInfo>();
                                        foreach (var col in viewCols)
                                        {
                                            bool existsInTarget = existingCols.Any(c => c.Equals(col, StringComparison.OrdinalIgnoreCase));
                                            if (!existsInTarget)
                                            {
                                                missingColsInfo.Add(new Models.MissingColumnInfo
                                                {
                                                    ColumnName = col,
                                                    IsInSource = true,
                                                    IsInTarget = false,
                                                    ShouldImport = true,
                                                    SuggestedDataType = "varchar(max)"
                                                });
                                            }
                                        }

                                        if (missingColsInfo.Count > 0)
                                        {
                                            var dialogResult = await missingColumnsPromptCallback(tableName, missingColsInfo);

                                            if (dialogResult.Action == Models.MissingColumnAction.Cancel)
                                            {
                                                _logger.LogWarning("User cancelled import during Missing Columns dialog.");
                                                throw new Exception("Import cancelled by user due to missing columns.");
                                            }

                                            // Apply chosen column additions
                                            var colsToKeepInView = new List<string>();
                                            foreach (var col in viewCols)
                                            {
                                                var missingInfo = dialogResult.Columns.FirstOrDefault(c => c.ColumnName.Equals(col, StringComparison.OrdinalIgnoreCase));
                                                if (missingInfo != null)
                                                {
                                                    if (missingInfo.ShouldImport)
                                                    {
                                                        string addColSql = $"ALTER TABLE [{tableName}] ADD [{col}] {missingInfo.SuggestedDataType}";
                                                        await targetConn.ExecuteAsync(new CommandDefinition(addColSql, cancellationToken: cancellationToken));
                                                        colsToKeepInView.Add(col);
                                                    }
                                                    // if !ShouldImport, we just don't add it to colsToKeepInView
                                                }
                                                else
                                                {
                                                    // Was already existing in target, so keep it
                                                    colsToKeepInView.Add(col);
                                                }
                                            }

                                            // Reconstruct the CREATE VIEW statement without the skipped columns
                                            string reconstructedStmt = System.Text.RegularExpressions.Regex.Replace(processedStmt, @"(CREATE\s+VIEW\s+(?:\[?[a-zA-Z0-9_]+\]?)\s+AS\s+SELECT\s+).+?(\s+FROM\s+\[?[a-zA-Z0-9_]+\]?)", $"${{1}}{string.Join(",", colsToKeepInView)}${{2}}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                                            try
                                            {
                                                _logger.LogDebug($"Executing reconstructed CREATE VIEW statement: {reconstructedStmt}");
                                                await targetConn.ExecuteAsync(new CommandDefinition(reconstructedStmt, cancellationToken: cancellationToken));
                                                _logger.LogInfo("Successfully created view after resolving missing columns.");
                                            }
                                            catch (Exception ex2)
                                            {
                                                _logger.LogError($"Statement failed after resolving columns: {reconstructedStmt}", ex2);
                                                throw new Exception($"Statement failed after resolving columns: {reconstructedStmt}. Error: {ex2.Message}", ex2);
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogError($"Statement failed (could not parse missing columns): {processedStmt}", sqlEx);
                                            throw new Exception($"Statement failed: {processedStmt}. Error: {sqlEx.Message}", sqlEx);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogError($"Statement failed (regex match failed for view creation): {processedStmt}", sqlEx);
                                        throw new Exception($"Statement failed: {processedStmt}. Error: {sqlEx.Message}", sqlEx);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"Statement failed: {processedStmt}", ex);
                                    throw new Exception($"Statement failed: {processedStmt}. Error: {ex.Message}", ex);
                                }
                            }
                        }
                    }
                    currentStep++;
                }

                progressCallback(new ViewModels.ImportProgressInfo
                {
                    IsIndeterminate = false,
                    Percentage = 100,
                    Message = "Import completed successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("PerformImportProcessAsync Error", ex);
                throw;
            }
        }

        private class ParsedSql
        {
            public string TableName { get; set; } = "";
            public List<string> Statements { get; set; } = new List<string>();
        }

        private ParsedSql ParseSqlImportFile(string content)
        {
            var result = new ParsedSql();

            // Extract @CREATE(TableName,...)
            var createMatch = System.Text.RegularExpressions.Regex.Match(content, @"@CREATE\s*\(\s*([^,]+)\s*,");
            if (createMatch.Success)
            {
                result.TableName = createMatch.Groups[1].Value.Trim();
            }

            // Extract CREATE VIEW ... AS SELECT ... FROM ... to find the temporary view name
            string viewName = "";
            var viewMatch = System.Text.RegularExpressions.Regex.Match(content, @"CREATE\s+VIEW\s+([^\s]+)\s+AS", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (viewMatch.Success)
            {
                viewName = viewMatch.Groups[1].Value.Trim();
            }

            // Split content into statements safely (respecting string literals)
            var rawStatements = SplitSqlStatements(content);

            foreach (var stmt in rawStatements)
            {
                // Skip macro statements
                if (stmt.StartsWith("@", StringComparison.OrdinalIgnoreCase)) continue;

                string processedStmt = stmt;

                // If this is an INSERT statement, we need to fix empty commas in VALUES
                if (processedStmt.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
                {
                    processedStmt = FixEmptySqlValues(processedStmt);
                    processedStmt = ProcessDateMacros(processedStmt);
                }

                // If it's dropping the view but using DROP TABLE, fix it to DROP VIEW
                if (!string.IsNullOrEmpty(viewName) && processedStmt.StartsWith($"DROP TABLE {viewName}", StringComparison.OrdinalIgnoreCase))
                {
                    processedStmt = $"DROP VIEW {viewName}";
                }

                result.Statements.Add(processedStmt);
            }

            return result;
        }

        private string ProcessJulianDatesInInsert(string stmt, List<bool> isDateColumn)
        {
            var match = System.Text.RegularExpressions.Regex.Match(stmt, @"\bVALUES\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) return stmt;

            int startIdx = match.Index + match.Length - 1; // Start exactly at the '('

            bool inString = false;

            var currentVal = new System.Text.StringBuilder();
            int colIndex = 0;
            var newStmt = new System.Text.StringBuilder(stmt.Substring(0, startIdx));

            string julianPattern = @"^'(\d{4})(\d{3})(?:\s+(\d{2}:\d{2}:\d{2}(?:\.\d+)?))?'$";

            bool inValues = false; // true when inside a (... ) block of values

            for (int i = startIdx; i < stmt.Length; i++)
            {
                char c = stmt[i];

                if (c == '\'')
                {
                    inString = !inString;
                    currentVal.Append(c);
                }
                else if (!inString)
                {
                    if (c == '(')
                    {
                        if (!inValues) {
                            inValues = true;
                            colIndex = 0;
                            newStmt.Append(c);
                        } else {
                            currentVal.Append(c);
                        }
                    }
                    else if (c == ')' && inValues)
                    {
                        inValues = false;

                        string valStr = currentVal.ToString();
                        string trimmedValStr = valStr.Trim();
                        if (trimmedValStr.Length > 0)
                        {
                            string processed = ProcessSingleJulianValue(trimmedValStr, colIndex, isDateColumn, julianPattern);
                            newStmt.Append(processed);
                        }
                        newStmt.Append(c);
                        currentVal.Clear();
                    }
                    else if (c == ',' && inValues)
                    {
                        string valStr = currentVal.ToString();
                        string trimmedValStr = valStr.Trim();
                        string processed = ProcessSingleJulianValue(trimmedValStr, colIndex, isDateColumn, julianPattern);
                        newStmt.Append(processed).Append(c);
                        currentVal.Clear();
                        colIndex++;
                    }
                    else
                    {
                        if (inValues)
                        {
                            currentVal.Append(c);
                        }
                        else
                        {
                            newStmt.Append(c);
                        }
                    }
                }
                else
                {
                    currentVal.Append(c);
                }
            }

            return newStmt.ToString();
        }

        private string ProcessSingleJulianValue(string valStr, int colIndex, List<bool> isDateColumn, string julianPattern)
        {
            if (colIndex < isDateColumn.Count && isDateColumn[colIndex])
            {
                var jMatch = System.Text.RegularExpressions.Regex.Match(valStr, julianPattern);
                if (jMatch.Success)
                {
                    string yearStr = jMatch.Groups[1].Value;
                    string dayStr = jMatch.Groups[2].Value;
                    string timeStr = jMatch.Groups[3].Value;

                    if (int.TryParse(yearStr, out int year) && year >= 1900 && year <= 2100 &&
                        int.TryParse(dayStr, out int day) && day >= 1 && day <= 366)
                    {
                        try
                        {
                            DateTime dt = new DateTime(year, 1, 1).AddDays(day - 1);
                            if (!string.IsNullOrEmpty(timeStr))
                            {
                                return $"'{dt.ToString("yyyy-MM-dd")} {timeStr}'";
                            }
                            else
                            {
                                return $"'{dt.ToString("yyyy-MM-dd")}'";
                            }
                        }
                        catch { }
                    }
                }
            }
            return valStr;
        }

        private string ProcessDateMacros(string statement)
        {
            // Process macro strings
            string macroPattern = @"'(@DSS[F\+\-]?\d*|@DSW[DF\+\-]?\d*)(?:\s+([^']+))?'";

            string processedStatement = System.Text.RegularExpressions.Regex.Replace(statement, macroPattern, match =>
            {
                string macro = match.Groups[1].Value.ToUpperInvariant();
                string timePart = match.Groups[2].Value;

                string sqlExpression = "";

                if (macro == "@DSSF")
                {
                    sqlExpression = "CONVERT(VARCHAR(10), GETDATE(), 120)";
                }
                else if (macro.StartsWith("@DSS+") || macro.StartsWith("@DSS-"))
                {
                    string sign = macro.Substring(4, 1); // get the +/-
                    string days = macro.Substring(5); // get the numbers
                    sqlExpression = $"CONVERT(VARCHAR(10), DATEADD(day, {sign}{days}, GETDATE()), 120)";
                }
                else if (macro == "@DSWD")
                {
                    // Start of week (Sunday)
                    sqlExpression = "CONVERT(VARCHAR(10), DATEADD(day, 1 - DATEPART(weekday, GETDATE()), GETDATE()), 120)";
                }
                else if (macro == "@DSWF")
                {
                    // End of week (Saturday)
                    sqlExpression = "CONVERT(VARCHAR(10), DATEADD(day, 7 - DATEPART(weekday, GETDATE()), GETDATE()), 120)";
                }
                else if (macro.StartsWith("@DSW+") || macro.StartsWith("@DSW-"))
                {
                    string sign = macro.Substring(4, 1);
                    string days = macro.Substring(5);
                    sqlExpression = $"CONVERT(VARCHAR(10), DATEADD(day, {sign}{days} + (1 - DATEPART(weekday, GETDATE())), GETDATE()), 120)";
                }
                else
                {
                    return match.Value; // Return original if unknown macro
                }

                if (!string.IsNullOrEmpty(timePart))
                {
                    // If there was a time part, append it
                    return $"CONVERT(DATETIME, {sqlExpression} + ' {timePart}', 120)";
                }

                return sqlExpression;
            }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return processedStatement;
        }

        private List<string> SplitSqlStatements(string sql)
        {
            var statements = new List<string>();
            bool inString = false;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < sql.Length; i++)
            {
                char c = sql[i];

                if (c == '\'')
                {
                    // If we encounter a quote, it toggles the string state, UNLESS it's escaped (two quotes)
                    // We just toggle it. If it's escaped, the next quote will toggle it back immediately.
                    inString = !inString;
                    sb.Append(c);
                }
                else if (c == ';' && !inString)
                {
                    string stmt = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(stmt))
                    {
                        statements.Add(stmt);
                    }
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
            {
                string stmt = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(stmt))
                {
                    statements.Add(stmt);
                }
            }

            return statements;
        }

        private string FixEmptySqlValues(string statement)
        {
            // We only want to replace empty commas outside of string literals
            bool inString = false;
            var sb = new System.Text.StringBuilder();
            char prevChar = '\0';

            for (int i = 0; i < statement.Length; i++)
            {
                char c = statement[i];

                if (c == '\'')
                {
                    inString = !inString;
                    sb.Append(c);
                    prevChar = c;
                    continue;
                }

                if (!inString)
                {
                    if (c == ',' && prevChar == ',')
                    {
                        sb.Append("NULL,");
                    }
                    else if (c == ',' && prevChar == '(')
                    {
                        sb.Append("NULL,");
                    }
                    else if (c == ')' && prevChar == ',')
                    {
                        sb.Append("NULL)");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    sb.Append(c);
                }

                // Skip spaces when tracking the previous structural character
                if (!char.IsWhiteSpace(c))
                {
                    prevChar = c;
                }
            }

            return sb.ToString();
        }

        public async Task<int> GetMatchRowIndexAsync(string server, string database, string? user, string? pass, string sql, object? parameters, string? filterClause, string searchText, Dictionary<string, string?> columnTypes, int startRowIndex, string? sortCol, string? sortDir, bool forward, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return -1;
            string safeFilter = searchText.Replace("'", "''");

            List<string> searchParts = new List<string>();
            foreach (var kvp in columnTypes)
            {
                string col = kvp.Key;
                string? type = kvp.Value;
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
                if (safeCol.Equals("Field", StringComparison.OrdinalIgnoreCase))
                {
                    orderBy = $"TRY_CAST(SUBSTRING([{safeCol}], 2, LEN([{safeCol}])) AS INT) {sortDir}, [{safeCol}] {sortDir}";
                }
                else
                {
                    orderBy = $"[{safeCol}] {sortDir}";
                }
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
                object? result;
                try
                {
                    result = await conn.ExecuteScalarAsync<object>(cmdDef);
                }
                catch (SqlException ex) when (ex.Number == 1033)
                {
                    string offsetFinalSql = ApplyFilter(sql, filterClause, true);
                    string offsetSql = $@"
                        SELECT TOP 1 RowNum
                        FROM (
                            SELECT ROW_NUMBER() OVER (ORDER BY {orderBy}) - 1 as RowNum, *
                            FROM ({offsetFinalSql}) AS _Base
                        ) AS _Ordered
                        WHERE RowNum {comparison} {startRowIndex}
                        AND ({searchExpression})
                        ORDER BY RowNum {orderDirection}";
                    var offsetCmdDef = new CommandDefinition(offsetSql, parameters, cancellationToken: cancellationToken);
                    result = await conn.ExecuteScalarAsync<object>(offsetCmdDef);
                }

                if (result == null || result == DBNull.Value) return -1;
                return Convert.ToInt32(result);
            }
        }
    }
}
