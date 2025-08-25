using System;
using Windows.UI.Xaml.Data;
using AnnaMessager.Core.Models;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    /// 从消息段中提取图片URL的转换器
    /// </summary>
    public class SegmentToImageUrlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MessageSegment segment && 
                string.Equals(segment.Type, "image", StringComparison.OrdinalIgnoreCase))
            {
                // 优先使用url
                if (segment.Data?.TryGetValue("url", out var urlObj) == true && 
                    urlObj != null && !string.IsNullOrEmpty(urlObj.ToString()))
                {
                    return urlObj.ToString();
                }
                
                // 如果没有url，尝试使用file
                if (segment.Data?.TryGetValue("file", out var fileObj) == true && 
                    fileObj != null && !string.IsNullOrEmpty(fileObj.ToString()))
                {
                    return fileObj.ToString();
                }
            }
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}