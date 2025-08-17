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

            Debug.WriteLine(
                $"ServerSettingsViewModel 使用的 ISettingsService 型別: {_settingsService.GetType().FullName}");

            SaveCommand = new MvxCommand(async () => await SaveAsync());
            CancelCommand = new MvxCommand(() => Close(this));
            TestConnectionCommand = new MvxCommand(async () => await TestConnectionAsync());

            // 預設值
            ServerUrl = "ws://localhost:3001";
            AccessToken = "";
            ConnectionTimeout = 30;
            EnableSsl = false;
            AutoReconnect = true;

            Debug.WriteLine("ServerSettingsViewModel 構造函數完成");
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
            Debug.WriteLine("ServerSettingsViewModel Initialize 開始");
            await base.Initialize();
            await LoadSettingsAsync();
            Debug.WriteLine("ServerSettingsViewModel Initialize 完成");
        }

        // 添加 Start 方法作為備選的初始化方法
        public override void Start()
        {
            base.Start();
            Debug.WriteLine("ServerSettingsViewModel Start 調用");
            // 如果 Initialize 沒有被調用，在 Start 中載入設定
            Task.Run(async () => await LoadSettingsAsync());
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                Debug.WriteLine("開始載入伺服器設定...");
                var settings = await _settingsService.LoadServerSettingsAsync();
                if (settings != null)
                {
                    Debug.WriteLine(
                        $"載入到的設定: ServerUrl={settings.ServerUrl}, AccessToken={settings.AccessToken?.Length ?? 0} 字符, ConnectionTimeout={settings.ConnectionTimeout}, EnableSsl={settings.EnableSsl}, AutoReconnect={settings.AutoReconnect}");

                    ServerUrl = settings.ServerUrl ?? "ws://localhost:3001";
                    AccessToken = settings.AccessToken ?? "";
                    ConnectionTimeout = settings.ConnectionTimeout > 0 ? settings.ConnectionTimeout : 30;
                    EnableSsl = settings.EnableSsl;
                    AutoReconnect = settings.AutoReconnect;

                    Debug.WriteLine(
                        $"設定屬性後: ServerUrl={ServerUrl}, AccessToken={AccessToken?.Length ?? 0} 字符, ConnectionTimeout={ConnectionTimeout}, EnableSsl={EnableSsl}, AutoReconnect={AutoReconnect}");
                }
                else
                {
                    Debug.WriteLine("沒有載入到設定，使用預設值");
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
                Debug.WriteLine($"錯誤詳情: {ex.StackTrace}");
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
                Debug.WriteLine($"開始保存設定: ServerUrl={ServerUrl}, AccessToken={AccessToken?.Length ?? 0} 字符");

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

                // 立即測試載入
                Debug.WriteLine("=== 立即測試載入保存的設定 ===");
                var loadedSettings = await _settingsService.LoadServerSettingsAsync();
                if (loadedSettings != null)
                {
                    Debug.WriteLine(
                        $"立即載入結果: ServerUrl={loadedSettings.ServerUrl}, AccessToken={loadedSettings.AccessToken?.Length ?? 0} 字符");

                    if (loadedSettings.ServerUrl == settings.ServerUrl &&
                        loadedSettings.AccessToken == settings.AccessToken)
                    {
                        Debug.WriteLine("✓ 保存和載入一致");
                    }
                    else
                    {
                        Debug.WriteLine("✗ 保存和載入不一致");
                        Debug.WriteLine($"  期望: ServerUrl={settings.ServerUrl}, AccessToken={settings.AccessToken}");
                        Debug.WriteLine(
                            $"  實際: ServerUrl={loadedSettings.ServerUrl}, AccessToken={loadedSettings.AccessToken}");
                    }
                }
                else
                {
                    Debug.WriteLine("✗ 立即載入失敗，返回 null");
                }

                Debug.WriteLine("=== 測試完成 ===");

                // 關閉視窗
                Close(this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存伺服器設定失敗: {ex.Message}");
                Debug.WriteLine($"錯誤詳情: {ex.StackTrace}");
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