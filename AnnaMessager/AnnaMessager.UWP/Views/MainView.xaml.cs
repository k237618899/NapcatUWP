using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class MainView : MvxWindowsPage
    {
        public MainView()
        {
            InitializeComponent();
            Loaded += MainView_Loaded;
        }

        public new MainViewModel ViewModel => (MainViewModel)DataContext;

        private void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            // 頁面載入時的初始化
            // 預設選擇聊天頁面
            NavigateToPage("Chats");
        }

        private void Navigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                NavigateToPage(tag);
                UpdateSelectedButton(button);
            }
        }

        private void NavigateToPage(string pageTag)
        {
            switch (pageTag)
            {
                case "Chats":
                    ViewModel.SelectedTabIndex = 0;
                    ContentFrame.Navigate(typeof(ChatListView));
                    break;
                case "Contacts":
                    ViewModel.SelectedTabIndex = 1;
                    ContentFrame.Navigate(typeof(ContactsView));
                    break;
                case "Groups":
                    ViewModel.SelectedTabIndex = 2;
                    ContentFrame.Navigate(typeof(GroupsView));
                    break;
                case "Settings":
                    ViewModel.SelectedTabIndex = 3;
                    ContentFrame.Navigate(typeof(SettingsView));
                    break;
            }
        }

        private void UpdateSelectedButton(Button selectedButton)
        {
            // 重置所有按鈕的樣式
            ResetButtonStyle(ChatsButton);
            ResetButtonStyle(ContactsButton);
            ResetButtonStyle(SettingsButton);

            // 設置選中按鈕的樣式
            if (selectedButton != null)
                selectedButton.Background = new SolidColorBrush(
                    Colors.White) { Opacity = 0.2 };
        }

        private void ResetButtonStyle(Button button)
        {
            if (button != null)
                button.Background = new SolidColorBrush(
                    Colors.Transparent);
        }
    }
}