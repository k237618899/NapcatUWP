using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using AnnaMessager.Core.Models;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    /// 检查消息段类型是否等于指定类型的可见性转换器
    /// </summary>
    public class TypeEqualsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MessageSegment segment && parameter is string expectedType)
            {
                return string.Equals(segment.Type, expectedType, StringComparison.OrdinalIgnoreCase) 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}