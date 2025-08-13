using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
            if (sender is TextBox textBox)
                ViewModel?.SearchCommand?.Execute(textBox.Text);
        }
    }
}