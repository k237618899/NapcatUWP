using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class ChatListView : MvxWindowsPage<ChatListViewModel>
    {
        public ChatListView()
        {
            InitializeComponent();
            Loaded += ChatListView_Loaded;
        }

        private void ChatListView_Loaded(object sender, RoutedEventArgs e)
        {
            // 頁面載入完成後的初始化工作
            if (ViewModel != null)
            {
                // 觸發淡入動畫
                var fadeInAnimation = Resources["FadeInAnimation"] as Storyboard;
                fadeInAnimation?.Begin();
            }
        }

        private void ChatListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView &&
                listView.SelectedItem is ChatItem chatItem)
            {
                ViewModel?.OpenChatCommand?.Execute(chatItem);
                listView.SelectedIndex = -1; // 清除選擇狀態
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox) ViewModel?.SearchCommand?.Execute(textBox.Text);
        }

        private void ChatItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // 右鍵點擊事件由 XAML 中的 ContextFlyout 處理
            e.Handled = true;
        }

        private async void RefreshContainer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
        {
            // 處理下拉刷新
            using (var deferral = args.GetDeferral())
            {
                if (ViewModel?.RefreshCommand?.CanExecute(null) == true)
                {
                    ViewModel.RefreshCommand.Execute(null);

                    // 等待刷新完成
                    while (ViewModel.IsRefreshing) await Task.Delay(100);
                }
            }
        }
    }
}