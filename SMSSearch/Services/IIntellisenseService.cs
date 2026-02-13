using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMS_Search.Services
{
    public class CompletionItem
    {
        public string Text { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = ""; // "Table", "Column", "Keyword", "Function", "Admin"
        public double Priority { get; set; }
    }

    public interface IIntellisenseService
    {
        bool IsEnabled { get; set; }
        bool IsReady { get; }
        bool AutoTriggerEnabled { get; set; }
        Task InitializeAsync(string server, string database, string? user, string? pass);
        IEnumerable<CompletionItem> GetCompletions(string text, int caretOffset, IntellisenseLevel level);
    }
}
