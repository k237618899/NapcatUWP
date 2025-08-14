using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;

namespace AnnaMessager.Core.ViewModels
{
    public class ContactsViewModel : MvxViewModel
    {
        private readonly IOneBotService _oneBotService;
        private ObservableCollection<ContactItem> _contacts;
        private bool _isRefreshing;
        private string _searchText;

        public ContactsViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;
            Contacts = new ObservableCollection<ContactItem>();

            OpenChatCommand = new MvxCommand<ContactItem>(OpenChat);
            RefreshCommand = new MvxCommand(async () => await RefreshAsync());
            SearchCommand = new MvxCommand<string>(OnSearch);
        }

        public ObservableCollection<ContactItem> Contacts
        {
            get => _contacts;
            set
            {
                SetProperty(ref _contacts, value);
                RaisePropertyChanged(() => IsEmpty);
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        // UI 顯示屬性
        public bool IsEmpty => !IsRefreshing && (Contacts == null || Contacts.Count == 0);

        public ICommand OpenChatCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }

        public override async Task Initialize()
        {
            await base.Initialize();
            await LoadContactsAsync();
        }

        private async Task LoadContactsAsync()
        {
            try
            {
                IsRefreshing = true;

                var friends = await _oneBotService.GetFriendListAsync();
                if (friends?.Status == "ok" && friends.Data != null)
                {
                    Contacts.Clear();
                    foreach (var friend in friends.Data)
                        Contacts.Add(new ContactItem
                        {
                            UserId = friend.UserId,
                            Nickname = friend.Nickname,
                            Remark = friend.Remark,
                            IsOnline = true, // 默認在線狀態
                            Status = UserStatus.Online
                        });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聯絡人失敗: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
                RaisePropertyChanged(() => IsEmpty);
            }
        }

        private void OpenChat(ContactItem contact)
        {
            if (contact == null) return;

            ShowViewModel<ChatViewModel>(new
            {
                chatId = contact.UserId,
                isGroup = false,
                chatName = contact.DisplayName
            });

            // 通過 Mvx 解析主 ViewModel 
            try
            {
                var mainViewModel = Mvx.Resolve<MainViewModel>();
                mainViewModel?.ChatListViewModel.AddOrUpdateChat(contact.UserId, false, contact.DisplayName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"添加到聊天列表失敗: {ex.Message}");
            }
        }

        private async Task RefreshAsync()
        {
            await LoadContactsAsync();
        }

        private void OnSearch(string searchText)
        {
            SearchText = searchText;
            // TODO: 實作搜尋功能
            // 可以根據 searchText 過濾 Contacts 集合
        }
    }
}