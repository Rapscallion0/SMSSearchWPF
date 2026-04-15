using SMS_Search.Data;
using SMS_Search.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SMS_Search.Services
{
    public class IntellisenseService : IIntellisenseService
    {
        private readonly IDataRepository _repository;
        private readonly ILoggerService _logger;
        private readonly IConfigService _configService;

        private Dictionary<string, List<string>> _schemaCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Level 1: Standard Keywords
        private readonly List<string> _keywords = new List<string>
        {
            "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE",
            "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "ON", "GROUP", "BY", "ORDER",
            "HAVING", "LIMIT", "TOP", "DISTINCT", "AS", "AND", "OR", "NOT", "NULL", "IS",
            "IN", "LIKE", "BETWEEN", "UNION", "ALL", "CASE", "WHEN", "THEN", "ELSE", "END",
            "EXEC", "DECLARE", "CREATE", "ALTER", "DROP", "TABLE", "VIEW",
            "PROCEDURE", "FUNCTION", "INDEX", "CONSTRAINT", "PRIMARY", "KEY", "FOREIGN",
            "REFERENCES", "DEFAULT", "CHECK", "UNIQUE", "TRANSACTION", "COMMIT", "ROLLBACK",
            "WITH", "NOLOCK", "CROSS", "APPLY"
        };

        // Level 2: Functional (+ SQL Functions)
        private readonly List<string> _functionalKeywords = new List<string>
        {
            "SUM", "AVG", "MIN", "MAX", "COUNT", "GETDATE", "CAST", "CONVERT", "COALESCE",
            "NULLIF", "ISNULL", "DATEADD", "DATEDIFF", "DATENAME", "DATEPART", "YEAR",
            "MONTH", "DAY", "LEN", "LTRIM", "RTRIM", "SUBSTRING", "REPLACE", "CHARINDEX",
            "UPPER", "LOWER", "ABS", "ROUND", "FLOOR", "CEILING"
        };

        // Level 3: Full (+ Admin/DCL commands)
        private readonly List<string> _adminKeywords = new List<string>
        {
            "GRANT", "REVOKE", "DENY", "BACKUP", "RESTORE", "TRUNCATE", "DBCC",
            "sp_who", "sp_who2", "sp_help", "sp_helptext", "xp_cmdshell",
            "ALTER DATABASE", "CREATE LOGIN", "CREATE USER", "sys.tables",
            "sys.columns", "sys.objects", "sys.databases", "sys.sysprocesses"
        };

        public bool IsEnabled { get; set; } = true;
        public bool IsReady { get; private set; } = false;
        public bool AutoTriggerEnabled { get; set; } = true;

        public bool StandardEnabled { get; set; } = true;
        public bool FunctionalEnabled { get; set; } = true;
        public bool FullEnabled { get; set; } = true;

        public bool StandardAutoEnabled { get; set; } = true;
        public bool FunctionalAutoEnabled { get; set; } = true;
        public bool FullAutoEnabled { get; set; } = true;

        public IntellisenseService(IDataRepository repository, ILoggerService logger, IConfigService configService)
        {
            _repository = repository;
            _logger = logger;
            _configService = configService;

            // Load initial setting
            var enabledStr = _configService.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.EnableIntellisense);
            if (enabledStr != null)
            {
                if (enabledStr == "0") IsEnabled = false;
                else if (enabledStr == "1") IsEnabled = true;
                else if (bool.TryParse(enabledStr, out bool b)) IsEnabled = b;
                else IsEnabled = true; // Default
            }
            else
            {
                IsEnabled = true; // Default
            }

            // Load Auto Trigger Setting
            var autoTriggerStr = _configService.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.AutoTriggerIntellisense);
            if (autoTriggerStr != null)
            {
                if (autoTriggerStr == "0") AutoTriggerEnabled = false;
                else if (autoTriggerStr == "1") AutoTriggerEnabled = true;
                else if (bool.TryParse(autoTriggerStr, out bool b)) AutoTriggerEnabled = b;
                else AutoTriggerEnabled = true;
            }
            else
            {
                AutoTriggerEnabled = true; // Default
            }

            // Load Level Settings
            StandardEnabled = GetBoolSetting("INTELLISENSE_STANDARD", true);
            FunctionalEnabled = GetBoolSetting("INTELLISENSE_FUNCTIONAL", true);
            FullEnabled = GetBoolSetting("INTELLISENSE_FULL", true);

            // Load Level Auto Settings
            StandardAutoEnabled = GetBoolSetting("INTELLISENSE_STANDARD_AUTO", true);
            FunctionalAutoEnabled = GetBoolSetting("INTELLISENSE_FUNCTIONAL_AUTO", true);
            FullAutoEnabled = GetBoolSetting("INTELLISENSE_FULL_AUTO", true);
        }

        private bool GetBoolSetting(string key, bool defaultValue)
        {
            var val = _configService.GetValue(AppSettings.Sections.Editor, key);
            if (val == "1") return true;
            if (val == "0") return false;
            return defaultValue;
        }

        public IntellisenseLevel GetInitialLevel()
        {
            return IntellisenseLevel.Schema;
        }

        public IntellisenseLevel GetNextLevel(IntellisenseLevel current)
        {
            int start = (int)current;
            for (int i = 1; i <= 3; i++)
            {
                int next = (start + i) % 4;
                if (IsLevelEnabled((IntellisenseLevel)next))
                {
                    return (IntellisenseLevel)next;
                }
            }
            return IntellisenseLevel.Schema;
        }

        private bool IsLevelEnabled(IntellisenseLevel level)
        {
            switch (level)
            {
                case IntellisenseLevel.Schema: return true; // Always enabled if intellisense is on
                case IntellisenseLevel.Standard: return StandardEnabled;
                case IntellisenseLevel.Functional: return FunctionalEnabled;
                case IntellisenseLevel.Full: return FullEnabled;
                default: return false;
            }
        }

        public async Task InitializeAsync(string server, string database, string? user, string? pass)
        {
            if (!IsEnabled)
            {
                _logger.LogInfo("IntellisenseService: Disabled in settings.");
                return;
            }

            try
            {
                IsReady = false;
                _logger.LogInfo($"IntellisenseService: Loading schema for {server}.{database}...");
                _schemaCache = await _repository.GetDatabaseSchemaAsync(server, database, user, pass);
                IsReady = true;
                _logger.LogInfo($"IntellisenseService: Schema loaded successfully ({_schemaCache.Count} tables).");
            }
            catch (Exception ex)
            {
                _logger.LogError($"IntellisenseService: Failed to load schema for {server}.{database}. Error: {ex.Message}", ex);
                IsReady = false;
            }
        }

        public IEnumerable<CompletionItem> GetCompletions(string text, int caretOffset, IntellisenseLevel level)
        {
            var stopwatch = Stopwatch.StartNew();

            if (!IsEnabled || !IsReady || string.IsNullOrEmpty(text))
                return Enumerable.Empty<CompletionItem>();

            if (caretOffset < 0) caretOffset = 0;
            if (caretOffset > text.Length) caretOffset = text.Length;

            // Use a local reference for thread safety during read
            var schema = _schemaCache;

            // Parsing backwards to find context
            // We want to find if we are in a "Table.Column" context
            // 1. Skip current identifier characters backwards
            int pos = caretOffset - 1;
            int identifierEnd = pos;

            while (pos >= 0)
            {
                char c = text[pos];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '$') // Identifier chars
                    pos--;
                else
                    break;
            }

            // 'pos' is the char BEFORE the start of identifier
            int identifierStart = pos + 1;
            string currentWord = "";
            if (identifierStart <= identifierEnd)
            {
                currentWord = text.Substring(identifierStart, identifierEnd - identifierStart + 1);
            }

            // Now 'pos' is at the character before the current identifier (or at -1)
            bool isDotContext = false;
            string parentIdentifier = "";

            if (pos >= 0 && text[pos] == '.')
            {
                // We are after a dot. Find the parent identifier.
                int endOfParent = pos - 1;
                int startOfParent = endOfParent;
                while (startOfParent >= 0)
                {
                    char c = text[startOfParent];
                    if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '$' || c == ']' || c == '[')
                        startOfParent--;
                    else
                        break;
                }
                // Determine identifier
                if (endOfParent >= 0)
                {
                    // Calculate length carefully
                    int length = endOfParent - startOfParent;
                    if (length > 0)
                    {
                        parentIdentifier = text.Substring(startOfParent + 1, length).Trim();
                        parentIdentifier = parentIdentifier.Trim('[', ']'); // Remove brackets
                        isDotContext = true;
                    }
                }
            }

            var results = new List<CompletionItem>();

            if (isDotContext)
            {
                 // Return columns for parentIdentifier (Level 0+ always includes this)
                 if (schema.TryGetValue(parentIdentifier, out var columns))
                 {
                     foreach (var col in columns)
                     {
                         if (string.IsNullOrEmpty(currentWord) || col.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                         {
                             results.Add(new CompletionItem { Text = col, Description = $"Column in {parentIdentifier}", Type = "Column", Priority = 2.0 });
                         }
                     }
                 }
            }
            else
            {
                // Not triggered by dot (Ctrl+Space or typing start of word)

                // Simple Context Detection: Find tables mentioned in the query
                // Look for FROM/JOIN [TableName]
                var mentionedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var regex = new Regex(@"\b(FROM|JOIN)\s+([\[\]\w\.]+)", RegexOptions.IgnoreCase);
                foreach (Match match in regex.Matches(text))
                {
                    if (match.Groups.Count > 2)
                    {
                        string rawName = match.Groups[2].Value;
                        string tableName = rawName;
                        if (rawName.Contains('.'))
                        {
                            var parts = rawName.Split('.');
                            tableName = parts.Last();
                        }
                        tableName = tableName.Trim('[', ']');
                        mentionedTables.Add(tableName);
                    }
                }

                // Add Keywords based on Level AND configuration
                // Include if level is explicitly requested OR if Auto-Trigger is enabled for that level
                if (StandardEnabled && (level >= IntellisenseLevel.Standard || StandardAutoEnabled))
                {
                    foreach (var kw in _keywords)
                    {
                        if (string.IsNullOrEmpty(currentWord) || kw.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                            results.Add(new CompletionItem { Text = kw, Description = "Keyword", Type = "Keyword", Priority = 0.5 });
                    }
                }

                if (FunctionalEnabled && (level >= IntellisenseLevel.Functional || FunctionalAutoEnabled))
                {
                    foreach (var kw in _functionalKeywords)
                    {
                        if (string.IsNullOrEmpty(currentWord) || kw.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                            results.Add(new CompletionItem { Text = kw, Description = "Function", Type = "Function", Priority = 0.6 });
                    }
                }

                if (FullEnabled && (level >= IntellisenseLevel.Full || FullAutoEnabled))
                {
                    foreach (var kw in _adminKeywords)
                    {
                        if (string.IsNullOrEmpty(currentWord) || kw.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                            results.Add(new CompletionItem { Text = kw, Description = "Admin/DCL", Type = "Admin", Priority = 0.4 });
                    }
                }

                // Add Tables (Level 0+)
                foreach (var kvp in schema)
                {
                    string table = kvp.Key;

                    if (string.IsNullOrEmpty(currentWord) || table.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                    {
                        // Boost priority if table is already in query (maybe user wants to type it again?)
                        double priority = mentionedTables.Contains(table) ? 1.5 : 1.0;
                        results.Add(new CompletionItem { Text = table, Description = "Table", Type = "Table", Priority = priority });
                    }

                    // Add Columns if table is in context (context-aware prioritization)
                    if (mentionedTables.Contains(table))
                    {
                        foreach (var col in kvp.Value)
                        {
                            if (string.IsNullOrEmpty(currentWord) || col.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                            {
                                results.Add(new CompletionItem { Text = col, Description = $"Column ({table})", Type = "Column", Priority = 1.2 });
                            }
                        }
                    }
                }
            }

            stopwatch.Stop();
            long elapsed = stopwatch.ElapsedMilliseconds;

            // Log fetch time if it's significant or if debug logging is useful
            // User requested tracking fetch times.
            if (elapsed > 50)
            {
                _logger.LogInfo($"Intellisense fetch ({level}) took {elapsed}ms. Found {results.Count} items.");
            }
            else
            {
                _logger.LogDebug($"Intellisense fetch ({level}) took {elapsed}ms. Found {results.Count} items.");
            }

            // Note: AvalonEdit filters the list based on what the user has already typed in the current word.
            // We return a broad set, and the UI handles the filtering.
            // Update: We now pre-filter here to avoid empty popups if nothing matches.

            return results.OrderByDescending(x => x.Priority).ThenBy(x => x.Text);
        }
    }
}
