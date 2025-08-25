using System;
using Windows.UI.Xaml.Data;
using System.Diagnostics;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    /// 将头像路径转换为 UWP 可用的 URI 格式
    /// </summary>
    public class PathToUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    // 修复：支持多种 URI 格式
                    // 如果已经是有效的 URI 格式，直接返回
                    if (path.StartsWith("ms-appdata:///") || 
                        path.StartsWith("ms-appx:///") ||
                        path.StartsWith("http://") ||
                        path.StartsWith("https://"))
                    {
                        Debug.WriteLine($"PathToUriConverter: 直接使用URI: {path}");
                        return new Uri(path);
                    }
                    
                    // 修复：检查是否是本地文件路径（绝对路径）
                    if (System.IO.Path.IsPathRooted(path))
                    {
                        // 尝试转换为 ms-appdata URI
                        try
                        {
                            var tempFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder.Path;
                            if (path.StartsWith(tempFolder))
                            {
                                // 提取相对于temp文件夹的路径
                                var relativePath = path.Substring(tempFolder.Length).TrimStart('\\', '/');
                                var msAppDataUri = $"ms-appdata:///temp/{relativePath}";
                                Debug.WriteLine($"PathToUriConverter: 转换本地路径: {path} -> {msAppDataUri}");
                                return new Uri(msAppDataUri);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"PathToUriConverter: 转换本地路径失败: {ex.Message}");
                        }
                        
                        // 如果转换失败，尝试直接作为文件URI
                        try
                        {
                            var fileUri = new Uri(path);
                            Debug.WriteLine($"PathToUriConverter: 直接文件URI: {path}");
                            return fileUri;
                        }
                        catch
                        {
                            Debug.WriteLine($"PathToUriConverter: 无法解析路径: {path}");
                            return null;
                        }
                    }
                    
                    // 修复：对于相对路径或其他格式，尝试直接返回
                    Debug.WriteLine($"PathToUriConverter: 原样返回: {path}");
                    return path;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PathToUriConverter error: {ex.Message}, Path: {path}");
                    return value;
                }
            }
            
            Debug.WriteLine($"PathToUriConverter: 空值或非字符串: {value}");
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}