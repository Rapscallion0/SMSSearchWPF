using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Controls;
using SMS_Search.Data;

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
            // values[4]: Column (DataGridColumn) - Optional but recommended for robust binding
            // values[5]: Row (DataContext) - Optional but recommended for robust binding

            if (values.Length < 4) return System.Windows.Media.Brushes.Transparent;

            try
            {
                // Check if highlight is enabled
                if (values.Length > 2 && values[2] is bool isEnabled && !isEnabled) return System.Windows.Media.Brushes.Transparent;
                if (values.Length > 2 && values[2] == System.Windows.DependencyProperty.UnsetValue) return System.Windows.Media.Brushes.Transparent;

                string filterText = values[1] as string ?? "";
                if (string.IsNullOrEmpty(filterText)) return System.Windows.Media.Brushes.Transparent;

                var highlightBrush = (values.Length > 3) ? (values[3] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Yellow) : System.Windows.Media.Brushes.Yellow;

                string? cellText = null;

                // Priority: Check UI Content (values[0]) first for performance.
                // Accessing VirtualRow properties involves dictionary lookups which are slow during scrolling.
                // Assuming WPF binding has updated the content correctly.

                if (values[0] is System.Windows.Controls.TextBlock tb)
                {
                    cellText = tb.Text;
                }
                else if (values[0] is string s)
                {
                    cellText = s;
                }
                else if (values[0] != null && !(values[0] is System.Windows.Controls.CheckBox) && values[0] != System.Windows.DependencyProperty.UnsetValue)
                {
                    cellText = values[0].ToString();
                }

                // Fallback to Row/Column data if UI content is not available
                if (cellText == null)
                {
                    // Optimized path for VirtualRow to avoid TypeDescriptor overhead
                    if (values.Length >= 6 && values[5] is VirtualRow vRow)
                    {
                        if (values[4] is DataGridColumn col)
                        {
                            string? propName = col.SortMemberPath;
                            if (!string.IsNullOrEmpty(propName))
                            {
                                // Direct property access bypassing TypeDescriptor
                                var props = vRow.GetProperties();
                                var prop = props[propName];
                                if (prop != null)
                                {
                                    var val = prop.GetValue(vRow);
                                    if (val != null) cellText = val.ToString();
                                }
                            }
                        }
                    }
                    else if (values.Length >= 6 && values[4] is DataGridColumn col && values[5] != null && values[5] != System.Windows.DependencyProperty.UnsetValue)
                    {
                        string? propName = col.SortMemberPath;
                        if (!string.IsNullOrEmpty(propName))
                        {
                            // Using TypeDescriptor works for both POCO and CustomTypeDescriptor (like VirtualRow)
                            var props = TypeDescriptor.GetProperties(values[5]);
                            var prop = props[propName];
                            if (prop != null)
                            {
                                var val = prop.GetValue(values[5]);
                                if (val != null) cellText = val.ToString();
                            }
                        }
                    }
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
