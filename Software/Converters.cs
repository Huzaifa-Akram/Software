using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Net.Http;

namespace Software
{
    /// <summary>
    /// Converter that returns true if a numeric value is greater than zero
    /// </summary>
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
                return doubleValue > 0;
            else if (value is decimal decimalValue)
                return decimalValue > 0;
            else if (value is int intValue)
                return intValue > 0;
            else if (value is float floatValue)
                return floatValue > 0;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that returns true if a numeric value is less than zero
    /// </summary>
    public class LessThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
                return doubleValue < 0;
            else if (value is decimal decimalValue)
                return decimalValue < 0;
            else if (value is int intValue)
                return intValue < 0;
            else if (value is float floatValue)
                return floatValue < 0;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
