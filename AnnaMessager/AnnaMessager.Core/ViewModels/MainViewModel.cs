using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;

namespace AnnaMessager.Core.ViewModels
{
    public class MainViewModel : MvxViewModel, IDisposable
    {
        private readonly INotificationService _notificationService;
        private readonly IOneBotService _oneBotService;
        private string _connectionErrorMessage;
        private string _connectionStatus;
        private string _currentUserAvatar;
        private string _currentUserName;
        private bool _disposed;
        private bool _hasConnectionError;
        private bool _isConnected;
        private bool _isLoading;
        private int _selectedTabIndex;
        private string _userStatus;
        private long _currentUserId;
        private bool _initializedFromParams;
        private string _serverUrl;
        private string _connectionIndicatorColor = "LightGray"; // Red/Yellow/Green

        public MainViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;
            _notificationService = Mvx.Resolve<INotificationService>();

            // 初始化子 ViewModel
            ChatListViewModel = new ChatListViewModel(_oneBotService);
            ContactsViewModel = new ContactsViewModel(_oneBotService);
            GroupsViewModel = new GroupsViewModel(_oneBotService);
            SettingsViewModel = new SettingsViewModel();

            // 初始化命令
            LogoutCommand = new MvxCommand(async () => await LogoutAsync());
            ReconnectCommand = new MvxCommand(async () => await ReconnectAsync());

            // 註冊事件
            _oneBotService.ConnectionStatusChanged += OnConnectionStatusChanged;

            // 初始狀態
            IsLoading = true;
            ConnectionStatus = "正在連接...";
            UserStatus = "在線";
            CurrentUserAvatar = null; // 先為空，XAML 會顯示首字母

            Debug.WriteLine("[MainViewModel] 建構子執行");
        }

        public ChatListViewModel ChatListViewModel { get; }
        public ContactsViewModel ContactsViewModel { get; }
        public GroupsViewModel GroupsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public ICommand LogoutCommand { get; }
        public ICommand ReconnectCommand { get; }

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

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasConnectionError
        {
            get => _hasConnectionError;
            set => SetProperty(ref _hasConnectionError, value);
        }

