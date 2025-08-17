using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        // parameter = 'invert' 可反轉
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isEmpty = string.IsNullOrEmpty(value as string);
            var invert = parameter as string == "invert";
            if (invert) isEmpty = !isEmpty;
            return isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}