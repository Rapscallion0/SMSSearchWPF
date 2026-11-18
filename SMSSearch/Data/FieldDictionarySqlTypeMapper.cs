using System;
using SMS_Search.Services;

namespace SMS_Search.Data
{
    public enum ImportSchemaSource
    {
        TemplateDatabase,
        FieldsCsv,
        InferFromSql
    }

    public static class FieldDictionarySqlTypeMapper
    {
        public static string MapToSqlType(FieldDictionaryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string typeCode = NormalizeTypeCode(entry.Type);
            int.TryParse(entry.Size?.Trim(), out int size);
            int.TryParse(entry.Dec?.Trim(), out int dec);

            switch (typeCode)
            {
                case "C":
                    if (size > 8000) return "VARCHAR(MAX)";
                    return size > 0 ? $"VARCHAR({size})" : "VARCHAR(255)";

                case "D":
                    return "DATETIME";

                case "F":
                    return "BIT";

                case "I":
                    return size >= 10 ? "BIGINT" : "INT";

                case "M":
                    if (size > 0)
                    {
                        int p = Math.Clamp(Math.Max(size, dec), 1, 38);
                        int s = Math.Clamp(dec, 0, p);
                        return $"DECIMAL({p},{s})";
                    }
                    return "MONEY";

                case "N":
                    if (size <= 0) return "FLOAT";
                    {
                        int p = Math.Clamp(Math.Max(size, dec), 1, 38);
                        int s = Math.Clamp(dec, 0, p);
                        return $"DECIMAL({p},{s})";
                    }

                case "T":
                    return size > 0 ? $"VARCHAR({size})" : "VARCHAR(8)";

                case "X":
                    return "NVARCHAR(MAX)";

                default:
                    return "VARCHAR(255)";
            }
        }

        public static string MapToRbFieldsType(string? csvType)
        {
            string code = NormalizeTypeCode(csvType);
            return code switch
            {
                "C" => "dtChar",
                "D" => "dtDate",
                "F" => "dtFlag",
                "I" => "dtInt",
                "M" => "dtMoney",
                "N" => "dtNum",
                "T" => "dtTime",
                "X" => "dtXML",
                _ => string.IsNullOrWhiteSpace(csvType) ? "dtChar" : csvType.Trim()
            };
        }

        private static string NormalizeTypeCode(string? rawType)
        {
            if (string.IsNullOrWhiteSpace(rawType)) return string.Empty;

            string t = rawType.Trim();
            if (t.StartsWith("dt", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring(2);
            }

            return t.ToUpperInvariant() switch
            {
                "C" or "CHAR" or "VARCHAR" or "STRING" => "C",
                "D" or "DATE" or "DATETIME" => "D",
                "F" or "FLAG" or "BOOL" or "BOOLEAN" or "BIT" => "F",
                "I" or "INT" or "INTEGER" => "I",
                "M" or "MONEY" or "CURRENCY" => "M",
                "N" or "NUM" or "NUMERIC" or "DECIMAL" => "N",
                "T" or "TIME" => "T",
                "X" or "XML" => "X",
                _ => t.ToUpperInvariant()
            };
        }
    }
}

