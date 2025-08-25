using System;
using Windows.UI.Xaml.Data;
using System.Diagnostics;

namespace AnnaMessager.UWP.Converters
{
    /// <summary>
    /// ��ͷ��·��ת��Ϊ UWP ���õ� URI ��ʽ
    /// </summary>
    public class PathToUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    // �޸���֧�ֶ��� URI ��ʽ
                    // ����Ѿ�����Ч�� URI ��ʽ��ֱ�ӷ���
                    if (path.StartsWith("ms-appdata:///") || 
                        path.StartsWith("ms-appx:///") ||
                        path.StartsWith("http://") ||
                        path.StartsWith("https://"))
                    {
                        Debug.WriteLine($"PathToUriConverter: ֱ��ʹ��URI: {path}");
                        return new Uri(path);
                    }
                    
                    // �޸�������Ƿ��Ǳ����ļ�·��������·����
                    if (System.IO.Path.IsPathRooted(path))
                    {
                        // ����ת��Ϊ ms-appdata URI
                        try
                        {
                            var tempFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder.Path;
                            if (path.StartsWith(tempFolder))
                            {
                                // ��ȡ�����temp�ļ��е�·��
                                var relativePath = path.Substring(tempFolder.Length).TrimStart('\\', '/');
                                var msAppDataUri = $"ms-appdata:///temp/{relativePath}";
                                Debug.WriteLine($"PathToUriConverter: ת������·��: {path} -> {msAppDataUri}");
                                return new Uri(msAppDataUri);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"PathToUriConverter: ת������·��ʧ��: {ex.Message}");
                        }
                        
                        // ���ת��ʧ�ܣ�����ֱ����Ϊ�ļ�URI
                        try
                        {
                            var fileUri = new Uri(path);
                            Debug.WriteLine($"PathToUriConverter: ֱ���ļ�URI: {path}");
                            return fileUri;
                        }
                        catch
                        {
                            Debug.WriteLine($"PathToUriConverter: �޷�����·��: {path}");
                            return null;
                        }
                    }
                    
                    // �޸����������·����������ʽ������ֱ�ӷ���
                    Debug.WriteLine($"PathToUriConverter: ԭ������: {path}");
                    return path;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PathToUriConverter error: {ex.Message}, Path: {path}");
                    return value;
                }
            }
            
            Debug.WriteLine($"PathToUriConverter: ��ֵ����ַ���: {value}");
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}