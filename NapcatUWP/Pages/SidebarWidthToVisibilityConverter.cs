using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace NapcatUWP.Pages
{
    public class SidebarWidthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double width = (double)value;
            return width > 100 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}