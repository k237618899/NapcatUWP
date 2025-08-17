using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;

namespace AnnaMessager.Core.ViewModels
{
    public class LoginViewModel : MvxViewModel
    {
        private readonly IPlatformDatabaseService _databaseService;
        private readonly IOneBotService _oneBotService;
        private readonly ISettingsService _settingsService;
        private string _accessToken;
        private string _account;
        private bool _autoLogin;
        private bool _hasError;
        private bool _isLogging;
        private string _loginStatus;
        private bool _rememberCredentials;

        public LoginViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;
            _settingsService = Mvx.Resolve<ISettingsService>();
            _databaseService = Mvx.Resolve<IPlatformDatabaseService>();

            LoginCommand = new MvxCommand(async () => await LoginAsync(), () => CanLogin);
            OpenServerSettingsCommand = new MvxCommand(() => ShowViewModel<ServerSettingsViewModel>());

            // 初始化時設置默認值
            Account = "";
            AccessToken = "";
            RememberCredentials = true;
            AutoLogin = false;

            InitializeAsync();
        }

        public string Account
        {
            get => _account;
            set
            {
                SetProperty(ref _account, value);
                // 通知 CanLogin 和 LoginCommand 屬性變更
                RaisePropertyChanged(() => CanLogin);
                ((MvxCommand)LoginCommand).RaiseCanExecuteChanged();
                HasError = false;
            }
        }

        public string AccessToken
        {
            get => _accessToken;
            set
            {
                SetProperty(ref _accessToken, value);
                // 通知 CanLogin 和 LoginCommand 屬性變更
                RaisePropertyChanged(() => CanLogin);
                ((MvxCommand)LoginCommand).RaiseCanExecuteChanged();
                HasError = false;
            }
        }

        public bool RememberCredentials
        {
            get => _rememberCredentials;
            set => SetProperty(ref _rememberCredentials, value);
        }

        public bool AutoLogin
        {
            get => _autoLogin;
            set => SetProperty(ref _autoLogin, value);
        }

