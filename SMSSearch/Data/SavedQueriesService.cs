using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.Data
{
    public sealed class SavedQueriesChangedMessage : ValueChangedMessage<string>
    {
        public SavedQueriesChangedMessage(string value) : base(value) { }
    }

    public sealed class SavedAdvancedClause
    {
        public SearchLogicalOperator LogicalOperator { get; set; } = SearchLogicalOperator.And;
        public SearchFieldTarget Field { get; set; } = SearchFieldTarget.Description;
        public string? ColumnName { get; set; }
        public SearchComparisonOperator Operator { get; set; } = SearchComparisonOperator.Contains;
        public string Value { get; set; } = string.Empty;
    }

    public sealed class SavedClauseGroupSpan
    {
        public int Start { get; set; }
        public int End { get; set; }
    }

    public sealed class SavedAdvancedQueryDefinition
    {
        public SearchMode Mode { get; set; }
        public bool ShowFields { get; set; } = true;
        public string? SelectedTable { get; set; }
        public bool LastTransaction { get; set; }
        public List<SavedAdvancedClause> Clauses { get; set; } = new();
        public List<SavedClauseGroupSpan> Groups { get; set; } = new();
    }

    public sealed class SavedQueryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public string Sql { get; set; } = string.Empty;
        public SavedAdvancedQueryDefinition? AdvancedQuery { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }

        [JsonIgnore]
        public bool IsAdvanced => AdvancedQuery != null;

        [JsonIgnore]
        public string QueryKindLabel => IsAdvanced ? "ADV" : "SQL";
    }

    public interface ISavedQueriesService
    {
        List<SavedQueryItem> GetAll();
        List<string> GetCategories();
        SavedQueryItem? FindDuplicateByNameAndCategory(string name, string category);
        List<SavedQueryItem> FindDuplicatesBySql(string sql);
        SavedQueryItem Save(string name, string category, string sql, SavedAdvancedQueryDefinition? advancedQuery = null);
        void Delete(string id);
        void MarkUsed(string id);
    }

    public class SavedQueriesService : ISavedQueriesService
    {
        private readonly IStateService _state;
        private readonly ILoggerService _logger;
        private const string SectionName = "SAVED_QUERIES";
        private const string KeyName = "ITEMS";

        public SavedQueriesService(IStateService state, ILoggerService logger)
        {
            _state = state;
            _logger = logger;
        }

        public List<SavedQueryItem> GetAll()
        {
            string? json = _state.GetValue(SectionName, KeyName);
            if (string.IsNullOrEmpty(json))
            {
                return new List<SavedQueryItem>();
            }

            try
            {
                var items = JsonSerializer.Deserialize<List<SavedQueryItem>>(json) ?? new List<SavedQueryItem>();
                return items
                    .OrderByDescending(x => x.LastUsedAt ?? x.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to deserialize saved queries: {ErrorMessage}", ex.Message);
                return new List<SavedQueryItem>();
            }
        }

        public List<string> GetCategories()
        {
            var categories = GetAll()
                .Select(x => string.IsNullOrWhiteSpace(x.Category) ? "General" : x.Category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!categories.Any(c => c.Equals("General", StringComparison.OrdinalIgnoreCase)))
            {
                categories.Insert(0, "General");
            }

            return categories;
        }

        public static string SuggestName(string? sql, int maxLength = 42)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return string.Empty;
            }

            var lines = sql.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            var meaningfulLines = new List<string>();
            foreach (var rawLine in lines)
            {
                string trimmed = rawLine.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }
                meaningfulLines.Add(trimmed);
            }

            string collapsed = string.Join(" ", meaningfulLines.Count > 0 ? meaningfulLines : lines.Select(l => l.Trim()));
            collapsed = System.Text.RegularExpressions.Regex.Replace(collapsed, @"\s+", " ").Trim();

            if (collapsed.Length <= maxLength)
            {
                return collapsed;
            }

            return collapsed.Substring(0, maxLength - 3).TrimEnd() + "...";
        }

        public static string NormalizeSql(string? sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return string.Empty;
            }

            return System.Text.RegularExpressions.Regex.Replace(sql.Trim(), @"\s+", " ");
        }

        public SavedQueryItem? FindDuplicateByNameAndCategory(string name, string category)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string trimmedName = name.Trim();
            string trimmedCategory = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();

            return GetAll().FirstOrDefault(x =>
                x.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase) &&
                x.Category.Equals(trimmedCategory, StringComparison.OrdinalIgnoreCase));
        }

        public List<SavedQueryItem> FindDuplicatesBySql(string sql)
        {
            string normalized = NormalizeSql(sql);
            if (string.IsNullOrEmpty(normalized))
            {
                return new List<SavedQueryItem>();
            }

            return GetAll()
                .Where(x => NormalizeSql(x.Sql).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public SavedQueryItem Save(string name, string category, string sql, SavedAdvancedQueryDefinition? advancedQuery = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Query name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL text is required.", nameof(sql));

            string trimmedName = name.Trim();
            string trimmedCategory = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();

            var items = GetAll();
            var existing = items.FirstOrDefault(x =>
                x.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase) &&
                x.Category.Equals(trimmedCategory, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Sql = sql;
                existing.AdvancedQuery = advancedQuery;
                existing.LastUsedAt = DateTime.UtcNow;
                Persist(items);
                WeakReferenceMessenger.Default.Send(new SavedQueriesChangedMessage(existing.Id));
                return existing;
            }

            var item = new SavedQueryItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = trimmedName,
                Category = trimmedCategory,
                Sql = sql,
                AdvancedQuery = advancedQuery,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            };
            items.Insert(0, item);
            Persist(items);
            WeakReferenceMessenger.Default.Send(new SavedQueriesChangedMessage(item.Id));
            return item;
        }

        public void Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var items = GetAll();
            int removed = items.RemoveAll(x => x.Id == id);
            if (removed > 0)
            {
                Persist(items);
                WeakReferenceMessenger.Default.Send(new SavedQueriesChangedMessage(id));
            }
        }

        public void MarkUsed(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var items = GetAll();
            var item = items.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.LastUsedAt = DateTime.UtcNow;
                Persist(items);
            }
        }

        private void Persist(List<SavedQueryItem> items)
        {
            try
            {
                string json = JsonSerializer.Serialize(items);
                _state.SetValue(SectionName, KeyName, json);
                _state.Save();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save queries");
            }
        }
    }
}

