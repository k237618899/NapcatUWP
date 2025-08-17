using System;
using System.Diagnostics;
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

        private async void ServerSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("ServerSettingsView_Loaded 調用");

            // 確保 ViewModel 已經設置
            if (ViewModel != null)
            {
                Debug.WriteLine("ViewModel 存在，嘗試調用 Initialize");
                // 手動調用 Initialize 以確保設定被載入
                try
                {
                    await ViewModel.Initialize();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"手動初始化 ViewModel 失敗: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine("ViewModel 為 null");
            }
        }
    }
}