using System;
using System.Linq;
using System.Reflection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    ///     布爾值到可見性轉換器 - UWP 版本，支援反轉參數和布爾值轉換
    ///     擴充: 支援以參數判斷枚舉 MessageType (text|image|voice|video...)
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, object targetTypeObj, object parameter, string language)
        {
            var targetType = targetTypeObj as Type ?? targetTypeObj as Type; // 兼容舊簽名 (MvvmCross 傳入 targetType)
            bool boolValue = false;
            string paramStr = parameter?.ToString();

            // 解析參數 tokens (支援 | , ; 空白 分隔) 並處理 inverted 標誌
            string[] tokens = Array.Empty<string>();
            bool inverted = false;
            if (!string.IsNullOrWhiteSpace(paramStr))
            {
                tokens = paramStr.Split(new[] { '|', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim().ToLower()).ToArray();
                if (tokens.Contains("inverted"))
                {
                    inverted = true;
                    tokens = tokens.Where(t => t != "inverted").ToArray();
                }
            }

            // 如果提供了類型 token 並且 value 是枚舉或字串，依 token 匹配
            if (tokens.Length > 0 && value != null && (value.GetType().GetTypeInfo().IsEnum || value is string))
            {
                var valueName = value.ToString().ToLower();
                boolValue = tokens.Contains(valueName);
            }
            else
            {
                // 原有處理: bool / 數字
                if (value is bool directBool)
                    boolValue = directBool;
                else if (value is int intValue)
                    boolValue = intValue > 0;
                else if (value is long longValue)
                    boolValue = longValue > 0;
                else if (value is double doubleValue)
                    boolValue = Math.Abs(doubleValue) > double.Epsilon;
                else if (value != null && double.TryParse(value.ToString(), out var numericValue))
                    boolValue = Math.Abs(numericValue) > double.Epsilon;
                else
                    boolValue = value != null; // 非空視為 true
            }

            if (inverted) boolValue = !boolValue;

            if (targetType == typeof(bool) || targetType == typeof(bool?))
                return boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Convert(value, (object)targetType, parameter, language);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            bool result;
            if (value is Visibility visibility)
                result = visibility == Visibility.Visible;
            else if (value is bool b)
                result = b;
            else
                result = false;

            var paramStr = parameter?.ToString()?.ToLower();
            if (!string.IsNullOrEmpty(paramStr) && paramStr.Contains("inverted"))
                result = !result;

            return result;
        }
    }
}