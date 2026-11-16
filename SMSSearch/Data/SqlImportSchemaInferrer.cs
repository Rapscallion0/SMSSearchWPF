using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#nullable enable

namespace SMS_Search.Data
{
    public enum InferredColumnType
    {
        Unknown,
        Int,
        BigInt,
        Decimal,
        DateTime,
        String
    }

    public sealed class InferredColumnDefinition
    {
        public string Name { get; }
        public InferredColumnType Type { get; set; }

        public InferredColumnDefinition(string name, InferredColumnType type = InferredColumnType.Unknown)
        {
            Name = name;
            Type = type;
        }

        public string ToSqlType()
        {
            return Type switch
            {
                InferredColumnType.Int => "INT",
                InferredColumnType.BigInt => "BIGINT",
                InferredColumnType.Decimal => "DECIMAL(38, 10)",
                InferredColumnType.DateTime => "DATETIME",
                _ => "NVARCHAR(MAX)"
            };
        }
    }

    /// <summary>
    /// Extracts table column names and infers SQL Server column data types from
    /// SMS .sql export scripts when no template database is available.
    /// </summary>
    public static class SqlImportSchemaInferrer
    {
        private static readonly Regex CreateViewColumnsRegex = new Regex(
            @"CREATE\s+VIEW\s+(?:\[?[^\s\]]+\]?)\s+AS\s+SELECT\s+(?<cols>.+?)\s+FROM\s+(?:\[?(?<table>[^\s\]\;]+)\]?)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex InsertColumnsRegex = new Regex(
            @"INSERT\s+(?:INTO\s+)?(?:\[[^\]]+\]|[^\s\(]+)\s*\((?<cols>[^)]+)\)\s*VALUES",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex JulianDateRegex = new Regex(
            @"^'(\d{4})(\d{3})(?:\s+(\d{2}:\d{2}:\d{2}(?:\.\d+)?))?'$",
            RegexOptions.Compiled);

        private static readonly string[] DateFormats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "MM/dd/yyyy",
            "MM/dd/yyyy HH:mm:ss",
            "M/d/yyyy",
            "M/d/yyyy h:mm:ss tt"
        };

        /// <summary>
        /// Builds a CREATE TABLE [dbo].[tableName] (...) statement from the parsed SQL statements.
        /// </summary>
        public static string BuildCreateTableSql(string tableName, IReadOnlyList<string> statements)
        {
            var columns = InferColumns(statements);
            return BuildCreateTableSql(tableName, columns.Select(c => (c.Name, c.ToSqlType())).ToList());
        }

