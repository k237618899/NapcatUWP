using Windows.UI.Xaml;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class ServerSettingsView : MvxWindowsPage
    {
        public ServerSettingsView()
        {
            InitializeComponent();
            Loaded += ServerSettingsView_Loaded;
        }

        public new ServerSettingsViewModel ViewModel => (ServerSettingsViewModel)DataContext;

        private void ServerSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // 頁面載入時的初始化
        }
    }
}