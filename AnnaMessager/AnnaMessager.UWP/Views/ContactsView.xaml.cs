using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class ContactsView : MvxWindowsPage
    {
        public ContactsView()
        {
            InitializeComponent();
            Loaded += ContactsView_Loaded;
        }

        public new ContactsViewModel ViewModel => (ContactsViewModel)DataContext;

        private void ContactsView_Loaded(object sender, RoutedEventArgs e)
        {
            // 頁面載入時的初始化
        }

        private void ContactsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView &&
                listView.SelectedItem is ContactItem contactItem)
            {
                ViewModel?.OpenChatCommand?.Execute(contactItem);
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