using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;

namespace AnnaMessager.Core.ViewModels
{
    public class MainViewModel : MvxViewModel
    {
        private readonly IOneBotService _oneBotService;
        private string _currentUserAvatar;
        private string _currentUserName;
        private bool _isConnected;
        private int _selectedTabIndex;

        public MainViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;

            // 初始化子 ViewModel
            ChatListViewModel = new ChatListViewModel(_oneBotService);
            ContactsViewModel = new ContactsViewModel(_oneBotService);
            GroupsViewModel = new GroupsViewModel(_oneBotService);
            SettingsViewModel = new SettingsViewModel();

            // 註冊事件
            _oneBotService.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        public ChatListViewModel ChatListViewModel { get; }
        public ContactsViewModel ContactsViewModel { get; }
        public GroupsViewModel GroupsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public string CurrentUserName
        {
            get => _currentUserName;
            set => SetProperty(ref _currentUserName, value);
        }

        public string CurrentUserAvatar
        {
            get => _currentUserAvatar;
            set => SetProperty(ref _currentUserAvatar, value);
        }

        public override async Task Initialize()
        {
            await base.Initialize();
            await LoadUserInfoAsync();
        }

        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            IsConnected = isConnected;
        }

        private async Task LoadUserInfoAsync()
        {
            try
            {
                var loginInfo = await _oneBotService.GetLoginInfoAsync();
                if (loginInfo?.Status == "ok" && loginInfo.Data != null) CurrentUserName = loginInfo.Data.Nickname;
                // 可以添加頭像 URL 的邏輯
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入用戶信息失敗: {ex.Message}");
            }
        }
    }
}