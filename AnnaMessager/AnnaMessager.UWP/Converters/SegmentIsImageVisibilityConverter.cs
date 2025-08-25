using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using AnnaMessager.Core.Models;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    /// �ж���Ϣ���Ƿ�ΪͼƬ���͵Ŀɼ���ת����
    /// </summary>
    public class SegmentIsImageVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MessageSegment segment)
            {
                return string.Equals(segment.Type, "image", StringComparison.OrdinalIgnoreCase) 
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