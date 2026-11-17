using System;
using System.Globalization;
using System.Windows.Data;
using SMS_Search.Services;

namespace SMS_Search.Converters
{
    public class DictionarySourceShortConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DictionarySourceMode mode)
            {
                return mode switch
                {
                    DictionarySourceMode.Auto => "Auto",
                    DictionarySourceMode.Csv => "CSV",
                    DictionarySourceMode.Database => "DB",
                    _ => mode.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

