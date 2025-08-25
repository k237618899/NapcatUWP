using System;
using Windows.UI.Xaml.Data;
using AnnaMessager.Core.Models;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    /// ����Ϣ������ȡ�ı����ݵ�ת����
    /// </summary>
    public class SegmentToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MessageSegment segment && 
                string.Equals(segment.Type, "text", StringComparison.OrdinalIgnoreCase))
            {
                if (segment.Data?.TryGetValue("text", out var textObj) == true && 
                    textObj != null)
                {
                    return textObj.ToString();
                }
            }
            
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}