using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace SMS_Search.Converters
{
    /// <summary>
    /// Converts control characters (\r, \n, \t) and backslashes to their escaped string representation for display.
    /// Used to allow editing of regex patterns containing newlines/tabs as literal characters.
    /// </summary>
    public class RawStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                // Escape backslashes first to preserve them, then escape control characters.
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
                                // If the user typed \d, we get \d.
                                // But since Convert escapes backslashes, user should see \\d for \d.
                                // If user types \d, it means literal \ followed by d?
                                // If we treat unescaped \ as literal, then \d -> \d.
                                // This allows typing \d without escaping it as \\d?
                                // If so, then \\ -> \ (escaped backslash).
                                // This hybrid approach allows \d to mean \d, but \n to mean newline.
                                // And \\n to mean \n.
                                // And \\d to mean \d?
                                // If user types \\d -> \d.
                                // If user types \d -> \d.
                                // This seems friendly.
                                sb.Append('\\');
                                // Do not increment i, let next char be processed
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
