using System.Collections.Generic;

namespace SMS_Search.Models
{
    public enum MissingColumnAction
    {
        Continue,
        Cancel
    }

    public class MissingColumnInfo
    {
        public string ColumnName { get; set; } = "";
        public bool IsInSource { get; set; }
        public bool IsInTarget { get; set; }
        public bool IsMissing => !IsInSource || !IsInTarget;

        // Per-column selection for columns that are in Source but not in Target
        public bool ShouldImport { get; set; } = true;

        public string SuggestedDataType { get; set; } = "varchar(max)";
    }

    public class MissingColumnDialogResult
    {
        public MissingColumnAction Action { get; set; }
        public List<MissingColumnInfo> Columns { get; set; } = new List<MissingColumnInfo>();
    }
}
