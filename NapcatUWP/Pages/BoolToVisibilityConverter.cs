using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace NapcatUWP.Pages
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var boolValue = false;

            if (value is bool) boolValue = (bool)value;

            // 檢查是否要反轉
            if (parameter != null && parameter.ToString() == "Invert") boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}