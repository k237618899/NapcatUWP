using Windows.UI.Xaml;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class SettingsView : MvxWindowsPage
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        public new SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // 頁面載入時的初始化
        }
    }
}