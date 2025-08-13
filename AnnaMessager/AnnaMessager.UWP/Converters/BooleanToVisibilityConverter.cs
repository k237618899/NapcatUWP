using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    ///     布爾值到可見性轉換器 - UWP 版本
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                // 檢查是否有反向參數
                var isInverted = parameter?.ToString()?.ToLower() == "inverted";

                if (isInverted) return boolValue ? Visibility.Collapsed : Visibility.Visible;

                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
            {
                var isInverted = parameter?.ToString()?.ToLower() == "inverted";

                if (isInverted) return visibility == Visibility.Collapsed;

                return visibility == Visibility.Visible;
            }

            return false;
        }
    }
}