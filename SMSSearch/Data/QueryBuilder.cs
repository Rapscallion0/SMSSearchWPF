using Dapper;

namespace SMS_Search.Data
{
    public class QueryBuilder
    {
        private readonly string _fctFields;
        private readonly string _tlzFields;

        // Base query for retrieving Field Metadata from system tables
        private const string FieldSelectBase = @"SELECT
    COL.name AS 'Field',
    RBF.F1454 AS 'Description',
    TAB.name AS 'Table',
    REPLACE (RBF.F1458,'dt','') AS 'Type',
    (CASE WHEN PKEY.COLUMN_NAME IS NOT NULL THEN '1' ELSE '0' END) AS 'Key',
    COL.max_length AS 'Size',
    COL.scale AS 'Dec',
    (CASE WHEN COL.is_nullable = 0 THEN 1 ELSE 0 END) AS 'Required',
    TAB.create_date AS 'Created'
FROM sys.columns COL
JOIN sys.tables TAB ON COL.object_id = TAB.object_id
LEFT JOIN RB_FIELDS RBF ON COL.name = RBF.F1453 ";

        private const string FieldSelectBaseNoRbFields = @"SELECT
    COL.name AS 'Field',
    '' AS 'Description',
    TAB.name AS 'Table',
    TYPE_NAME(COL.user_type_id) AS 'Type',
    (CASE WHEN PKEY.COLUMN_NAME IS NOT NULL THEN '1' ELSE '0' END) AS 'Key',
    COL.max_length AS 'Size',
    COL.scale AS 'Dec',
    (CASE WHEN COL.is_nullable = 0 THEN 1 ELSE 0 END) AS 'Required',
    TAB.create_date AS 'Created'
FROM sys.columns COL
JOIN sys.tables TAB ON COL.object_id = TAB.object_id ";

        private const string FieldSelectJoinPKey = @"
LEFT OUTER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE PKEY ON
    PKEY.COLUMN_NAME = COL.name AND
    TAB.name=PKEY.TABLE_NAME";

        private const string FieldSelectGroupBy = @"
GROUP BY
    COL.name,
    RBF.F1454,
    TAB.name,
    RBF.F1458,
    pkey.COLUMN_NAME,
    COL.max_length,
    COL.scale,
    COL.is_nullable,
    TAB.create_date";

        private const string FieldSelectGroupByNoRbFields = @"
GROUP BY
    COL.name,
    TAB.name,
    COL.user_type_id,
    pkey.COLUMN_NAME,
    COL.max_length,
    COL.scale,
    COL.is_nullable,
    TAB.create_date";
        private const string FieldSelectOrderBy = @" ORDER BY TRY_CAST(SUBSTRING(COL.name, 2, LEN(COL.name)) AS INT) ASC";

        public QueryBuilder(string fctFields, string tlzFields)
        {
            _fctFields = string.IsNullOrEmpty(fctFields) ? "F1063, F1064, F1051, F1050, F1081" : fctFields;
            _tlzFields = string.IsNullOrEmpty(tlzFields) ? "F1034, F1039, F1128, F1129, F1179, F1253, F1710, F1131, F1048, F1709" : tlzFields;
        }

