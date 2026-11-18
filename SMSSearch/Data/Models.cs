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
    public enum LayoutSearchTabPreference
    {
        [System.ComponentModel.Description("Function")]
        Function,
        [System.ComponentModel.Description("Totalizer")]
        Totalizer,
        [System.ComponentModel.Description("Fields")]
        Field,
        [System.ComponentModel.Description("Records")]
        Records,
        [System.ComponentModel.Description("Last used")]
        Last
    }
    public enum LayoutSearchModePreference
    {
        [System.ComponentModel.Description("Simple")]
        Simple,
        [System.ComponentModel.Description("Advanced")]
        Advanced,
        [System.ComponentModel.Description("Last used")]
        Last
    }
    public enum SearchType { Number, Description, CustomSql, Table }
    public enum SearchLogicalOperator { And, Or }
    public enum SearchComparisonOperator
    {
        [System.ComponentModel.Description("=")]
        Equals,
        [System.ComponentModel.Description("!=")]
        NotEquals,
        [System.ComponentModel.Description("Contains")]
        Contains,
        [System.ComponentModel.Description("Not Contains")]
        DoesNotContain,
        [System.ComponentModel.Description("Wildcard (* ?)")]
        Wildcard,
        [System.ComponentModel.Description("In (a, b, ...)")]
        In
    }
    public enum SearchFieldTarget { Number, Description, Table }
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

    public abstract class SearchCriteriaNode
    {
        public SearchLogicalOperator LogicalOperator { get; set; } = SearchLogicalOperator.And;
    }

    public class SearchClause : SearchCriteriaNode
    {
        public SearchFieldTarget Field { get; set; }
        public string? ColumnName { get; set; }
        public SearchComparisonOperator Operator { get; set; } = SearchComparisonOperator.Equals;
        public string Value { get; set; } = string.Empty;
    }

    public class SearchClauseGroup : SearchCriteriaNode
    {
        public System.Collections.Generic.List<SearchCriteriaNode> Children { get; set; } = new();
    }

    public class SearchCriteria
    {
        public SearchMode Mode { get; set; }
        public SearchType Type { get; set; }
        public string? Value { get; set; }
        public bool AnyMatch { get; set; }
        public bool ShowFields { get; set; }
        public bool LastTransaction { get; set; }
        public SearchClauseGroup? RootGroup { get; set; }
    }

    public class QueryResult
    {
        public string Sql { get; set; } = string.Empty;
        public DynamicParameters? Parameters { get; set; }
    }
}
