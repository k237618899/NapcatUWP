using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;

namespace AnnaMessager.Core.ViewModels
{
    public class LoginViewModel : MvxViewModel
    {
        private readonly IOneBotService _oneBotService;
        private string _accessToken;
        private bool _isLogging;
        private string _loginStatus;
        private bool _rememberCredentials;
        private string _serverUrl;

        public LoginViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;

            LoginCommand = new MvxCommand(async () => await LoginAsync(), () => CanLogin);
            OpenServerSettingsCommand = new MvxCommand(() => ShowViewModel<ServerSettingsViewModel>());

            LoadSavedCredentials();
        }

        public string ServerUrl
        {
            get => _serverUrl;
            set
            {
                SetProperty(ref _serverUrl, value);
                RaisePropertyChanged(() => CanLogin);
            }
        }

        public string AccessToken
        {
            get => _accessToken;
            set
            {
                SetProperty(ref _accessToken, value);
                RaisePropertyChanged(() => CanLogin);
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
            set => SetProperty(ref _isLogging, value);
        }

        public string LoginStatus
        {
            get => _loginStatus;
            set => SetProperty(ref _loginStatus, value);
        }

        public bool CanLogin => !string.IsNullOrEmpty(ServerUrl) && !string.IsNullOrEmpty(AccessToken) && !IsLogging;

        public ICommand LoginCommand { get; }
        public ICommand OpenServerSettingsCommand { get; }

        private async Task LoginAsync()
        {
            try
            {
                IsLogging = true;
                LoginStatus = "正在連接服務器...";

                var success = await _oneBotService.ConnectAsync(ServerUrl, AccessToken);
                if (success)
                {
                    LoginStatus = "登入成功！";

                    if (RememberCredentials) SaveCredentials();

                    // 導航到主界面
                    ShowViewModel<MainViewModel>();
                }
                else
                {
                    LoginStatus = "登入失敗，請檢查服務器地址和令牌";
                }
            }
            catch (Exception ex)
            {
                LoginStatus = $"連接失敗: {ex.Message}";
                Debug.WriteLine($"登入失敗: {ex.Message}");
            }
            finally
            {
                IsLogging = false;
            }
        }

        private void LoadSavedCredentials()
        {
            // TODO: 從本地存儲載入保存的憑證
            ServerUrl = "ws://localhost:3001";
            AccessToken = "";
            RememberCredentials = true;
        }

        private void SaveCredentials()
        {
            // TODO: 保存憑證到本地存儲
        }
    }
}