        /// <summary>
        /// Builds a CREATE TABLE [dbo].[tableName] (...) statement from explicit column names and SQL types.
        /// </summary>
        public static string BuildCreateTableSql(string tableName, IReadOnlyList<(string Name, string SqlType)> columns)
        {
            if (columns.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Could not determine column definitions for table '{tableName}' from the SQL file without a template database.");
            }

            string safeTableName = tableName.Replace("]", "]]");
            var colDefs = columns.Select(c =>
            {
                string sqlType = string.IsNullOrWhiteSpace(c.SqlType) ? "NVARCHAR(MAX)" : c.SqlType.Trim();
                return $"[{c.Name.Replace("]", "]]")}] {sqlType} NULL";
            });
            return $"CREATE TABLE [dbo].[{safeTableName}] ({string.Join(", ", colDefs)})";
        }

        /// <summary>
        /// Extracts column definitions and infers their types from CREATE VIEW and INSERT statements.
        /// </summary>
        public static List<InferredColumnDefinition> InferColumns(IReadOnlyList<string> statements)
        {
            var columnNames = ExtractColumnNames(statements);
            if (columnNames.Count == 0)
            {
                return new List<InferredColumnDefinition>();
            }

            var columns = columnNames.Select(n => new InferredColumnDefinition(n)).ToList();
            var colIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columns.Count; i++)
            {
                colIndexMap[columns[i].Name] = i;
            }

            foreach (var stmt in statements)
            {
                if (!stmt.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                List<int>? targetIndices = null;
                var colMatch = InsertColumnsRegex.Match(stmt);
                if (colMatch.Success)
                {
                    var insertCols = ParseColumnList(colMatch.Groups["cols"].Value);
                    targetIndices = new List<int>(insertCols.Count);
                    foreach (var col in insertCols)
                    {
                        targetIndices.Add(colIndexMap.TryGetValue(col, out int idx) ? idx : -1);
                    }
                }

                foreach (var rowValues in ExtractInsertValueTuples(stmt))
                {
                    for (int i = 0; i < rowValues.Count; i++)
                    {
                        int colIdx = targetIndices != null
                            ? (i < targetIndices.Count ? targetIndices[i] : -1)
                            : i;

                        if (colIdx < 0 || colIdx >= columns.Count) continue;

                        var inferred = InferValueType(rowValues[i]);
                        columns[colIdx].Type = WidenType(columns[colIdx].Type, inferred);
                    }
                }
            }

            return columns;
        }

        private static List<string> ExtractColumnNames(IReadOnlyList<string> statements)
        {
            foreach (var stmt in statements)
            {
                var viewMatch = CreateViewColumnsRegex.Match(stmt);
                if (viewMatch.Success)
                {
                    string colsStr = viewMatch.Groups["cols"].Value.Trim();
                    if (colsStr != "*")
                    {
                        var cols = ParseColumnList(colsStr);
                        if (cols.Count > 0) return cols;
                    }
                }
            }

            foreach (var stmt in statements)
            {
                var insertMatch = InsertColumnsRegex.Match(stmt);
                if (insertMatch.Success)
                {
                    var cols = ParseColumnList(insertMatch.Groups["cols"].Value);
                    if (cols.Count > 0) return cols;
                }
            }

            return new List<string>();
        }

        private static List<string> ParseColumnList(string colsStr)
        {
            return colsStr
                .Split(',')
                .Select(c => c.Trim().Trim('[', ']', '"', '\''))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();
        }

        private static List<List<string>> ExtractInsertValueTuples(string stmt)
        {
            var rows = new List<List<string>>();
            int valuesIdx = FindValuesKeywordIndex(stmt);
            if (valuesIdx < 0) return rows;

            bool inString = false;
            int parenDepth = 0;
            var currentVal = new StringBuilder();
            List<string>? currentRow = null;

            for (int i = valuesIdx + 6; i < stmt.Length; i++)
            {
                char c = stmt[i];

                if (c == '\'')
                {
                    inString = !inString;
                    if (currentRow != null) currentVal.Append(c);
                    continue;
                }

                if (inString)
                {
                    if (currentRow != null) currentVal.Append(c);
                    continue;
                }

                if (c == '(')
                {
                    if (parenDepth == 0)
                    {
                        currentRow = new List<string>();
                        currentVal.Clear();
                    }
                    else
                    {
                        currentVal.Append(c);
                    }
                    parenDepth++;
                }
                else if (c == ')')
                {
                    parenDepth--;
                    if (parenDepth == 0 && currentRow != null)
                    {
                        currentRow.Add(currentVal.ToString().Trim());
                        rows.Add(currentRow);
                        currentRow = null;
                        currentVal.Clear();
                    }
                    else if (parenDepth > 0 && currentRow != null)
                    {
                        currentVal.Append(c);
                    }
                }
                else if (c == ',' && parenDepth == 1 && currentRow != null)
                {
                    currentRow.Add(currentVal.ToString().Trim());
                    currentVal.Clear();
                }
                else if (currentRow != null)
                {
                    currentVal.Append(c);
                }
            }

            return rows;
        }

        private static int FindValuesKeywordIndex(string stmt)
        {
            bool inString = false;
            bool inBracket = false;
            bool inQuoteIdent = false;

            for (int i = 0; i <= stmt.Length - 6; i++)
            {
                char c = stmt[i];
                if (c == '\'' && !inBracket && !inQuoteIdent) inString = !inString;
                else if (!inString)
                {
                    if (c == '[' && !inQuoteIdent) inBracket = true;
                    else if (c == ']' && inBracket) inBracket = false;
                    else if (c == '"' && !inBracket) inQuoteIdent = !inQuoteIdent;
                    else if (!inBracket && !inQuoteIdent &&
                             char.ToUpperInvariant(c) == 'V' &&
                             char.ToUpperInvariant(stmt[i + 1]) == 'A' &&
                             char.ToUpperInvariant(stmt[i + 2]) == 'L' &&
                             char.ToUpperInvariant(stmt[i + 3]) == 'U' &&
                             char.ToUpperInvariant(stmt[i + 4]) == 'E' &&
                             char.ToUpperInvariant(stmt[i + 5]) == 'S' &&
                             (i == 0 || !char.IsLetterOrDigit(stmt[i - 1])) &&
                             (i + 6 == stmt.Length || !char.IsLetterOrDigit(stmt[i + 6])))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static InferredColumnType InferValueType(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken) ||
                rawToken.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                return InferredColumnType.Unknown;
            }

            // Date macros expanded by ProcessDateMacros()
            if (rawToken.StartsWith("CONVERT(", StringComparison.OrdinalIgnoreCase) &&
                rawToken.IndexOf("GETDATE()", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return InferredColumnType.DateTime;
            }

            if (rawToken.StartsWith("'", StringComparison.Ordinal) &&
                rawToken.EndsWith("'", StringComparison.Ordinal) &&
                rawToken.Length >= 2)
            {
                if (IsJulianDateLiteral(rawToken))
                {
                    return InferredColumnType.DateTime;
                }

                string inner = rawToken.Substring(1, rawToken.Length - 2).Replace("''", "'");
                if (DateTime.TryParseExact(inner, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return InferredColumnType.DateTime;
                }

                return InferredColumnType.String;
            }

            if (int.TryParse(rawToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return InferredColumnType.Int;
            }

            if (long.TryParse(rawToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return InferredColumnType.BigInt;
            }

            if (decimal.TryParse(rawToken, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                return InferredColumnType.Decimal;
            }

            return InferredColumnType.String;
        }

        private static bool IsJulianDateLiteral(string rawToken)
        {
            var match = JulianDateRegex.Match(rawToken);
            if (!match.Success) return false;

            if (!int.TryParse(match.Groups[1].Value, out int year) ||
                !int.TryParse(match.Groups[2].Value, out int dayOfYear))
            {
                return false;
            }

            int maxDays = DateTime.IsLeapYear(year) ? 366 : 365;
            return year >= 1900 && year <= 2100 && dayOfYear >= 1 && dayOfYear <= maxDays;
        }

        private static InferredColumnType WidenType(InferredColumnType current, InferredColumnType observed)
        {
            if (observed == InferredColumnType.Unknown) return current;
            if (current == InferredColumnType.Unknown) return observed;
            if (current == observed) return current;

            if ((current == InferredColumnType.Int && observed == InferredColumnType.BigInt) ||
                (current == InferredColumnType.BigInt && observed == InferredColumnType.Int))
            {
                return InferredColumnType.BigInt;
            }

            if ((current == InferredColumnType.Int || current == InferredColumnType.BigInt || current == InferredColumnType.Decimal) &&
                (observed == InferredColumnType.Int || observed == InferredColumnType.BigInt || observed == InferredColumnType.Decimal))
            {
                return InferredColumnType.Decimal;
            }

            return InferredColumnType.String;
        }
    }
}

