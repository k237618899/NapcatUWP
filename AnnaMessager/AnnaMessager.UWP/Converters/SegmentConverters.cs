using System;
using System.Collections.Generic;
using System.Reflection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    internal static class SegmentHelper
    {
        private static PropertyInfo FindPropertyRecursive(Type type, string name)
        {
            while (type != null)
            {
                var ti = type.GetTypeInfo();
                var prop = ti.GetDeclaredProperty(name);
                if (prop != null) return prop;
                type = ti.BaseType; // 繼續往基底類找
            }
            return null;
        }
        private static object GetProp(object seg, string name)
        {
            if (seg == null || string.IsNullOrEmpty(name)) return null;
            try
            {
                var prop = FindPropertyRecursive(seg.GetType(), name);
                return prop?.GetValue(seg);
            }
            catch { return null; }
        }
        public static string GetSegmentType(object seg) => GetProp(seg, "Type") as string;
        public static IDictionary<string, object> GetSegmentData(object seg) => GetProp(seg, "Data") as IDictionary<string, object>;
    }

    public class SegmentIsTextVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var t = SegmentHelper.GetSegmentType(value);
            return string.Equals(t, "text", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    public class SegmentIsImageVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var t = SegmentHelper.GetSegmentType(value);
            return string.Equals(t, "image", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    public class SegmentToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var t = SegmentHelper.GetSegmentType(value)?.ToLower();
            var data = SegmentHelper.GetSegmentData(value);
            if (t == null) return string.Empty;
            if (t == "text")
            {
                if (data != null && data.TryGetValue("text", out var txt)) return txt?.ToString() ?? string.Empty;
                return string.Empty;
            }
            if (t == "reply")
            {
                if (data != null && data.TryGetValue("id", out var id)) return "回覆 #" + id;
                return "回覆";
            }
            if (t == "at")
            {
                if (data != null && data.TryGetValue("qq", out var qq)) return qq?.ToString() == "all" ? "@所有人" : "@" + qq;
                return "@?";
            }
            return string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    public class SegmentToImageUrlConverter : IValueConverter
    {
        private string Sanitize(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            // 只需要處理常見 &amp; 編碼即可（NapCat 回傳 raw_message 中常見）
            if (url.IndexOf("&amp;", StringComparison.OrdinalIgnoreCase) >= 0)
                url = url.Replace("&amp;", "&");
            return url;
        }
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var t = SegmentHelper.GetSegmentType(value);
            if (!string.Equals(t, "image", StringComparison.OrdinalIgnoreCase)) return null;
            var data = SegmentHelper.GetSegmentData(value);
            if (data == null) return null;
            if (data.TryGetValue("url", out var u) && u != null && !string.IsNullOrEmpty(u.ToString())) return Sanitize(u.ToString());
            if (data.TryGetValue("file", out var f) && f != null && !string.IsNullOrEmpty(f.ToString())) return Sanitize(f.ToString());
            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    public class TypeEqualsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var segType = SegmentHelper.GetSegmentType(value);
            var target = parameter as string;
            if (string.IsNullOrEmpty(segType) || string.IsNullOrEmpty(target)) return Visibility.Collapsed;
            return string.Equals(segType, target, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
