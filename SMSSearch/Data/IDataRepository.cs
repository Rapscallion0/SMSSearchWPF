using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SMS_Search.Data
{
    public interface IDataRepository
    {
        string GetConnectionString(string server, string database, string user, string pass);
        Task<bool> TestConnectionAsync(string server, string database, string user, string pass, CancellationToken cancellationToken = default);
        bool TestConnection(string server, string database, string user, string pass);
        Task<DataTable> ExecuteQueryAsync(string server, string database, string user, string pass, string sql, object parameters = null, CancellationToken cancellationToken = default);
        Task<int> GetQueryCountAsync(string server, string database, string user, string pass, string sql, object parameters, string filter = null, CancellationToken cancellationToken = default);
        Task<DataTable> GetQueryPageAsync(string server, string database, string user, string pass, string sql, object parameters, int offset, int limit, string sortCol, string sortDir, string filter = null, CancellationToken cancellationToken = default);
        Task<DataTable> GetQuerySchemaAsync(string server, string database, string user, string pass, string sql, object parameters, CancellationToken cancellationToken = default);
        Task<DbDataReader> GetQueryDataReaderAsync(string server, string database, string user, string pass, string sql, object parameters, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetDatabasesAsync(string server, string user, string pass, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetTablesAsync(string server, string database, string user, string pass, CancellationToken cancellationToken = default);
        Task<List<string>> GetColumnDescriptionsAsync(string server, string database, string user, string pass, IEnumerable<string> columnNames, CancellationToken cancellationToken = default);
        Task<long> GetTotalMatchCountAsync(string server, string database, string user, string pass, string sql, object parameters, string filterClause, string filterText, Dictionary<string, string> columnTypes, CancellationToken cancellationToken = default);
        Task<long> GetPrecedingMatchCountAsync(string server, string database, string user, string pass, string sql, object parameters, string filterClause, string filterText, Dictionary<string, string> columnTypes, int limitRowIndex, string sortCol, string sortDir, CancellationToken cancellationToken = default);
        Task<int> GetMatchRowIndexAsync(string server, string database, string user, string pass, string sql, object parameters, string filterClause, string searchText, Dictionary<string, string> columnTypes, int startRowIndex, string sortCol, string sortDir, bool forward, CancellationToken cancellationToken = default);
    }
}
