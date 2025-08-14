using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class GroupsView : MvxWindowsPage
    {
        public GroupsView()
        {
            InitializeComponent();
            Loaded += GroupsView_Loaded;
        }

        public new GroupsViewModel ViewModel => (GroupsViewModel)DataContext;

        private void GroupsView_Loaded(object sender, RoutedEventArgs e)
        {
            // 頁面載入時的初始化
        }

        private void GroupsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView &&
                listView.SelectedItem is GroupItem groupItem)
            {
                ViewModel?.OpenChatCommand?.Execute(groupItem);
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