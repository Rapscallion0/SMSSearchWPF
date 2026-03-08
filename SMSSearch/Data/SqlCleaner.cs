using System.Collections.Generic;

namespace SMS_Search.Data
{
    public struct SqlCleaningRule
    {
        public string? Pattern;
        public string? Replacement;
    }

    public static class SqlCleaner
    {
        public static List<SqlCleaningRule> DefaultRules
        {
            get
            {
                return new List<SqlCleaningRule>
                {
                    new SqlCleaningRule { Pattern = "&amp;", Replacement = "&" },
                    new SqlCleaningRule { Pattern = "<(/|)(((logsql|sql|prm|msg|errsql|logurl|).*?)|(pre|p|(br(( |)/|))))>", Replacement = "" },
                    new SqlCleaningRule { Pattern = "&lt;", Replacement = "<" },
                    new SqlCleaningRule { Pattern = "&gt;", Replacement = ">" },
                    new SqlCleaningRule { Pattern = @"\[", Replacement = "(" },
                    new SqlCleaningRule { Pattern = @"\]", Replacement = ")" },
                    new SqlCleaningRule { Pattern = "&quot;", Replacement = "'" },
                    new SqlCleaningRule { Pattern = @"\{09\}", Replacement = "" }
                };
            }
        }
    }
}
