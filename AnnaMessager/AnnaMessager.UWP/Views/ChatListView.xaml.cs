using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media; // for VisualTreeHelper & CompositionTarget
using AnnaMessager.Core.Models;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class ChatListView : MvxWindowsPage
    {
        private bool _shouldMaintainTop;
        private int _pendingScrollVersion;

        public ChatListView()
        {
            InitializeComponent();
            Loaded += ChatListView_Loaded;
            Unloaded += ChatListView_Unloaded;
        }

        public ChatListViewModel ViewModel => (ChatListViewModel)DataContext;

        private void ChatListView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ChatMovedToTop -= ViewModel_ChatMovedToTop; // 避免重複
                ViewModel.ChatMovedToTop += ViewModel_ChatMovedToTop;
            }
        }

        private void ChatListView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ChatMovedToTop -= ViewModel_ChatMovedToTop;
            }
        }

        private void ViewModel_ChatMovedToTop(ChatItem obj)
        {
            NotifyChatMovedToTop(obj);
        }

        private void ChatListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is ChatItem chatItem)
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

        // 提供給 ViewModel 在重新排序後呼叫 (可在 XAML 綁定行為或在程式碼後續注入)；這裡先透過事件方式
        public void NotifyChatMovedToTop(ChatItem item)
        {
            if (ChatListViewControl == null || item == null) return;
            // 判斷是否目前第一項就是 item
            if (ChatListViewControl.Items.Count > 0 && ChatListViewControl.Items[0] == item)
            {
                // 是否滾動條已在頂部
                var sv = GetScrollViewer(ChatListViewControl);
                if (sv != null && Math.Abs(sv.VerticalOffset) < 0.5)
                {
                    // 已在頂部 -> 下一幀強制保持頂部 (避免布局造成偏移)
                    _shouldMaintainTop = true;
                    _pendingScrollVersion++;
                    var currentVersion = _pendingScrollVersion;
                    EventHandler<object> handler = null;
                    handler = (s, a) =>
                    {
                        if (currentVersion != _pendingScrollVersion)
                        {
                            CompositionTarget.Rendering -= handler;
                            return;
                        }
                        if (_shouldMaintainTop)
                        {
                            var sv2 = GetScrollViewer(ChatListViewControl);
                            sv2?.ChangeView(null, 0, null, true);
                            _shouldMaintainTop = false;
                        }
                        CompositionTarget.Rendering -= handler; // 一次性
                    };
                    CompositionTarget.Rendering += handler;
                }
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject root)
        {
            if (root == null) return null;
            if (root is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}