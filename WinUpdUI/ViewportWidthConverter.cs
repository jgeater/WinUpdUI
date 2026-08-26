using System;
using System.Globalization;
using System.Windows.Data;

namespace WinUpdUI
{
    public class ViewportWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string param && double.TryParse(param, out double offset))
                return Math.Max(0, width - offset);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