        public QueryResult Build(SearchCriteria criteria, bool hasRbFields = true)
        {
            var p = new DynamicParameters();
            string sql = "";
            string value = criteria.Value ?? "";

            if (criteria.Mode == SearchMode.Field)
            {
                string selectBase = hasRbFields ? FieldSelectBase : FieldSelectBaseNoRbFields;
                string groupBy = hasRbFields ? FieldSelectGroupBy : FieldSelectGroupByNoRbFields;

                if (criteria.Type == SearchType.Number)
                {
                    string rbJoinFilter = hasRbFields ? " AND RBF.F1452 = TAB.name" : "";
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string fldNum = "F" + value.Trim();
                        HandleWildcards(ref fldNum, out string op);

                        sql = $"{selectBase}{rbJoinFilter} {FieldSelectJoinPKey} WHERE col.name {op} @Val {groupBy}{FieldSelectOrderBy}";
                        p.Add("Val", fldNum);
                    }
                    else
                    {
                        sql = $"{selectBase}{rbJoinFilter} {FieldSelectJoinPKey} {groupBy}{FieldSelectOrderBy}";
                    }
                }
                else if (criteria.Type == SearchType.Description)
                {
                    string rbJoinFilter = hasRbFields ? " AND RBF.F1452 = TAB.name" : "";
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        if (hasRbFields)
                        {
                            string desc = value.Trim();
                            if (criteria.AnyMatch) desc = $"*{desc}*";
                            HandleWildcards(ref desc, out string op);

                            sql = $"{selectBase}{rbJoinFilter} {FieldSelectJoinPKey} WHERE RBF.F1454 {op} @Val {groupBy}{FieldSelectOrderBy}";
                            p.Add("Val", desc);
                        }
                        else
                        {
                            sql = $"{selectBase} {FieldSelectJoinPKey} WHERE 1 = 0 {groupBy}{FieldSelectOrderBy}";
                        }
                    }
                    else
                    {
                        sql = $"{selectBase}{rbJoinFilter} {FieldSelectJoinPKey} {groupBy}{FieldSelectOrderBy}";
                    }
                }
                else if (criteria.Type == SearchType.Table)
                {
                    if (criteria.ShowFields)
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            string table = value.Trim();
                            HandleWildcards(ref table, out string op);
                            string rbJoinFilter = hasRbFields ? $" AND RBF.F1452 {op} @Table" : "";

                            sql = $"{selectBase}{rbJoinFilter} {FieldSelectJoinPKey} WHERE TAB.name {op} @Table {groupBy}{FieldSelectOrderBy}";
                            p.Add("Table", table);
                        }
                        else
                        {
                            string rbJoinFilter = hasRbFields ? " AND RBF.F1452 = TAB.name" : "";
                            sql = $"{selectBase}{rbJoinFilter} {FieldSelectJoinPKey} {groupBy}{FieldSelectOrderBy}";
                        }
                    }
                    else
                    {
                        string tableName = SanitizeIdentifier(value);
                        sql = $"SELECT * FROM {tableName}";

                        if (criteria.LastTransaction)
                        {
                            sql += $" WHERE F1032 = (SELECT DISTINCT TOP 1 F1032 FROM {tableName} ORDER BY F1032 DESC)";
                        }
                    }
                }
                else if (criteria.Type == SearchType.CustomSql)
                {
                    sql = value;
                }
            }
            else if (criteria.Mode == SearchMode.Totalizer)
            {
                sql = $"SELECT {_tlzFields} FROM TLZ_TAB";
                if (criteria.Type == SearchType.Number)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string val = value.Trim();
                        HandleWildcards(ref val, out string op);
                        sql += $" WHERE F1034 {op} @Val";
                        p.Add("Val", val);
                    }
                }
                else if (criteria.Type == SearchType.Description)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string val = value.Trim();
                        if (criteria.AnyMatch) val = $"*{val}*";
                        HandleWildcards(ref val, out string op);
                        sql += $" WHERE F1039 {op} @Val";
                        p.Add("Val", val);
                    }
                }
                else if (criteria.Type == SearchType.CustomSql)
                {
                    sql = value;
                }
            }
            else if (criteria.Mode == SearchMode.Function)
            {
                sql = $"SELECT {_fctFields} FROM FCT_TAB";
                if (criteria.Type == SearchType.Number)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string val = value.Trim();
                        HandleWildcards(ref val, out string op);
                        sql += $" WHERE F1063 {op} @Val";
                        p.Add("Val", val);
                    }
                }
                else if (criteria.Type == SearchType.Description)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string val = value.Trim();
                        if (criteria.AnyMatch) val = $"*{val}*";
                        HandleWildcards(ref val, out string op);
                        sql += $" WHERE F1064 {op} @Val";
                        p.Add("Val", val);
                    }
                }
                else if (criteria.Type == SearchType.CustomSql)
                {
                    sql = value;
                }
            }

            if (string.IsNullOrEmpty(sql)) sql = "SELECT * FROM SYS_TAB";

            return new QueryResult { Sql = sql, Parameters = p };
        }

        private void HandleWildcards(ref string value, out string op)
        {
            if (value.Contains("*") || value.Contains("?"))
            {
                value = value.Replace("*", "%").Replace("?", "_");
                op = "LIKE";
            }
            else
            {
                op = "=";
            }
        }

        private string SanitizeIdentifier(string? id)
        {
            if (string.IsNullOrEmpty(id)) return "[]";
            string clean = id.Replace("[", "").Replace("]", "");
            return "[" + clean + "]";
        }
    }
}
