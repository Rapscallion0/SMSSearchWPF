using Dapper;

namespace SMS_Search.Data
{
    public enum SearchMode { Function, Totalizer, Field }
    public enum DefaultSearchTabMode
    {
        Function,
        Totalizer,
        Field,
        [System.ComponentModel.Description("Last open tab")]
        Last
    }
    public enum DefaultTableAction
    {
        [System.ComponentModel.Description("Query Fields")]
        QueryFields,
        [System.ComponentModel.Description("Query Records")]
        QueryRecords
    }
    public enum SearchType { Number, Description, CustomSql, Table }
    public enum StartupLocationMode
    {
        [System.ComponentModel.Description("Last location")]
        Last,
        [System.ComponentModel.Description("Primary monitor")]
        Primary,
        [System.ComponentModel.Description("Active monitor")]
        Active,
        [System.ComponentModel.Description("Cursor location")]
        Cursor
    }

    public enum AppThemeMode
    {
        [System.ComponentModel.Description("Follow System")]
        System,
        [System.ComponentModel.Description("Light")]
        Light,
        [System.ComponentModel.Description("Dark")]
        Dark
    }

    public class SearchCriteria
    {
        public SearchMode Mode { get; set; }
        public SearchType Type { get; set; }
        public string? Value { get; set; }
        public bool AnyMatch { get; set; }
        public bool ShowFields { get; set; }
        public bool LastTransaction { get; set; }
    }

    public class QueryResult
    {
        public string Sql { get; set; } = string.Empty;
        public DynamicParameters? Parameters { get; set; }
    }
}
