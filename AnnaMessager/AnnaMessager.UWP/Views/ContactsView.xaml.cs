using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;
using AnnaMessager.Core.Services;
using MvvmCross.Platform;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class ContactsView : MvxWindowsPage
    {
        private readonly IAvatarCacheService _avatarCacheService;
        private bool _isIncrementalLoading;

        public ContactsView()
        {
            InitializeComponent();
            Loaded += ContactsView_Loaded;
            _avatarCacheService = Mvx.Resolve<IAvatarCacheService>();
        }

        public new ContactsViewModel ViewModel => (ContactsViewModel)DataContext;

        private void ContactsView_Loaded(object sender, RoutedEventArgs e)
        {
            _ = IncrementalPrefetchAsync();
        }

        private async Task IncrementalPrefetchAsync()
        {
            if (_isIncrementalLoading) return;
            _isIncrementalLoading = true;
            try
            {
                // 等待 ViewModel 初次載入完成
                int wait = 0;
                while (ViewModel != null && ViewModel.IsRefreshing && wait < 100) { await Task.Delay(100); wait++; }
                if (ViewModel?.Contacts == null) return;
                const int batch = 20;
                int index = 0;
                while (index < ViewModel.Contacts.Count)
                {
                    for (int i = index; i < index + batch && i < ViewModel.Contacts.Count; i++)
                    {
                        var c = ViewModel.Contacts[i];
                        if (string.IsNullOrEmpty(c?.AvatarUrl)) continue;
                        _ = _avatarCacheService.PrefetchAsync(c.AvatarUrl, "friend", c.UserId);
                    }
                    index += batch;
                    await Task.Delay(120);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"增量頭像預載失敗: {ex.Message}");
            }
            finally
            {
                _isIncrementalLoading = false;
            }
        }

        private void ContactsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ContactItem contactItem)
            {
                ViewModel?.OpenChatCommand?.Execute(contactItem);
            }
        }

        private void ContactsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
                ViewModel?.SearchCommand?.Execute(textBox.Text);
        }

        private void CategoryHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string catName)
            {
                ViewModel?.ToggleCategory(catName);
            }
        }

        private void ContactOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContactItem item)
            {
                ViewModel?.OpenChatCommand?.Execute(item);
            }
        }
    }
}