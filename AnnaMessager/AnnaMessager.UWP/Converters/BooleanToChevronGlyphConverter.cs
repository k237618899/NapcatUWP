using System;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    // true(展開) -> 向下圖示, false(收起) -> 向右圖示
    public class BooleanToChevronGlyphConverter : IValueConverter
    {
        // E70D: ChevronDown, E76C: ChevronRight
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var expanded = false;
            if (value is bool b) expanded = b;
            return expanded ? "\uE70D" : "\uE76C";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value != null && value.ToString() == "\uE70D"; // Down -> true
        }
    }
}