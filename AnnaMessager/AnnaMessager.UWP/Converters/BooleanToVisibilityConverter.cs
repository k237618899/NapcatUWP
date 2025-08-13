using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    ///     布爾值到可見性轉換器 - UWP 版本，支援反轉參數
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var boolValue = false;

            // 處理 bool 類型
            if (value is bool directBool)
                boolValue = directBool;
            // 處理 int 類型 (如 UnreadCount > 0)
            else if (value is int intValue)
                boolValue = intValue > 0;
            // 處理其他數值類型
            else if (value != null)
                if (double.TryParse(value.ToString(), out var numericValue))
                    boolValue = numericValue > 0;

            // 檢查是否有反向參數
            var isInverted = parameter?.ToString()?.ToLower() == "inverted";

            if (isInverted)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
            {
                var isInverted = parameter?.ToString()?.ToLower() == "inverted";

                if (isInverted)
                    return visibility == Visibility.Collapsed;

                return visibility == Visibility.Visible;
            }

            return false;
        }
    }
}