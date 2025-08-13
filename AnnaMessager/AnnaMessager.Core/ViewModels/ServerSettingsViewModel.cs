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
        private bool _isTestingConnection;
        private string _serverUrl;

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
                ServerUrl = settings.ServerUrl;
                AccessToken = settings.AccessToken;
                ConnectionTimeout = settings.ConnectionTimeout;
                EnableSsl = settings.EnableSsl;
                AutoReconnect = settings.AutoReconnect;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入伺服器設定失敗: {ex.Message}");
                // 使用預設值
                ServerUrl = "ws://localhost:3001";
                ConnectionTimeout = 30;
                EnableSsl = false;
                AutoReconnect = true;
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                var settings = new ServerSettings
                {
                    ServerUrl = ServerUrl,
                    AccessToken = AccessToken,
                    ConnectionTimeout = ConnectionTimeout,
                    EnableSsl = EnableSsl,
                    AutoReconnect = AutoReconnect
                };

                await _settingsService.SaveServerSettingsAsync(settings);
                Debug.WriteLine("伺服器設定已保存");

                // 顯示保存成功通知
                await _notificationService.ShowToastAsync("設定已保存", ToastType.Success);

                Close(this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存伺服器設定失敗: {ex.Message}");

                // 顯示保存失敗通知
                await _notificationService.ShowToastAsync($"保存失敗: {ex.Message}", ToastType.Error);
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                IsTestingConnection = true;

                // 測試連接
                var isConnected = await _oneBotService.ConnectAsync(ServerUrl, AccessToken);

                if (isConnected)
                {
                    Debug.WriteLine("連接測試成功");

                    // 顯示成功通知
                    await _notificationService.ShowToastAsync("連接測試成功", ToastType.Success);
                }
                else
                {
                    Debug.WriteLine("連接測試失敗");

                    // 顯示失敗通知
                    await _notificationService.ShowToastAsync("連接測試失敗", ToastType.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"測試連接失敗: {ex.Message}");

                // 顯示錯誤通知
                await _notificationService.ShowToastAsync($"連接測試錯誤: {ex.Message}", ToastType.Error);
            }
            finally
            {
                IsTestingConnection = false;
            }
        }
    }
}