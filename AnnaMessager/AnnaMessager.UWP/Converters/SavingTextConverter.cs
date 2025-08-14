using System;
using Windows.UI.Xaml.Data;

namespace AnnaMessager.UWP.Converters
{
    public class SavingTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isSaving)
                return isSaving ? "儲存中..." : "儲存";

            return "儲存";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}