using System;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace AnnaMessager.UWP.Converters
{
    public class PinnedBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                var isPinned = value is bool pinned && pinned;
                if (!isPinned) return new SolidColorBrush(Colors.Transparent);
                var accent = (Color)Windows.UI.Xaml.Application.Current.Resources["SystemAccentColor"];
                byte r = (byte)Math.Min(255, accent.R + 30);
                byte g = (byte)Math.Min(255, accent.G + 30);
                byte bChannel = (byte)Math.Min(255, accent.B + 30);
                return new SolidColorBrush(Color.FromArgb(90, r, g, bChannel));
            }
            catch
            {
                return new SolidColorBrush(Colors.Transparent);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => false;
    }
}