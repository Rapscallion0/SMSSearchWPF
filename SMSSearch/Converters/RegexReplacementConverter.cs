using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace SMS_Search.Converters
{
    /// <summary>
    /// Converts control characters (\r, \n, \t) and backslashes to their escaped string representation for display,
    /// and converts them back to actual characters for editing.
    /// Example: A newline character becomes the string "\n".
    /// </summary>
    public class RegexReplacementConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                // Escape backslashes first to avoid double escaping, then control characters.
                return s.Replace("\\", "\\\\")
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n")
                        .Replace("\t", "\\t");
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        char next = s[i + 1];
                        switch (next)
                        {
                            case 'r':
                                sb.Append('\r');
                                i++;
                                break;
                            case 'n':
                                sb.Append('\n');
                                i++;
                                break;
                            case 't':
                                sb.Append('\t');
                                i++;
                                break;
                            case '\\':
                                sb.Append('\\');
                                i++;
                                break;
                            default:
                                // Not a recognized escape sequence (e.g. \d), treat as literal backslash
                                sb.Append('\\');
                                // Do not increment i, so the next character is processed normally in the next iteration
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(s[i]);
                    }
                }
                return sb.ToString();
            }
            return value;
        }
    }
}
