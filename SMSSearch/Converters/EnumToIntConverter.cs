using System;
using System.Globalization;
using System.Windows.Data;

namespace SMS_Search.Converters
{
    public class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum)
            {
                return System.Convert.ToInt32(value);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return Enum.ToObject(targetType, intValue);
            }
            return 0;
        }
    }
}
