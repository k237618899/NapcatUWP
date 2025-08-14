using System;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    public class TestingTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isTesting)
                return isTesting ? "測試中..." : "測試連接";

            return "測試連接";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}