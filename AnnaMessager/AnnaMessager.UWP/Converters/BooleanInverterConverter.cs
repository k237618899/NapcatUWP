using System;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    ///     布爾值反轉轉換器 - 用於按鈕 IsEnabled 綁定
    /// </summary>
    public class BooleanInverterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
                return !boolValue;

            return true; // 默認啟用
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
                return !boolValue;

            return false;
        }
    }
}