using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SMS_Search.Utils
{
    public static class MarkdownTableFormatter
    {
        public static string Format(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
        {
            if (headers == null || headers.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append("| ");
            sb.Append(string.Join(" | ", headers.Select(EscapeCell)));
            sb.AppendLine(" |");

            sb.Append("| ");
            sb.Append(string.Join(" | ", headers.Select(_ => "---")));
            sb.AppendLine(" |");

            foreach (var row in rows)
            {
                var cells = new string[headers.Count];
                for (int i = 0; i < headers.Count; i++)
                {
                    cells[i] = i < row.Count ? EscapeCell(row[i]) : string.Empty;
                }

                sb.Append("| ");
                sb.Append(string.Join(" | ", cells));
                sb.AppendLine(" |");
            }

            return sb.ToString();
        }

        private static string EscapeCell(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("|", "\\|")
                .Replace("\r\n", "<br/>")
                .Replace("\n", "<br/>")
                .Replace("\r", "<br/>");
        }
    }
}

