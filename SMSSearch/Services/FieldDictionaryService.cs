using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SMS_Search.Data;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class FieldDictionaryService : IFieldDictionaryService
    {
        private readonly IConfigService _configService;
        private readonly ILoggerService _logger;
        private readonly string _baseDirectory;
        private readonly object _cacheLock = new();

        private LoadedDictionary? _cache;

        static FieldDictionaryService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public event EventHandler? DictionaryChanged;

        public FieldDictionaryService(IConfigService configService, ILoggerService logger)
            : this(configService, logger, AppContext.BaseDirectory)
        {
        }

        public FieldDictionaryService(IConfigService configService, ILoggerService logger, string baseDirectory)
        {
            _configService = configService;
            _logger = logger;
            _baseDirectory = baseDirectory;
        }

        public DictionarySourceMode GetSourceMode()
        {
            string? val = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionarySourceMode);
            return Enum.TryParse<DictionarySourceMode>(val, ignoreCase: true, out var mode)
                ? mode
                : DictionarySourceMode.Auto;
        }

        public void SetSourceMode(DictionarySourceMode mode)
        {
            _configService.SetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionarySourceMode, mode.ToString());
            _configService.Save();
            DictionaryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cache = null;
            }
            DictionaryChanged?.Invoke(this, EventArgs.Empty);
        }

        public DictionaryOptions GetOptions()
        {
            string? langStr = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryLanguage);
            var language = Enum.TryParse<DictionaryLanguage>(langStr, ignoreCase: true, out var parsedLang)
                ? parsedLang
                : DictionaryLanguage.English;

            bool showEn = language != DictionaryLanguage.English &&
                          _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowEn) == "1";
            bool showFr = language != DictionaryLanguage.French &&
                          _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowFr) == "1";
            bool showEs = language != DictionaryLanguage.Spanish &&
                          _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowEs) == "1";

            string? showDctVal = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryShowDct);
            bool showDct = showDctVal != null ? showDctVal == "1" : IsCsvActive();

            return new DictionaryOptions
            {
                Language = language,
                ShowEn = showEn,
                ShowFr = showFr,
                ShowEs = showEs,
                ShowDct = showDct
            };
        }

        public bool IsCsvActive()
        {
            var mode = GetSourceMode();
            if (mode == DictionarySourceMode.Database)
                return false;

            if (mode == DictionarySourceMode.Csv)
                return true;

            // Auto: active if a valid Fields.csv is configured or beside the executable
            var (path, _) = ResolveFieldsCsvPath();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public DictionaryStatus GetStatus()
        {
            var mode = GetSourceMode();
            var (resolvedPath, isAuto) = ResolveFieldsCsvPath();
            bool exists = !string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath);

            if (!exists)
            {
                string? err = mode == DictionarySourceMode.Csv
                    ? "Fields.csv not found. Browse for Fields.csv or place it beside the executable."
                    : null;

                return new DictionaryStatus
                {
                    IsCsvActive = mode == DictionarySourceMode.Csv,
                    IsCsvAvailable = false,
                    ResolvedFieldsCsvPath = resolvedPath,
                    IsAutoDetected = isAuto,
                    ErrorMessage = err
                };
            }

            var loaded = EnsureLoaded();
            return new DictionaryStatus
            {
                IsCsvActive = mode != DictionarySourceMode.Database,
                IsCsvAvailable = loaded.ErrorMessage == null && loaded.Entries.Count > 0,
                ResolvedFieldsCsvPath = loaded.FieldsCsvPath,
                IsAutoDetected = isAuto,
                HasFields2Csv = loaded.HasFields2Csv,
                HasFieldsEs = loaded.HasFieldsEs,
                FieldCount = loaded.Entries.Count,
                ErrorMessage = loaded.ErrorMessage
            };
        }

        public string GetDescription(string fieldName, DictionaryLanguage? language = null)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return string.Empty;

            var loaded = EnsureLoaded();
            if (!loaded.Entries.TryGetValue(fieldName.Trim(), out var entry))
                return string.Empty;

            var lang = language ?? GetOptions().Language;
            return SelectDescription(entry, lang);
        }

        public List<string> GetColumnDescriptions(IEnumerable<string> columnNames)
        {
            var loaded = EnsureLoaded();
            var lang = GetOptions().Language;
            var result = new List<string>();

            foreach (var col in columnNames)
            {
                if (!string.IsNullOrWhiteSpace(col) && loaded.Entries.TryGetValue(col.Trim(), out var entry))
                {
                    result.Add(SelectDescription(entry, lang));
                }
                else
                {
                    result.Add(string.Empty);
                }
            }

            return result;
        }

        public DataTable SearchOffline(SearchCriteria criteria, DictionaryOptions? options = null)
        {
            options ??= GetOptions();
            var loaded = EnsureLoaded();
            var dt = CreateResultSchema(options);

            if (loaded.Entries.Count == 0)
                return dt;

            string value = (criteria.Value ?? string.Empty).Trim();
            var rows = new List<(FieldDictionaryEntry Entry, string DctName, string TableName, string Flag)>();

            if (criteria.Type == SearchType.Number)
            {
                string? pattern = string.IsNullOrWhiteSpace(value)
                    ? null
                    : (value.StartsWith("F", StringComparison.OrdinalIgnoreCase) ? value : "F" + value);

                foreach (var entry in loaded.Entries.Values)
                {
                    if (pattern != null && !MatchesWildcard(entry.Field, pattern))
                        continue;

                    if (entry.TableFlags.Count == 0)
                    {
                        rows.Add((entry, string.Empty, string.Empty, string.Empty));
                    }
                    else
                    {
                        foreach (var kvp in entry.TableFlags)
                        {
                            string sqlTable = DictionaryTableMapper.GetPrimarySqlTableForDct(kvp.Key);
                            rows.Add((entry, kvp.Key, sqlTable, kvp.Value));
                        }
                    }
                }
            }
            else if (criteria.Type == SearchType.Description)
            {
                string? pattern = string.IsNullOrWhiteSpace(value)
                    ? null
                    : (criteria.AnyMatch ? $"*{value}*" : value);

                foreach (var entry in loaded.Entries.Values)
                {
                    string primaryDesc = SelectDescription(entry, options.Language);
                    if (pattern != null && !MatchesWildcard(primaryDesc, pattern))
                        continue;

                    if (entry.TableFlags.Count == 0)
                    {
                        rows.Add((entry, string.Empty, string.Empty, string.Empty));
                    }
                    else
                    {
                        foreach (var kvp in entry.TableFlags)
                        {
                            string sqlTable = DictionaryTableMapper.GetPrimarySqlTableForDct(kvp.Key);
                            rows.Add((entry, kvp.Key, sqlTable, kvp.Value));
                        }
                    }
                }
            }
            else if (criteria.Type == SearchType.Table && criteria.ShowFields)
            {
                foreach (var entry in loaded.Entries.Values)
                {
                    foreach (var kvp in entry.TableFlags)
                    {
                        if (!string.IsNullOrWhiteSpace(value) &&
                            !DictionaryTableMapper.MatchesTableFilter(kvp.Key, value, MatchesWildcard))
                        {
                            continue;
                        }

                        string sqlTable = DictionaryTableMapper.GetPrimarySqlTableForDct(kvp.Key);
                        rows.Add((entry, kvp.Key, sqlTable, kvp.Value));
                    }
                }
            }

            foreach (var item in rows.OrderBy(r => ParseFieldSortNumber(r.Entry.Field)).ThenBy(r => r.TableName, StringComparer.OrdinalIgnoreCase))
            {
                var row = dt.NewRow();
                PopulateRowMetadata(row, item.Entry, item.DctName, item.TableName, options);

                bool isPk = loaded.IsPrimaryKey(item.DctName, item.Entry.Field, item.Flag);
                bool isReq = loaded.IsRequired(item.DctName, item.Entry.Field, item.Flag);

                row["Type"] = item.Entry.Type;
                row["Key"] = isPk ? "1" : "0";
                row["Size"] = int.TryParse(item.Entry.Size, out int sizeVal) ? sizeVal : DBNull.Value;
                row["Dec"] = int.TryParse(item.Entry.Dec, out int decVal) ? decVal : DBNull.Value;
                row["Required"] = isReq ? 1 : 0;
                row["Created"] = DBNull.Value;
                dt.Rows.Add(row);
            }

            return dt;
        }

        public DataTable EnrichSqlMetadata(DataTable sqlMetadata, SearchCriteria criteria, DictionaryOptions? options = null)
        {
            options ??= GetOptions();
            var loaded = EnsureLoaded();
            var dt = CreateResultSchema(options);

            string? descFilterPattern = null;
            if (criteria.Type == SearchType.Description && !string.IsNullOrWhiteSpace(criteria.Value))
            {
                string v = criteria.Value.Trim();
                descFilterPattern = criteria.AnyMatch ? $"*{v}*" : v;
            }

            foreach (DataRow src in sqlMetadata.Rows)
            {
                string fieldName = src["Field"]?.ToString()?.Trim() ?? string.Empty;
                string tableName = src["Table"]?.ToString()?.Trim() ?? string.Empty;
                string sqlType = src.Table.Columns.Contains("SqlType")
                    ? src["SqlType"]?.ToString()?.Trim() ?? string.Empty
                    : (src["Type"]?.ToString()?.Trim() ?? string.Empty);

                loaded.Entries.TryGetValue(fieldName, out var entry);
                string dctName = DictionaryTableMapper.GetDctForSqlTable(tableName);

                string primaryDesc = entry != null ? SelectDescription(entry, options.Language) : string.Empty;
                if (descFilterPattern != null && !MatchesWildcard(primaryDesc, descFilterPattern))
                    continue;

                var row = dt.NewRow();
                row["Field"] = fieldName;
                row["Description"] = primaryDesc;
                if (options.ShowEn) row["Description (EN)"] = entry?.DescriptionEn ?? string.Empty;
                if (options.ShowFr) row["Description (FR)"] = entry?.DescriptionFr ?? string.Empty;
                if (options.ShowEs) row["Description (ES)"] = entry?.DescriptionEs ?? string.Empty;
                row["Table"] = tableName;
                if (options.ShowDct) row["Dictionary"] = dctName;

                row["Type"] = !string.IsNullOrWhiteSpace(entry?.Type) ? entry!.Type : sqlType;
                row["Key"] = src["Key"] ?? "0";
                row["Size"] = src["Size"] ?? DBNull.Value;
                row["Dec"] = src["Dec"] ?? DBNull.Value;
                row["Required"] = src["Required"] ?? 0;
                row["Created"] = src["Created"] ?? DBNull.Value;

                dt.Rows.Add(row);
            }

            return dt;
        }

        private static DataTable CreateResultSchema(DictionaryOptions options)
        {
            var dt = new DataTable();
            dt.Columns.Add("Field", typeof(string));
            dt.Columns.Add("Description", typeof(string));
            if (options.ShowEn) dt.Columns.Add("Description (EN)", typeof(string));
            if (options.ShowFr) dt.Columns.Add("Description (FR)", typeof(string));
            if (options.ShowEs) dt.Columns.Add("Description (ES)", typeof(string));
            dt.Columns.Add("Table", typeof(string));
            if (options.ShowDct) dt.Columns.Add("Dictionary", typeof(string));
            dt.Columns.Add("Type", typeof(string));
            dt.Columns.Add("Key", typeof(string));
            dt.Columns.Add("Size", typeof(int));
            dt.Columns.Add("Dec", typeof(int));
            dt.Columns.Add("Required", typeof(int));
            dt.Columns.Add("Created", typeof(DateTime));
            return dt;
        }

        private static void PopulateRowMetadata(DataRow row, FieldDictionaryEntry entry, string dctName, string tableName, DictionaryOptions options)
        {
            row["Field"] = entry.Field;
            row["Description"] = SelectDescription(entry, options.Language);
            if (options.ShowEn) row["Description (EN)"] = entry.DescriptionEn;
            if (options.ShowFr) row["Description (FR)"] = entry.DescriptionFr;
            if (options.ShowEs) row["Description (ES)"] = entry.DescriptionEs;
            row["Table"] = tableName;
            if (options.ShowDct) row["Dictionary"] = dctName;
        }

        private static string SelectDescription(FieldDictionaryEntry entry, DictionaryLanguage language)
        {
            string desc = language switch
            {
                DictionaryLanguage.French => !string.IsNullOrWhiteSpace(entry.DescriptionFr) ? entry.DescriptionFr : entry.DescriptionEn,
                DictionaryLanguage.Spanish => !string.IsNullOrWhiteSpace(entry.DescriptionEs) ? entry.DescriptionEs : entry.DescriptionEn,
                _ => entry.DescriptionEn
            };
            return desc.Trim();
        }

        private (string? Path, bool IsAutoDetected) ResolveFieldsCsvPath()
        {
            string? configured = _configService.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.DictionaryPath)?.Trim();
            if (!string.IsNullOrEmpty(configured))
            {
                return (configured, false);
            }

            string autoPath = System.IO.Path.Combine(_baseDirectory, "Fields.csv");
            return (autoPath, true);
        }

        private LoadedDictionary EnsureLoaded()
        {
            var (fieldsPath, _) = ResolveFieldsCsvPath();
            if (string.IsNullOrEmpty(fieldsPath) || !File.Exists(fieldsPath))
            {
                return new LoadedDictionary(fieldsPath ?? string.Empty);
            }

            string dir = System.IO.Path.GetDirectoryName(fieldsPath) ?? _baseDirectory;
            string fields2Path = System.IO.Path.Combine(dir, "Fields2.csv");
            string fieldsEsPath = System.IO.Path.Combine(dir, "Fields.es");

            DateTime t1 = File.GetLastWriteTimeUtc(fieldsPath);
            DateTime t2 = File.Exists(fields2Path) ? File.GetLastWriteTimeUtc(fields2Path) : DateTime.MinValue;
            DateTime t3 = File.Exists(fieldsEsPath) ? File.GetLastWriteTimeUtc(fieldsEsPath) : DateTime.MinValue;

            lock (_cacheLock)
            {
                if (_cache != null &&
                    string.Equals(_cache.FieldsCsvPath, fieldsPath, StringComparison.OrdinalIgnoreCase) &&
                    _cache.FieldsCsvTimestamp == t1 &&
                    _cache.Fields2CsvTimestamp == t2 &&
                    _cache.FieldsEsTimestamp == t3)
                {
                    return _cache;
                }

                var loaded = LoadFromDisk(fieldsPath, t1, fields2Path, t2, fieldsEsPath, t3);
                _cache = loaded;
                return loaded;
            }
        }

        private LoadedDictionary LoadFromDisk(
            string fieldsPath, DateTime t1,
            string fields2Path, DateTime t2,
            string fieldsEsPath, DateTime t3)
        {
            var result = new LoadedDictionary(fieldsPath)
            {
                FieldsCsvTimestamp = t1,
                Fields2CsvTimestamp = t2,
                FieldsEsTimestamp = t3
            };

            try
            {
                ParseMainOrCustomCsv(fieldsPath, result, isOverlay: false);

                if (File.Exists(fields2Path))
                {
                    result.HasFields2Csv = true;
                    ParseMainOrCustomCsv(fields2Path, result, isOverlay: true);
                }

                if (File.Exists(fieldsEsPath))
                {
                    result.HasFieldsEs = true;
                    ParseSpanishCsv(fieldsEsPath, result);
                }

                result.BuildCompositeKeyIndex();
                _logger.LogInfo("Loaded database dictionary from {Path} ({Count} fields, Fields2={HasFields2}, FieldsEs={HasFieldsEs}).",
                    fieldsPath, result.Entries.Count, result.HasFields2Csv, result.HasFieldsEs);
            }
            catch (IOException ex)
            {
                result.ErrorMessage = $"Unable to read dictionary file (it may be locked by another program): {ex.Message}";
                _logger.LogWarning("IO error loading dictionary {Path}: {Message}", fieldsPath, ex.Message);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Failed to parse dictionary: {ex.Message}";
                _logger.LogError(ex, "Failed to parse dictionary {Path}", fieldsPath);
            }

            return result;
        }

        private static void ParseMainOrCustomCsv(string path, LoadedDictionary target, bool isOverlay)
        {
            var lines = ReadAllLinesShared(path);
            if (lines.Count == 0) return;

            var header = ParseCsvLine(lines[0]);
            int fieldIdx = FindHeaderIndex(header, "FIELD", 1);
            int typeIdx = FindHeaderIndex(header, "TYPE", 2);
            int sizeIdx = FindHeaderIndex(header, "SIZE", 3);
            int decIdx = FindHeaderIndex(header, "DEC", 4);
            int descIdx = FindHeaderIndex(header, "DESCRIPTOR", 5);
            int shortEnIdx = FindHeaderIndex(header, "Short", 6, occurrence: 0);
            int transFrIdx = FindHeaderIndex(header, "Translation", 8);
            int shortFrIdx = FindHeaderIndex(header, "Short", 9, occurrence: 1);
            int sIdx = FindHeaderIndex(header, "S", 10);

            int firstTableCol = sIdx >= 0 ? sIdx + 1 : 11;

            for (int r = 1; r < lines.Count; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) continue;
                var cols = ParseCsvLine(lines[r]);
                if (fieldIdx < 0 || fieldIdx >= cols.Count) continue;

                string fieldName = cols[fieldIdx].Trim();
                if (string.IsNullOrEmpty(fieldName)) continue;

                if (!target.Entries.TryGetValue(fieldName, out var entry))
                {
                    entry = new FieldDictionaryEntry { Field = fieldName };
                    target.Entries[fieldName] = entry;
                }

                // Non-destructive merge: only overwrite if non-empty (or not an overlay)
                SetIfPresent(cols, typeIdx, v => entry.Type = v, isOverlay);
                SetIfPresent(cols, sizeIdx, v => entry.Size = v, isOverlay);
                SetIfPresent(cols, decIdx, v => entry.Dec = v, isOverlay);
                SetIfPresent(cols, descIdx, v => entry.DescriptionEn = v, isOverlay);
                SetIfPresent(cols, shortEnIdx, v => entry.ShortEn = v, isOverlay);
                SetIfPresent(cols, transFrIdx, v => entry.DescriptionFr = v, isOverlay);
                SetIfPresent(cols, shortFrIdx, v => entry.ShortFr = v, isOverlay);

                for (int c = firstTableCol; c < header.Count && c < cols.Count; c++)
                {
                    string tableDct = header[c].Trim();
                    string flag = cols[c].Trim();
                    if (!string.IsNullOrEmpty(tableDct) && !string.IsNullOrEmpty(flag))
                    {
                        entry.TableFlags[tableDct] = flag;
                    }
                }
            }
        }

        private static void ParseSpanishCsv(string path, LoadedDictionary target)
        {
            var lines = ReadAllLinesShared(path);
            if (lines.Count == 0) return;

            var header = ParseCsvLine(lines[0]);
            int fieldIdx = FindHeaderIndex(header, "FIELD", 0);
            int transIdx = FindHeaderIndex(header, "Translation", 1);
            int shortIdx = FindHeaderIndex(header, "Short", 2);

            for (int r = 1; r < lines.Count; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) continue;
                var cols = ParseCsvLine(lines[r]);
                if (fieldIdx < 0 || fieldIdx >= cols.Count) continue;

                string fieldName = cols[fieldIdx].Trim();
                if (string.IsNullOrEmpty(fieldName)) continue;

                if (target.Entries.TryGetValue(fieldName, out var entry))
                {
                    if (transIdx >= 0 && transIdx < cols.Count && !string.IsNullOrWhiteSpace(cols[transIdx]))
                        entry.DescriptionEs = cols[transIdx].Trim();
                    if (shortIdx >= 0 && shortIdx < cols.Count && !string.IsNullOrWhiteSpace(cols[shortIdx]))
                        entry.ShortEs = cols[shortIdx].Trim();
                }
            }
        }

        private static void SetIfPresent(List<string> cols, int index, Action<string> setter, bool isOverlay)
        {
            if (index < 0 || index >= cols.Count) return;
            string val = cols[index].Trim();
            if (!isOverlay || !string.IsNullOrEmpty(val))
            {
                setter(val);
            }
        }

        private static int FindHeaderIndex(List<string> header, string name, int fallback, int occurrence = 0)
        {
            int seen = 0;
            for (int i = 0; i < header.Count; i++)
            {
                if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    if (seen == occurrence)
                        return i;
                    seen++;
                }
            }
            return fallback < header.Count ? fallback : -1;
        }

        private static List<string> ReadAllLinesShared(string path)
        {
            byte[] bytes;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                bytes = ms.ToArray();
            }

            string text = DecodeBytes(bytes);
            var lines = new List<string>();
            using var reader = new StringReader(text);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }
            return lines;
        }

        private static string DecodeBytes(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            try
            {
                var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                return strictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1252).GetString(bytes);
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else
                {
                    if (ch == '"')
                    {
                        inQuotes = true;
                    }
                    else if (ch == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }

        private static bool MatchesWildcard(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;

            string regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("%", ".*")
                .Replace("\\?", ".")
                .Replace("_", ".") + "$";

            return Regex.IsMatch(input ?? string.Empty, regexPattern, RegexOptions.IgnoreCase);
        }

        public static List<string> GetExistingDictionaryFiles(string sourceFieldsCsvPath, string targetDirectory)
        {
            var existing = new List<string>();
            if (string.IsNullOrWhiteSpace(sourceFieldsCsvPath) || string.IsNullOrWhiteSpace(targetDirectory))
                return existing;

            string? sourceDir = System.IO.Path.GetDirectoryName(sourceFieldsCsvPath);
            if (string.IsNullOrEmpty(sourceDir))
                return existing;

            if (File.Exists(System.IO.Path.Combine(targetDirectory, "Fields.csv")))
                existing.Add("Fields.csv");

            foreach (var companion in new[] { "Fields2.csv", "Fields.es" })
            {
                if (File.Exists(System.IO.Path.Combine(sourceDir, companion)) &&
                    File.Exists(System.IO.Path.Combine(targetDirectory, companion)))
                {
                    existing.Add(companion);
                }
            }

            return existing;
        }

        public static void CopyDictionaryFilesToDirectory(string sourceFieldsCsvPath, string targetDirectory)
        {
            if (!File.Exists(sourceFieldsCsvPath))
                throw new FileNotFoundException("Source Fields.csv file not found.", sourceFieldsCsvPath);

            Directory.CreateDirectory(targetDirectory);
            File.Copy(sourceFieldsCsvPath, System.IO.Path.Combine(targetDirectory, "Fields.csv"), overwrite: true);

            string? sourceDir = System.IO.Path.GetDirectoryName(sourceFieldsCsvPath);
            if (!string.IsNullOrEmpty(sourceDir))
            {
                foreach (var companion in new[] { "Fields2.csv", "Fields.es" })
                {
                    string src = System.IO.Path.Combine(sourceDir, companion);
                    if (File.Exists(src))
                    {
                        File.Copy(src, System.IO.Path.Combine(targetDirectory, companion), overwrite: true);
                    }
                }
            }
        }

        private static int ParseFieldSortNumber(string fieldName)
        {
            if (fieldName.Length > 1 &&
                (fieldName[0] == 'F' || fieldName[0] == 'f') &&
                int.TryParse(fieldName.AsSpan(1), out int num))
            {
                return num;
            }
            return int.MaxValue;
        }

        private sealed class LoadedDictionary
        {
            public string FieldsCsvPath { get; }
            public DateTime FieldsCsvTimestamp { get; init; }
            public DateTime Fields2CsvTimestamp { get; init; }
            public DateTime FieldsEsTimestamp { get; init; }
            public bool HasFields2Csv { get; set; }
            public bool HasFieldsEs { get; set; }
            public string? ErrorMessage { get; set; }

            public Dictionary<string, FieldDictionaryEntry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

            private readonly Dictionary<string, HashSet<string>> _compositePkByTable = new(StringComparer.OrdinalIgnoreCase);

            public LoadedDictionary(string path)
            {
                FieldsCsvPath = path;
            }

            public void BuildCompositeKeyIndex()
            {
                _compositePkByTable.Clear();
                foreach (var entry in Entries.Values)
                {
                    foreach (var kvp in entry.TableFlags)
                    {
                        if (kvp.Value.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!_compositePkByTable.TryGetValue(kvp.Key, out var pkSet))
                            {
                                pkSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                _compositePkByTable[kvp.Key] = pkSet;
                            }
                            pkSet.Add(entry.Field);

                            var parts = kvp.Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 1; i < parts.Length; i++)
                            {
                                pkSet.Add(parts[i].Trim());
                            }
                        }
                    }
                }
            }

            public bool IsPrimaryKey(string dctName, string fieldName, string flag)
            {
                if (flag.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                    return true;

                return !string.IsNullOrEmpty(dctName) &&
                       _compositePkByTable.TryGetValue(dctName, out var pkSet) &&
                       pkSet.Contains(fieldName);
            }

            public bool IsRequired(string dctName, string fieldName, string flag)
            {
                if (IsPrimaryKey(dctName, fieldName, flag))
                    return true;

                return string.Equals(flag, "R", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(flag, "RS", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