        public bool IsLogging
        {
            get => _isLogging;
            set
            {
                SetProperty(ref _isLogging, value);
                // 通知 CanLogin 和 LoginCommand 屬性變更
                RaisePropertyChanged(() => CanLogin);
                ((MvxCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }

        public string LoginStatus
        {
            get => _loginStatus;
            set => SetProperty(ref _loginStatus, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public bool CanLogin => !string.IsNullOrWhiteSpace(Account) &&
                                !string.IsNullOrWhiteSpace(AccessToken) &&
                                !IsLogging;

        public ICommand LoginCommand { get; }
        public ICommand OpenServerSettingsCommand { get; }

        private async void InitializeAsync()
        {
            try
            {
                // 確保數據庫已初始化
                await _databaseService.InitializeDatabaseAsync();

                // 載入保存的憑證
                await LoadSavedCredentialsAsync();

                // 檢查是否應該自動登入
                if (AutoLogin && CanLogin)
                {
                    await Task.Delay(1000); // 稍微延遲，確保UI已載入
                    await LoginAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoginViewModel 初始化失敗: {ex.Message}");
            }
        }

        private async Task LoginAsync()
        {
            try
            {
                IsLogging = true;
                HasError = false;
                LoginStatus = "正在連接服務器...";

                // 驗證賬號格式
                if (!IsValidAccount(Account))
                {
                    LoginStatus = "請輸入有效的 QQ 號";
                    HasError = true;
                    return;
                }

                // 從設定中獲取伺服器地址
                var serverSettings = await _settingsService.LoadServerSettingsAsync();
                if (serverSettings == null || string.IsNullOrEmpty(serverSettings.ServerUrl))
                {
                    LoginStatus = "請先在伺服器設定中配置服務器地址";
                    HasError = true;
                    return;
                }

                var success = await _oneBotService.ConnectAsync(serverSettings.ServerUrl, AccessToken);
                if (success)
                {
                    // 嘗試獲取登入信息以驗證連接
                    var loginInfo = await _oneBotService.GetLoginInfoAsync();
                    if (loginInfo?.Status == "ok" && loginInfo.Data != null)
                    {
                        // 驗證賬號是否匹配
                        if (loginInfo.Data.UserId.ToString() == Account)
                        {
                            LoginStatus = $"登入成功！歡迎，{loginInfo.Data.Nickname}";

                            // 保存賬號信息到數據庫
                            await SaveAccountToDatabase(loginInfo.Data);

                            if (RememberCredentials) await SaveCredentialsAsync();

                            // 短暫延遲後導航到主界面
                            await Task.Delay(1500);
                            // 傳遞參數到主視圖模型以免再次呼叫 API
                            ShowViewModel<MainViewModel>(new { userId = loginInfo.Data.UserId, nickname = loginInfo.Data.Nickname });
                        }
                        else
                        {
                            LoginStatus = $"賬號不匹配。當前連接的是：{loginInfo.Data.UserId}";
                            HasError = true;
                            await _oneBotService.DisconnectAsync();
                        }
                    }
                    else
                    {
                        LoginStatus = "連接成功但無法獲取用戶信息，請檢查令牌權限";
                        HasError = true;
                        await _oneBotService.DisconnectAsync();
                    }
                }
                else
                {
                    LoginStatus = "登入失敗，請檢查存取令牌或伺服器設定";
                    HasError = true;
                }
            }
            catch (TaskCanceledException)
            {
                LoginStatus = "連接超時，請檢查網絡連接和服務器狀態";
                HasError = true;
            }
            catch (Exception ex)
            {
                LoginStatus = $"連接失敗: {GetFriendlyErrorMessage(ex.Message)}";
                HasError = true;
                Debug.WriteLine($"登入失敗: {ex.Message}");
            }
            finally
            {
                IsLogging = false;
            }
        }

        private async Task LoadSavedCredentialsAsync()
        {
            try
            {
                // 首先嘗試從數據庫載入默認賬號
                var defaultAccount = await _databaseService.GetDefaultAccountAsync();
                if (defaultAccount != null)
                {
                    Account = defaultAccount.Account ?? "";
                    AccessToken = defaultAccount.AccessToken ?? "";
                    RememberCredentials = !string.IsNullOrEmpty(defaultAccount.AccessToken);
                    AutoLogin = RememberCredentials;

                    Debug.WriteLine($"從數據庫載入默認賬號: {defaultAccount.Account} ({defaultAccount.Nickname})");
                    return;
                }

                // 如果數據庫中沒有默認賬號，嘗試從舊的設定載入
                if (_settingsService != null)
                {
                    var settings = await _settingsService.LoadServerSettingsAsync();
                    if (!string.IsNullOrEmpty(settings?.Account) && !string.IsNullOrEmpty(settings?.AccessToken))
                    {
                        Account = settings.Account;
                        AccessToken = settings.AccessToken;
                        RememberCredentials = true;
                        AutoLogin = true;

                        // 將舊設定遷移到數據庫
                        await MigrateOldCredentialsToDatabase(settings);

                        Debug.WriteLine($"從舊設定載入並遷移賬號: {settings.Account}");
                        return;
                    }
                }

                // 如果都沒有，使用默認值
                Account = "";
                AccessToken = "";
                RememberCredentials = true;
                AutoLogin = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入保存的憑證失敗: {ex.Message}");
                Account = "";
                AccessToken = "";
                RememberCredentials = true;
                AutoLogin = false;
            }
        }

        private async Task SaveCredentialsAsync()
        {
            try
            {
                // 保存到新的設定系統
                var existingSettings = await _settingsService.LoadServerSettingsAsync() ?? new ServerSettings();
                existingSettings.Account = Account;
                existingSettings.AccessToken = AccessToken;
                await _settingsService.SaveServerSettingsAsync(existingSettings);

                // 保存登入憑證
                var credentials = new LoginCredentials
                {
                    ServerUrl = existingSettings.ServerUrl,
                    AccessToken = AccessToken,
                    RememberCredentials = RememberCredentials
                };
                await _settingsService.SaveLoginCredentialsAsync(credentials);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存憑證失敗: {ex.Message}");
            }
        }

        private async Task SaveAccountToDatabase(LoginInfoData loginData)
        {
            try
            {
                // 檢查賬號是否已存在
                var existingAccount = await _databaseService.GetAccountAsync(Account);

                var accountEntity = new AccountEntity
                {
                    Id = existingAccount?.Id ?? 0,
                    Account = Account,
                    AccessToken = RememberCredentials ? AccessToken : "",
                    Nickname = loginData.Nickname ?? "",
                    Avatar = "", // 頭像信息需要從其他API獲取
                    LastLoginTime = DateTime.Now,
                    IsDefault = true, // 設為默認賬號
                    CreatedAt = existingAccount?.CreatedAt ?? DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                await _databaseService.SaveAccountAsync(accountEntity);

                // 確保設為默認賬號
                if (accountEntity.Id > 0) await _databaseService.SetDefaultAccountAsync(accountEntity.Id);

                Debug.WriteLine($"賬號信息已保存到數據庫: {Account} ({loginData.Nickname})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存賬號到數據庫失敗: {ex.Message}");
            }
        }

        private async Task MigrateOldCredentialsToDatabase(ServerSettings settings)
        {
            try
            {
                var accountEntity = new AccountEntity
                {
                    Account = settings.Account,
                    AccessToken = settings.AccessToken,
                    Nickname = "", // 舊設定中沒有暱稱信息
                    Avatar = "",
                    LastLoginTime = DateTime.Now,
                    IsDefault = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                await _databaseService.SaveAccountAsync(accountEntity);
                Debug.WriteLine($"已將舊憑證遷移到數據庫: {settings.Account}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"遷移舊憑證失敗: {ex.Message}");
            }
        }

        private static bool IsValidAccount(string account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return false;

            // QQ號應該是5-12位數字
            return long.TryParse(account, out var qq) && qq >= 10000 && qq <= 999999999999;
        }

        private static string GetFriendlyErrorMessage(string originalMessage)
        {
            if (string.IsNullOrEmpty(originalMessage))
                return "未知錯誤";

            // 常見錯誤的友善訊息
            if (originalMessage.Contains("connection") || originalMessage.Contains("連接"))
                return "無法連接到服務器，請檢查網絡和伺服器設定";

            if (originalMessage.Contains("timeout") || originalMessage.Contains("超時"))
                return "連接超時，請稍後重試";

            if (originalMessage.Contains("refused") || originalMessage.Contains("拒絕"))
                return "服務器拒絕連接，請檢查伺服器設定";

            if (originalMessage.Contains("unauthorized") || originalMessage.Contains("401"))
                return "存取令牌無效，請檢查令牌設定";

            return originalMessage;
        }
    }
}