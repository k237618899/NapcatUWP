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
    public class ServerSettingsViewModel : MvxViewModel
    {
        private readonly INotificationService _notificationService;
        private readonly IOneBotService _oneBotService;
        private readonly ISettingsService _settingsService;
        private string _accessToken;
        private bool _autoReconnect;
        private int _connectionTimeout;
        private bool _enableSsl;
        private bool _hasTestResult;
        private bool _isSaving;
        private bool _isTestingConnection;
        private string _serverUrl;
        private string _testResult;

        public ServerSettingsViewModel()
        {
            _settingsService = Mvx.Resolve<ISettingsService>();
            _oneBotService = Mvx.Resolve<IOneBotService>();
            _notificationService = Mvx.Resolve<INotificationService>();

            SaveCommand = new MvxCommand(async () => await SaveAsync());
            CancelCommand = new MvxCommand(() => Close(this));
            TestConnectionCommand = new MvxCommand(async () => await TestConnectionAsync());
        }

        public string ServerUrl
        {
            get => _serverUrl;
            set => SetProperty(ref _serverUrl, value);
        }

        public string AccessToken
        {
            get => _accessToken;
            set => SetProperty(ref _accessToken, value);
        }

        public int ConnectionTimeout
        {
            get => _connectionTimeout;
            set => SetProperty(ref _connectionTimeout, value);
        }

        public bool EnableSsl
        {
            get => _enableSsl;
            set => SetProperty(ref _enableSsl, value);
        }

        public bool AutoReconnect
        {
            get => _autoReconnect;
            set => SetProperty(ref _autoReconnect, value);
        }

        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set => SetProperty(ref _isTestingConnection, value);
        }

        // 為 UI 添加的新屬性
        public bool IsTesting
        {
            get => _isTestingConnection;
            set => SetProperty(ref _isTestingConnection, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        public string TestResult
        {
            get => _testResult;
            set
            {
                SetProperty(ref _testResult, value);
                HasTestResult = !string.IsNullOrEmpty(value);
            }
        }

        public bool HasTestResult
        {
            get => _hasTestResult;
            set => SetProperty(ref _hasTestResult, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand TestConnectionCommand { get; }

        public override async Task Initialize()
        {
            await base.Initialize();
            await LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                var settings = await _settingsService.LoadServerSettingsAsync();
                if (settings != null)
                {
                    ServerUrl = settings.ServerUrl ?? "ws://localhost:3001";
                    AccessToken = settings.AccessToken ?? "";
                    ConnectionTimeout = settings.ConnectionTimeout > 0 ? settings.ConnectionTimeout : 30;
                    EnableSsl = settings.EnableSsl;
                    AutoReconnect = settings.AutoReconnect;
                }
                else
                {
                    // 使用預設值
                    ServerUrl = "ws://localhost:3001";
                    AccessToken = "";
                    ConnectionTimeout = 30;
                    EnableSsl = false;
                    AutoReconnect = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入伺服器設定失敗: {ex.Message}");
                // 使用預設值
                ServerUrl = "ws://localhost:3001";
                AccessToken = "";
                ConnectionTimeout = 30;
                EnableSsl = false;
                AutoReconnect = true;
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                IsSaving = true;

                var settings = new ServerSettings
                {
                    ServerUrl = ServerUrl?.Trim(),
                    AccessToken = AccessToken?.Trim(),
                    ConnectionTimeout = ConnectionTimeout,
                    EnableSsl = EnableSsl,
                    AutoReconnect = AutoReconnect
                };

                await _settingsService.SaveServerSettingsAsync(settings);
                Debug.WriteLine("伺服器設定已保存");

                // 關閉視窗
                Close(this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存伺服器設定失敗: {ex.Message}");
                TestResult = $"保存失敗: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                IsTesting = true;
                TestResult = "";

                if (string.IsNullOrWhiteSpace(ServerUrl))
                {
                    TestResult = "請輸入伺服器地址";
                    return;
                }

                if (!IsValidWebSocketUrl(ServerUrl))
                {
                    TestResult = "無效的 WebSocket 地址格式，請使用 ws:// 或 wss://";
                    return;
                }

                // 測試連接
                var isConnected = await _oneBotService.ConnectAsync(ServerUrl, AccessToken);

                if (isConnected)
                {
                    TestResult = "✓ 連接測試成功！伺服器回應正常";
                    Debug.WriteLine("連接測試成功");

                    // 測試完成後斷開連接
                    await _oneBotService.DisconnectAsync();
                }
                else
                {
                    TestResult = "✗ 連接測試失敗，請檢查伺服器地址和令牌";
                    Debug.WriteLine("連接測試失敗");
                }
            }
            catch (Exception ex)
            {
                TestResult = $"✗ 連接測試錯誤: {ex.Message}";
                Debug.WriteLine($"測試連接失敗: {ex.Message}");
            }
            finally
            {
                IsTesting = false;
            }
        }

        private static bool IsValidWebSocketUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == "ws" || uri.Scheme == "wss");
        }
    }
}