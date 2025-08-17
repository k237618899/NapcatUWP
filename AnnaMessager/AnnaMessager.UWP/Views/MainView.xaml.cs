using System.Diagnostics;
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
            Debug.WriteLine("[MainView] Constructor 完成, DataContext=" + (DataContext?.GetType().Name ?? "null"));
            Loaded += MainView_Loaded;
            SizeChanged += MainView_SizeChanged;
        }

        public new MainViewModel ViewModel => (MainViewModel)DataContext;

        private void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualState(ActualWidth);
            // 預設顯示聊天列表並標記選中
            NavigateToPage("Chats");
            ApplySelectedVisualStates();
            Debug.WriteLine("[MainView] Loaded 觸發, ViewModel=" + (ViewModel?.GetType().Name ?? "null"));
            if (ViewModel != null)
            {
                if (ViewModel.ChatListViewModel?.ChatList?.Count == 0)
                {
                    Debug.WriteLine("[MainView] 強制呼叫 MainViewModel.Initialize()");
                    _ = ViewModel.Initialize();
                }
            }
        }

        private void MainView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisualState(e.NewSize.Width);
        }

        private void UpdateVisualState(double width)
        {
            if (width < 640)
                VisualStateManager.GoToState(this, "Narrow", true);
            else
                VisualStateManager.GoToState(this, "Wide", true);
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen;
        }

        private void Navigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                // 如果已在該頁，不再重新導航或取消選中，只刷新視覺狀態
                if (IsTagAlreadySelected(tag))
                {
                    ApplySelectedVisualStates();
                    return;
                }
                NavigateToPage(tag);
                ApplySelectedVisualStates();
            }
        }

        private bool IsTagAlreadySelected(string tag)
        {
            if (ViewModel == null) return false;
            switch (tag)
            {
                case "Chats": return ViewModel.SelectedTabIndex == 0;
                case "Contacts": return ViewModel.SelectedTabIndex == 1;
                case "Groups": return ViewModel.SelectedTabIndex == 2;
                case "Settings": return ViewModel.SelectedTabIndex == 3;
            }
            return false;
        }

        private void ApplySelectedVisualStates()
        {
            ApplySelectedState(ChatsButton, ViewModel?.SelectedTabIndex == 0);
            ApplySelectedState(ContactsButton, ViewModel?.SelectedTabIndex == 1);
            ApplySelectedState(GroupsButton, ViewModel?.SelectedTabIndex == 2);
            ApplySelectedState(SettingsButton, ViewModel?.SelectedTabIndex == 3);
        }

        private void ApplySelectedState(Button button, bool isSelected)
        {
            if (button == null) return;
            try
            {
                var stateName = isSelected ? "Selected" : "Normal";
                VisualStateManager.GoToState(button, stateName, true);
            }
            catch { }
        }

        private void NavigateToPage(string tag)
        {
            Debug.WriteLine($"[MainView] NavigateToPage: {tag}");
            if (ViewModel == null || ContentFrame == null) return;
            object pageContent = null;
            try
            {
                switch (tag)
                {
                    case "Chats":
                        ViewModel.SelectedTabIndex = 0;
                        pageContent = new ChatListView { DataContext = ViewModel.ChatListViewModel };
                        break;
                    case "Contacts":
                        ViewModel.SelectedTabIndex = 1;
                        if (ViewModel.ContactsViewModel?.Contacts?.Count == 0 && !ViewModel.ContactsViewModel.IsRefreshing)
                            _ = ViewModel.ContactsViewModel.Initialize();
                        pageContent = new ContactsView { DataContext = ViewModel.ContactsViewModel };
                        break;
                    case "Groups":
                        ViewModel.SelectedTabIndex = 2;
                        pageContent = new GroupsView { DataContext = ViewModel.GroupsViewModel };
                        break;
                    case "Settings":
                        ViewModel.SelectedTabIndex = 3;
                        pageContent = new SettingsView { DataContext = ViewModel.SettingsViewModel };
                        break;
                }
                if (pageContent != null)
                    ContentFrame.Content = pageContent;
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[MainView] NavigateToPage 發生例外: {ex.Message}");
            }
        }
    }
}