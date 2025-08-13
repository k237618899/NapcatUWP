using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;
using MvvmCross.Platform;
using Newtonsoft.Json;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     跨平台設定服務實現 - PCL 兼容，使用平台特定存儲
    /// </summary>
    public class CrossPlatformSettingsService : ISettingsService
    {
        private readonly IPlatformSettingsService _platformService;

        public CrossPlatformSettingsService()
        {
            _platformService = Mvx.Resolve<IPlatformSettingsService>();
            InitializeAsync().Wait(); // PCL 模式下需要同步等待
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                var json = await _platformService.GetValueAsync("AppSettings");
                if (string.IsNullOrEmpty(json)) return GetDefaultAppSettings();

                return JsonConvert.DeserializeObject<AppSettings>(json) ?? GetDefaultAppSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入應用程式設定失敗: {ex.Message}");
                return GetDefaultAppSettings();
            }
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings);
                await _platformService.SetValueAsync("AppSettings", json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存應用程式設定失敗: {ex.Message}");
            }
        }

        public async Task<ServerSettings> LoadServerSettingsAsync()
        {
            try
            {
                var json = await _platformService.GetValueAsync("ServerSettings");
                if (string.IsNullOrEmpty(json)) return GetDefaultServerSettings();

                return JsonConvert.DeserializeObject<ServerSettings>(json) ?? GetDefaultServerSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入伺服器設定失敗: {ex.Message}");
                return GetDefaultServerSettings();
            }
        }

        public async Task SaveServerSettingsAsync(ServerSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings);
                await _platformService.SetValueAsync("ServerSettings", json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存伺服器設定失敗: {ex.Message}");
            }
        }

        public async Task<LoginCredentials> LoadLoginCredentialsAsync()
        {
            try
            {
                var json = await _platformService.GetValueAsync("LoginCredentials");
                if (string.IsNullOrEmpty(json)) return new LoginCredentials();

                return JsonConvert.DeserializeObject<LoginCredentials>(json) ?? new LoginCredentials();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入登入憑證失敗: {ex.Message}");
                return new LoginCredentials();
            }
        }

        public async Task SaveLoginCredentialsAsync(LoginCredentials credentials)
        {
            try
            {
                var json = JsonConvert.SerializeObject(credentials);
                await _platformService.SetValueAsync("LoginCredentials", json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存登入憑證失敗: {ex.Message}");
            }
        }

        public async Task ClearAllSettingsAsync()
        {
            try
            {
                await _platformService.ClearAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除設定失敗: {ex.Message}");
            }
        }

        public async Task<bool> HasSettingsAsync()
        {
            try
            {
                return await _platformService.ContainsKeyAsync("AppSettings");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"檢查設定是否存在失敗: {ex.Message}");
                return false;
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _platformService.InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化設定服務失敗: {ex.Message}");
            }
        }

        private AppSettings GetDefaultAppSettings()
        {
            return new AppSettings
            {
                EnableNotifications = true,
                EnableSounds = true,
                MaxImageCacheSize = 100,
                MaxAvatarCacheSize = 50,
                MessageCacheDays = 30,
                AutoLogin = false,
                EnableVibration = true
            };
        }

        private ServerSettings GetDefaultServerSettings()
        {
            return new ServerSettings
            {
                ServerUrl = "ws://localhost:3001",
                ConnectionTimeout = 30,
                EnableSsl = false,
                AutoReconnect = true
            };
        }
    }
}