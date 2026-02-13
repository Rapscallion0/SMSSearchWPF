using SMS_Search.Data;
using SMS_Search.Utils;
using System;
using System.Collections.Generic;
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
        private readonly List<string> _keywords = new List<string>
        {
            "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE",
            "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "ON", "GROUP", "BY", "ORDER",
            "HAVING", "LIMIT", "TOP", "DISTINCT", "AS", "AND", "OR", "NOT", "NULL", "IS",
            "IN", "LIKE", "BETWEEN", "UNION", "ALL", "CASE", "WHEN", "THEN", "ELSE", "END",
            "CAST", "CONVERT", "EXEC", "DECLARE", "CREATE", "ALTER", "DROP", "TABLE", "VIEW",
            "PROCEDURE", "FUNCTION", "INDEX", "CONSTRAINT", "PRIMARY", "KEY", "FOREIGN",
            "REFERENCES", "DEFAULT", "CHECK", "UNIQUE", "TRANSACTION", "COMMIT", "ROLLBACK",
            "GRANT", "REVOKE", "DENY", "WITH", "NOLOCK", "CROSS", "APPLY"
        };

        public bool IsEnabled { get; set; } = true;
        public bool IsReady { get; private set; } = false;
        public bool AutoTriggerEnabled { get; set; } = true;

        public IntellisenseService(IDataRepository repository, ILoggerService logger, IConfigService configService)
        {
            _repository = repository;
            _logger = logger;
            _configService = configService;

            // Load initial setting
            var enabledStr = _configService.GetValue("GENERAL", "ENABLE_INTELLISENSE");
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
            var autoTriggerStr = _configService.GetValue("GENERAL", "AUTO_TRIGGER_INTELLISENSE");
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

        public IEnumerable<CompletionItem> GetCompletions(string text, int caretOffset)
        {
            if (!IsEnabled || !IsReady || string.IsNullOrEmpty(text))
                return Enumerable.Empty<CompletionItem>();

            if (caretOffset < 0) caretOffset = 0;
            if (caretOffset > text.Length) caretOffset = text.Length;

            // Parsing backwards to find context
            // We want to find if we are in a "Table.Column" context
            // 1. Skip current identifier characters backwards
            int pos = caretOffset - 1;
            while (pos >= 0)
            {
                char c = text[pos];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '$') // Identifier chars
                    pos--;
                else
                    break;
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
                 // Return columns for parentIdentifier
                 if (_schemaCache.TryGetValue(parentIdentifier, out var columns))
                 {
                     foreach (var col in columns)
                     {
                         results.Add(new CompletionItem { Text = col, Description = $"Column in {parentIdentifier}", Type = "Column", Priority = 2.0 });
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

                // Add Keywords
                foreach (var kw in _keywords)
                {
                    results.Add(new CompletionItem { Text = kw, Description = "Keyword", Type = "Keyword", Priority = 0.5 });
                }

                // Add Tables
                foreach (var kvp in _schemaCache)
                {
                    string table = kvp.Key;
                    // Boost priority if table is already in query (maybe user wants to type it again?)
                    double priority = mentionedTables.Contains(table) ? 1.5 : 1.0;
                    results.Add(new CompletionItem { Text = table, Description = "Table", Type = "Table", Priority = priority });

                    // Add Columns if table is in context (context-aware prioritization)
                    if (mentionedTables.Contains(table))
                    {
                        foreach (var col in kvp.Value)
                        {
                            results.Add(new CompletionItem { Text = col, Description = $"Column ({table})", Type = "Column", Priority = 1.2 });
                        }
                    }
                }
            }

            // Note: AvalonEdit filters the list based on what the user has already typed in the current word.
            // We return a broad set, and the UI handles the filtering.

            return results.OrderByDescending(x => x.Priority).ThenBy(x => x.Text);
        }
    }
}
