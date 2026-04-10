using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SMS_Search.Utils;

namespace SMS_Search.Data
{
    public interface IQueryHistoryService
    {
        void AddQuery(string type, string sql);
        List<string> GetHistory(string type);
        void ClearHistory(string type);
    }

    public class QueryHistoryService : IQueryHistoryService
    {
        private readonly IStateService _state;
        private const int MaxHistory = 20;
        private const string SectionName = "QUERY_HISTORY";

        public QueryHistoryService(IStateService state)
        {
            _state = state;
        }

        public void AddQuery(string type, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;

            var history = GetHistory(type);

            history.RemoveAll(x => x.Equals(sql, StringComparison.OrdinalIgnoreCase));
            history.Insert(0, sql);

            if (history.Count > MaxHistory)
            {
                history = history.Take(MaxHistory).ToList();
            }

            SaveHistory(type, history);
        }

        public List<string> GetHistory(string type)
        {
            string? json = _state.GetValue(SectionName, type);
            if (string.IsNullOrEmpty(json)) return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public void ClearHistory(string type)
        {
            SaveHistory(type, new List<string>());
        }

        private void SaveHistory(string type, List<string> history)
        {
            try
            {
                string json = JsonSerializer.Serialize(history);
                _state.SetValue(SectionName, type, json);
                _state.Save();
            }
            catch { }
        }
    }
}
