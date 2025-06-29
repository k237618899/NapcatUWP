using NapcatUWP.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace NapcatUWP.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainView : Page
    {
        private bool _sidebarOpen = false;
        public MainView()
        {
            this.InitializeComponent();
            InitializeAvatorAndInfo();
            SidebarColumn.Width = new GridLength(0);
            UpdateOverlay();
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarOpen)
            {
                SidebarColumn.Width = new GridLength(0);
            }
            else
            {
                SidebarColumn.Width = new GridLength(280);
            }
            _sidebarOpen = !_sidebarOpen;
            UpdateOverlay();
        }

        private void OverlayRect_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_sidebarOpen)
            {
                SidebarColumn.Width = new GridLength(0);
                _sidebarOpen = false;
                UpdateOverlay();
            }
        }

        private void UpdateOverlay()
        {
            OverlayRect.Visibility = _sidebarOpen ? Visibility.Visible : Visibility.Collapsed;
        }
        private void InitializeAvatorAndInfo()
        {
            MainPage.SocketClientStarter._socket.Send(JSONTools.ActionToJSON("get_login_info", new JsonObject(), "login_info"));
        }

        public void UpdateInfoSafe(double id, string name)
        {
            UpdateInfo(id,name);
        }
        private void UpdateInfo(double id, string name)
        {
            TextUser.Text = name;
            TextID.Text = id.ToString();
        }
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }
        private async void SidebarListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as ListView;
            var selectedItem = listView.SelectedItem as ListViewItem;
            if (selectedItem != null && selectedItem.Content.ToString() == "Log Out")
            {
                // 顯示紫色進度環
                LogoutMask.Visibility = Visibility.Visible;
                LogoutProgressRing.IsActive = true;

                // 關閉 WebSocket（async void，無法await，只能延遲一會兒）
                MainPage.SocketClientStarter._socket.Close(1000, "logout");
                await Task.Delay(600); // 給一點時間確保連接關閉

                // 隱藏進度環並導航
                LogoutProgressRing.IsActive = false;
                LogoutMask.Visibility = Visibility.Collapsed;
                Frame.Navigate(typeof(MainPage));
                listView.SelectedItem = null;
            }
        }
    }

}
