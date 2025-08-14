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
        private readonly IOneBotService _oneBotService;
        private readonly ISettingsService _settingsService;
        private string _accessToken;
        private string _account;
        private bool _hasError;
        private bool _isLogging;
        private string _loginStatus;
        private bool _rememberCredentials;

        public LoginViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;
            _settingsService = Mvx.Resolve<ISettingsService>();

            LoginCommand = new MvxCommand(async () => await LoginAsync(), () => CanLogin);
            OpenServerSettingsCommand = new MvxCommand(() => ShowViewModel<ServerSettingsViewModel>());

            LoadSavedCredentials();
        }

        public string Account
        {
            get => _account;
            set
            {
                SetProperty(ref _account, value);
                RaisePropertyChanged(() => CanLogin);
                HasError = false;
            }
        }

        public string AccessToken
        {
            get => _accessToken;
            set
            {
                SetProperty(ref _accessToken, value);
                RaisePropertyChanged(() => CanLogin);
                HasError = false;
            }
        }

        public bool RememberCredentials
        {
            get => _rememberCredentials;
            set => SetProperty(ref _rememberCredentials, value);
        }

        public bool IsLogging
        {
            get => _isLogging;
            set
            {
                SetProperty(ref _isLogging, value);
                RaisePropertyChanged(() => CanLogin);
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

        public bool CanLogin => !string.IsNullOrEmpty(Account) &&
                                !string.IsNullOrEmpty(AccessToken) &&
                                !IsLogging;

        public ICommand LoginCommand { get; }
        public ICommand OpenServerSettingsCommand { get; }

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

                            if (RememberCredentials)
                                await SaveCredentialsAsync();

                            // 短暫延遲後導航到主界面
                            await Task.Delay(1500);
                            ShowViewModel<MainViewModel>();
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

        private async void LoadSavedCredentials()
        {
            try
            {
                if (_settingsService != null)
                {
                    var settings = await _settingsService.LoadServerSettingsAsync();
                    Account = settings?.Account ?? "";
                    AccessToken = settings?.AccessToken ?? "";
                    RememberCredentials = !string.IsNullOrEmpty(settings?.AccessToken);
                }
                else
                {
                    Account = "";
                    AccessToken = "";
                    RememberCredentials = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入保存的憑證失敗: {ex.Message}");
                Account = "";
                AccessToken = "";
                RememberCredentials = true;
            }
        }

        private async Task SaveCredentialsAsync()
        {
            try
            {
                if (_settingsService != null)
                {
                    var existingSettings = await _settingsService.LoadServerSettingsAsync() ?? new ServerSettings();

                    existingSettings.Account = Account;
                    existingSettings.AccessToken = AccessToken;

                    await _settingsService.SaveServerSettingsAsync(existingSettings);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存憑證失敗: {ex.Message}");
            }
        }

        private static bool IsValidAccount(string account)
        {
            if (string.IsNullOrEmpty(account))
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