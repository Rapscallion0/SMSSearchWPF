using System;
using System.Globalization;
using System.Windows.Data;

namespace SMS_Search.Converters
{
    public class IgnoreNewItemPlaceholderConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && (value.ToString() == "{NewItemPlaceholder}" || value.GetType().FullName == "MS.Internal.NamedObject"))
            {
                return null;
            }
            return value;
        }
    }
}
