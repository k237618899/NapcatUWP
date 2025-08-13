using System;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    ///     提取第一個字符的轉換器，用於頭像顯示
    /// </summary>
    public class FirstLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string text && !string.IsNullOrEmpty(text)) return text.Substring(0, 1).ToUpper();
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}