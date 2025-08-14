using System;
using System.Collections.Specialized;
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class ChatView : MvxWindowsPage
    {
        public ChatView()
        {
            InitializeComponent();
            Loaded += ChatView_Loaded;
        }

        public new ChatViewModel ViewModel => (ChatViewModel)DataContext;

        private void ChatView_Loaded(object sender, RoutedEventArgs e)
        {
            // 設置焦點到輸入框
            MessageTextBox.Focus(FocusState.Programmatic);

            // 滾動到底部顯示最新訊息
            ScrollToBottom();

            // 監聽新消息事件，自動滾動到底部
            if (ViewModel?.Messages != null) ViewModel.Messages.CollectionChanged += Messages_CollectionChanged;
        }

        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // 有新消息時滾動到底部 - 修正：使用 UWP 15063 相容的方法
                var task = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () => ScrollToBottom());
            }
        }

        private void ScrollToBottom()
        {
            try
            {
                // 滾動到底部
                MessagesScrollViewer.ChangeView(null, MessagesScrollViewer.ScrollableHeight, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"滾動到底部失敗: {ex.Message}");
            }
        }

        private void MessageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Enter 發送訊息（Shift+Enter 換行）
            if (e.Key == VirtualKey.Enter)
            {
                var shiftPressed = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift)
                    .HasFlag(CoreVirtualKeyStates.Down);

                if (!shiftPressed)
                {
                    e.Handled = true;

                    // 發送消息
                    if (ViewModel?.SendMessageCommand?.CanExecute(null) == true)
                        ViewModel.SendMessageCommand.Execute(null);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 返回上一頁或主界面
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                // 導航到主界面的聊天列表
                Frame.Navigate(typeof(MainView));
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // 清理事件監聽
            if (ViewModel?.Messages != null) ViewModel.Messages.CollectionChanged -= Messages_CollectionChanged;

            base.OnNavigatedFrom(e);
        }

        private async void AttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 實現文件選擇和發送功能
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".gif");
                picker.FileTypeFilter.Add(".bmp");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                    // 處理文件上傳
                    Debug.WriteLine($"選擇的文件: {file.Name}");
                // TODO: 調用 ViewModel 的文件發送方法
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"選擇文件失敗: {ex.Message}");
            }
        }
    }
}