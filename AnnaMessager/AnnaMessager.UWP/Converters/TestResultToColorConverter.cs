using System;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace AnnaMessager.UWP.Converters
{
    public class TestResultToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string testResult)
            {
                if (testResult.Contains("成功"))
                    return new SolidColorBrush(Colors.Green);
                if (testResult.Contains("失敗") || testResult.Contains("錯誤"))
                    return new SolidColorBrush(Colors.Red);
            }

            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}