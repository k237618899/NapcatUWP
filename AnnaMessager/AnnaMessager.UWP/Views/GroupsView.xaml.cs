using System;
using System.Diagnostics;
using Windows.UI.Xaml;
using Ctl=Windows.UI.Xaml.Controls; // alias
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class GroupsView : MvxWindowsPage
    {
        private bool _opening;
        public GroupsView()
        {
            InitializeComponent();
            Loaded += GroupsView_Loaded;
        }

        public new GroupsViewModel ViewModel => (GroupsViewModel)DataContext;

        private void GroupsView_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void GroupsListView_ItemClick(object sender, Ctl.ItemClickEventArgs e)
        {
            try
            {
                if (_opening) return;
                _opening = true;
                if (e.ClickedItem is AnnaMessager.Core.Models.GroupItem groupItem)
                {
                    ViewModel?.OpenChatCommand?.Execute(groupItem);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"開啟群聊失敗: {ex.Message}");
            }
            finally
            {
                _opening = false;
            }
        }

        private void GroupsListView_SelectionChanged(object sender, Ctl.SelectionChangedEventArgs e)
        {
            // 不再使用 SelectionChanged
            if (sender is Ctl.ListView list) list.SelectedIndex = -1;
        }

        private void SearchBox_TextChanged(object sender, Ctl.TextChangedEventArgs e)
        {
            if (sender is Ctl.TextBox textBox)
                ViewModel?.SearchCommand?.Execute(textBox.Text);
        }
    }
}