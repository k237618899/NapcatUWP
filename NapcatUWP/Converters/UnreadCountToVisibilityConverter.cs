using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace NapcatUWP.Converters
{
    public class UnreadCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int count) return count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (value is bool boolValue) return boolValue ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}