        public string ConnectionErrorMessage
        {
            get => _connectionErrorMessage;
            set => SetProperty(ref _connectionErrorMessage, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public string CurrentUserName
        {
            get => _currentUserName;
            set => SetProperty(ref _currentUserName, value);
        }

        public string UserName => CurrentUserName ?? "用戶名稱";

        public string CurrentUserAvatar
        {
            get => _currentUserAvatar;
            set => SetProperty(ref _currentUserAvatar, value);
        }

        public string UserStatus
        {
            get => _userStatus;
            set => SetProperty(ref _userStatus, value);
        }

        public long CurrentUserId
        {
            get => _currentUserId;
            set => SetProperty(ref _currentUserId, value);
        }

        public string ServerUrl
        {
            get => _serverUrl;
            set => SetProperty(ref _serverUrl, value);
        }

        public string ConnectionIndicatorColor
        {
            get => _connectionIndicatorColor;
            set => SetProperty(ref _connectionIndicatorColor, value);
        }

        public override async Task Initialize()
        {
            await base.Initialize();
            if (string.IsNullOrEmpty(ServerUrl) && _oneBotService?.CurrentServerUrl != null)
                ServerUrl = _oneBotService.CurrentServerUrl;

            Debug.WriteLine("MainViewModel Initialize 開始");
            Debug.WriteLine("[MainViewModel] Initialize 觸發, ChatList 項目數=" + (ChatListViewModel?.ChatList?.Count ?? 0));
            // 初始化子 ViewModel（確保其 Initialize 被呼叫）
            if (ChatListViewModel != null)
            {
                Debug.WriteLine("初始化 ChatListViewModel");
                await ChatListViewModel.Initialize();
            }
            if (ContactsViewModel != null)
            {
                Debug.WriteLine("初始化 ContactsViewModel");
                await ContactsViewModel.Initialize();
            }
            if (GroupsViewModel != null)
            {
                Debug.WriteLine("初始化 GroupsViewModel");
                await GroupsViewModel.Initialize();
            }
            if (SettingsViewModel != null)
            {
                Debug.WriteLine("初始化 SettingsViewModel");
                await SettingsViewModel.Initialize();
            }
            await LoadUserInfoAsync();
            Debug.WriteLine("MainViewModel Initialize 完成");
        }

        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            if (string.IsNullOrEmpty(ServerUrl) && _oneBotService?.CurrentServerUrl != null)
                ServerUrl = _oneBotService.CurrentServerUrl;

            IsConnected = isConnected;
            HasConnectionError = !isConnected;

            if (isConnected)
            {
                ConnectionStatus = string.IsNullOrEmpty(ServerUrl) ? "連線正常" : $"連線正常 {ServerUrl}";
                UserStatus = "在線";
                ConnectionErrorMessage = null;
                IsLoading = false;
                ConnectionIndicatorColor = "LimeGreen";
            }
            else
            {
                ConnectionStatus = "連線中斷";
                UserStatus = "離線";
                ConnectionErrorMessage = "與服務器的連線已中斷";
                ConnectionIndicatorColor = "OrangeRed";
            }

            RaisePropertyChanged(() => UserName);

            if (isConnected)
            {
                // 連線成功後如果尚未載入使用者資訊則自動載入
                if (string.IsNullOrEmpty(CurrentUserName))
                {
                    // 嘗試再載入一次使用者資訊
                    Task.Run(async () => await LoadUserInfoAsync());
                }
            }
        }

        public void Init(long userId, string nickname)
        {
            try
            {
                Debug.WriteLine($"[MainViewModel] Init 參數: userId={userId}, nickname={nickname}");
                if (userId > 0)
                {
                    CurrentUserId = userId;
                    CurrentUserName = string.IsNullOrEmpty(nickname) ? userId.ToString() : nickname;
                    CurrentUserAvatar = $"https://q1.qlogo.cn/g?b=qq&nk={userId}&s=640";
                    _initializedFromParams = true;
                    IsLoading = false;
                    if (string.IsNullOrEmpty(ServerUrl) && _oneBotService?.CurrentServerUrl != null)
                        ServerUrl = _oneBotService.CurrentServerUrl;
                    ConnectionStatus = string.IsNullOrEmpty(ServerUrl) ? "連線正常" : $"連線正常 {ServerUrl}";
                    ConnectionIndicatorColor = "LimeGreen";
                    UserStatus = "在線";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Init 參數初始化失敗: {ex.Message}");
            }

            Debug.WriteLine("[MainViewModel] Init 參數初始化完成");
        }

        private async Task LoadUserInfoAsync()
        {
            try
            {
                Debug.WriteLine("[MainViewModel] LoadUserInfoAsync 開始");
                IsLoading = true;

                if (_initializedFromParams && CurrentUserId > 0)
                {
                    // 已經由導航參數初始化，跳過遠程請求
                    IsLoading = false;
                    return;
                }

                var loginInfo = await _oneBotService.GetLoginInfoAsync();
                if (loginInfo?.Status == "ok" && loginInfo.Data != null)
                {
                    CurrentUserName = loginInfo.Data.Nickname;
                    CurrentUserId = loginInfo.Data.UserId;
                    CurrentUserAvatar = $"https://q1.qlogo.cn/g?b=qq&nk={loginInfo.Data.UserId}&s=640";
                }
                else
                {
                    CurrentUserName = "未知用戶";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入用戶信息失敗: {ex.Message}");
                CurrentUserName = "載入失敗";
                HasConnectionError = true;
                ConnectionErrorMessage = "無法載入用戶信息，請檢查網絡連接";
            }
            finally
            {
                IsLoading = false;
                Debug.WriteLine("[MainViewModel] LoadUserInfoAsync 結束");
            }
        }

        private async Task LogoutAsync()
        {
            try
            {
                IsLoading = true;

                // 斷開連接
                await _oneBotService.DisconnectAsync();

                // 清理通知
                try
                {
                    await _notificationService.ClearAllNotificationsAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"清理通知失敗: {ex.Message}");
                }

                // 導航回登入頁面
                ShowViewModel<LoginViewModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"登出失敗: {ex.Message}");

                // 即使出錯也嘗試導航回登入頁面
                ShowViewModel<LoginViewModel>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ReconnectAsync()
        {
            try
            {
                IsLoading = true;
                HasConnectionError = false;
                ConnectionStatus = "正在重新連接...";
                ConnectionIndicatorColor = "Gold";

                // 這裡需要重新獲取連接參數，可能需要從設置服務中讀取
                // 暫時先嘗試重新連接現有的連接

                // 如果重連失敗，引導用戶回到登入頁面
                await Task.Delay(2000); // 模擬重連過程

                // 實際的重連邏輯應該調用 OneBotService 的重連方法
                // 這裡暫時導航回登入頁面
                ShowViewModel<LoginViewModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重新連接失敗: {ex.Message}");
                ConnectionErrorMessage = $"重新連接失敗: {ex.Message}";
                HasConnectionError = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    try
                    {
                        // 取消註冊事件
                        if (_oneBotService != null) _oneBotService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"清理 MainViewModel 資源時發生錯誤: {ex.Message}");
                    }

                _disposed = true;
            }
        }

        #endregion
    }
}