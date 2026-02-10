using Dapper;

namespace SMS_Search.Data
{
    public enum SearchMode { Function, Totalizer, Field }
    public enum SearchType { Number, Description, CustomSql, Table }

    public class SearchCriteria
    {
        public SearchMode Mode { get; set; }
        public SearchType Type { get; set; }
        public string Value { get; set; }
        public bool AnyMatch { get; set; }
        public bool ShowFields { get; set; }
        public bool LastTransaction { get; set; }
    }

    public class QueryResult
    {
        public string Sql { get; set; }
        public DynamicParameters Parameters { get; set; }
    }
}
