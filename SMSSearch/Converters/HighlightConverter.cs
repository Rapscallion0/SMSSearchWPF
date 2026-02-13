using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Controls;

namespace SMS_Search.Converters
{
    public class HighlightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: Cell Content (UIElement usually)
            // values[1]: Filter Text (string)
            // values[2]: IsHighlightEnabled (bool)
            // values[3]: HighlightColor (Brush)

            if (values.Length < 4) return System.Windows.Media.Brushes.Transparent;

            try
            {
                // Check if highlight is enabled
                if (values[2] is bool isEnabled && !isEnabled) return System.Windows.Media.Brushes.Transparent;
                if (values[2] == System.Windows.DependencyProperty.UnsetValue) return System.Windows.Media.Brushes.Transparent;

                string filterText = values[1] as string ?? "";
                if (string.IsNullOrEmpty(filterText)) return System.Windows.Media.Brushes.Transparent;

                var highlightBrush = values[3] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Yellow;

                string? cellText = null;
                if (values[0] is System.Windows.Controls.TextBlock tb)
                {
                    cellText = tb.Text;
                }
                else if (values[0] is string s)
                {
                    cellText = s;
                }
                else if (values[0] != null && !(values[0] is System.Windows.Controls.CheckBox))
                {
                    // Fallback for other controls, but exclude CheckBox
                    cellText = values[0].ToString();
                }

                if (string.IsNullOrEmpty(cellText)) return System.Windows.Media.Brushes.Transparent;

                if (cellText.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return highlightBrush;
                }
            }
            catch
            {
                // Ignore conversion errors
            }

            return System.Windows.Media.Brushes.Transparent;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
