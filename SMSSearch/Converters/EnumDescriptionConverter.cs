using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace SMS_Search.Converters
{
    public class EnumDescriptionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum enumValue)
            {
                string? name = value.ToString();
                if (name != null)
                {
                    FieldInfo? field = value.GetType().GetField(name);
                    if (field != null)
                    {
                        DescriptionAttribute? attribute =
                            Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
                        if (attribute != null)
                        {
                            return attribute.Description;
                        }
                    }
                }
                return value.ToString();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
