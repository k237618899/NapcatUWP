using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace NapcatUWP.Pages
{
    public class SidebarWidthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double width) return width > 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}