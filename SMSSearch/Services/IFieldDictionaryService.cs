using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using SMS_Search.Data;

namespace SMS_Search.Services
{
    public enum DictionarySourceMode
    {
        [Description("Auto (Use Fields.csv if present)")]
        Auto,
        [Description("CSV Dictionary (Fields.csv)")]
        Csv,
        [Description("Database (RB_FIELDS)")]
        Database
    }

    public enum DictionaryLanguage
    {
        [Description("English")]
        English,
        [Description("French")]
        French,
        [Description("Spanish")]
        Spanish
    }

    public class FieldDictionaryEntry
    {
        public string Field { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Dec { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public string ShortEn { get; set; } = string.Empty;
        public string DescriptionFr { get; set; } = string.Empty;
        public string ShortFr { get; set; } = string.Empty;
        public string DescriptionEs { get; set; } = string.Empty;
        public string ShortEs { get; set; } = string.Empty;

        /// <summary>
        /// Maps normalized DCT table names (e.g., "OBJ", "POS", "BUYERS") to their flag string
        /// (e.g., "F", "S", "R", "RS", "P", "P;F902;F1000").
        /// </summary>
        public Dictionary<string, string> TableFlags { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class DictionaryStatus
    {
        public bool IsCsvActive { get; init; }
        public bool IsCsvAvailable { get; init; }
        public string? ResolvedFieldsCsvPath { get; init; }
        public bool IsAutoDetected { get; init; }
        public bool HasFields2Csv { get; init; }
        public bool HasFieldsEs { get; init; }
        public int FieldCount { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public class DictionaryOptions
    {
        public DictionaryLanguage Language { get; init; } = DictionaryLanguage.English;
        public bool ShowEn { get; init; }
        public bool ShowFr { get; init; }
        public bool ShowEs { get; init; }
        public bool ShowDct { get; init; } = true;
    }

    public interface IFieldDictionaryService
    {
        event EventHandler? DictionaryChanged;

        DictionarySourceMode GetSourceMode();
        void SetSourceMode(DictionarySourceMode mode);

        DictionaryStatus GetStatus();
        bool IsCsvActive();
        DictionaryOptions GetOptions();

        string GetDescription(string fieldName, DictionaryLanguage? language = null);
        List<string> GetColumnDescriptions(IEnumerable<string> columnNames);

        DataTable SearchOffline(SearchCriteria criteria, DictionaryOptions? options = null);
        DataTable EnrichSqlMetadata(DataTable sqlMetadata, SearchCriteria criteria, DictionaryOptions? options = null);
        IReadOnlyDictionary<string, FieldDictionaryEntry> GetEntries(string? overrideFieldsCsvPath = null);

        void InvalidateCache();
    }
}

