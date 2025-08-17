using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Platform;
using Newtonsoft.Json;

namespace AnnaMessager.UWP.Services
{
    /// <summary>
    ///     UWP 平台基於 SQLite 的設定服務實現
    /// </summary>
    public class UwpSettingsService : ISettingsService
    {
        private readonly IPlatformDatabaseService _databaseService;
        private bool _isInitialized;

        public UwpSettingsService()
        {
            _databaseService = Mvx.Resolve<IPlatformDatabaseService>();
            Debug.WriteLine($"UwpSettingsService: 構造函數，數據庫服務類型: {_databaseService?.GetType().Name ?? "null"}");
            _ = InitializeAsync();
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                Debug.WriteLine("UwpSettingsService: LoadSettingsAsync 開始");
                await EnsureInitializedAsync();

                var settingEntity = await _databaseService.GetSettingAsync("AppSettings");
                if (settingEntity != null && !string.IsNullOrEmpty(settingEntity.Value))
                {
                    var settings = JsonConvert.DeserializeObject<AppSettings>(settingEntity.Value);
                    Debug.WriteLine($"載入應用程式設定成功: {settingEntity.Value}");
                    return settings ?? GetDefaultAppSettings();
                }

                Debug.WriteLine("應用程式設定不存在，使用默認設定");
                return GetDefaultAppSettings();
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
                Debug.WriteLine("UwpSettingsService: SaveSettingsAsync 開始");
                await EnsureInitializedAsync();

                var json = JsonConvert.SerializeObject(settings);
                var settingEntity = new AppSettingsEntity
                {
                    Key = "AppSettings",
                    Value = json,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                await _databaseService.SaveSettingAsync(settingEntity);
                Debug.WriteLine($"應用程式設定已保存: {json}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存應用程式設定失敗: {ex.Message}");
                throw;
            }
        }

        public async Task<ServerSettings> LoadServerSettingsAsync()
        {
            try
            {
                Debug.WriteLine("UwpSettingsService: LoadServerSettingsAsync 開始");
                await EnsureInitializedAsync();
                Debug.WriteLine("UwpSettingsService: 確保初始化完成");

                // 首先嘗試從設定表載入
                Debug.WriteLine("UwpSettingsService: 調用 _databaseService.GetSettingAsync");
                var settingEntity = await _databaseService.GetSettingAsync("ServerSettings");
                Debug.WriteLine($"UwpSettingsService: 從數據庫獲取 ServerSettings: {settingEntity?.Value ?? "null"}");

                if (settingEntity != null && !string.IsNullOrEmpty(settingEntity.Value))
                    try
                    {
                        Debug.WriteLine("UwpSettingsService: 開始反序列化");
                        var settings = JsonConvert.DeserializeObject<ServerSettings>(settingEntity.Value);
                        if (settings != null)
                        {
                            Debug.WriteLine(
                                $"從設定表載入伺服器設定成功: ServerUrl={settings.ServerUrl}, AccessToken={settings.AccessToken?.Length ?? 0} 字符, ConnectionTimeout={settings.ConnectionTimeout}, EnableSsl={settings.EnableSsl}, AutoReconnect={settings.AutoReconnect}");
                            return settings;
                        }

                        Debug.WriteLine("UwpSettingsService: 反序列化結果為 null");
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"反序列化設定失敗: {ex.Message}");
                    }

                Debug.WriteLine("設定表中沒有伺服器設定，嘗試從伺服器表載入...");

                // 如果設定表中沒有，嘗試從伺服器表載入默認伺服器
                var defaultServer = await _databaseService.GetDefaultServerAsync();
                if (defaultServer != null)
                {
                    var settings = new ServerSettings
                    {
                        ServerUrl = defaultServer.ServerUrl,
                        ConnectionTimeout = defaultServer.ConnectionTimeout,
                        EnableSsl = defaultServer.EnableSsl,
                        AutoReconnect = defaultServer.AutoReconnect,
                        Account = "",
                        AccessToken = ""
                    };
                    Debug.WriteLine($"從伺服器表載入設定: ServerUrl={settings.ServerUrl}");
                    return settings;
                }

                Debug.WriteLine("沒有找到任何伺服器設定，使用默認設定");
                return GetDefaultServerSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入伺服器設定失敗: {ex.Message}");
                Debug.WriteLine($"錯誤詳情: {ex.StackTrace}");
                return GetDefaultServerSettings();
            }
        }

        public async Task SaveServerSettingsAsync(ServerSettings settings)
        {
            try
            {
                Debug.WriteLine("UwpSettingsService: SaveServerSettingsAsync 開始");
                await EnsureInitializedAsync();
                Debug.WriteLine("UwpSettingsService: 確保初始化完成");

                Debug.WriteLine(
                    $"開始保存伺服器設定: ServerUrl={settings.ServerUrl}, AccessToken={settings.AccessToken?.Length ?? 0} 字符, ConnectionTimeout={settings.ConnectionTimeout}, EnableSsl={settings.EnableSsl}, AutoReconnect={settings.AutoReconnect}");

                // 保存到設定表
                var json = JsonConvert.SerializeObject(settings);
                var settingEntity = new AppSettingsEntity
                {
                    Key = "ServerSettings",
                    Value = json,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                Debug.WriteLine($"準備保存的 JSON: {json}");
                Debug.WriteLine("UwpSettingsService: 調用 _databaseService.SaveSettingAsync");

                var savedId = await _databaseService.SaveSettingAsync(settingEntity);
                Debug.WriteLine($"UwpSettingsService: 設定表保存結果，ID: {savedId}");

                // 立即驗證保存結果
                Debug.WriteLine("=== UwpSettingsService: 立即驗證數據庫保存結果 ===");
                Debug.WriteLine("UwpSettingsService: 調用 _databaseService.GetSettingAsync 進行驗證");
                var verifyEntity = await _databaseService.GetSettingAsync("ServerSettings");
                if (verifyEntity != null)
                {
                    Debug.WriteLine($"UwpSettingsService: 驗證保存成功: Key={verifyEntity.Key}, Value={verifyEntity.Value}");

                    // 嘗試反序列化驗證
                    try
                    {
                        var verifySettings = JsonConvert.DeserializeObject<ServerSettings>(verifyEntity.Value);
                        if (verifySettings != null)
                            Debug.WriteLine($"UwpSettingsService: 反序列化成功: ServerUrl={verifySettings.ServerUrl}");
                        else
                            Debug.WriteLine("UwpSettingsService: 反序列化結果為 null");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UwpSettingsService: 反序列化失敗: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine("UwpSettingsService: ✗ 驗證保存失敗：未找到保存的設定");

                    // 檢查所有設定
                    Debug.WriteLine("UwpSettingsService: 調用 _databaseService.GetAllSettingsAsync");
                    var allSettings = await _databaseService.GetAllSettingsAsync();
                    Debug.WriteLine($"UwpSettingsService: 數據庫中總共有 {allSettings.Count} 個設定項");
                    foreach (var s in allSettings)
                        Debug.WriteLine($"UwpSettingsService:   設定: Key={s.Key}, Value={s.Value?.Length ?? 0} 字符");
                }

                Debug.WriteLine("=== UwpSettingsService: 驗證完成 ===");

                Debug.WriteLine("UwpSettingsService: 伺服器設定保存流程完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UwpSettingsService: 保存伺服器設定失敗: {ex.Message}");
                Debug.WriteLine($"UwpSettingsService: 錯誤詳情: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<LoginCredentials> LoadLoginCredentialsAsync()
        {
            try
            {
                await EnsureInitializedAsync();

                var settingEntity = await _databaseService.GetSettingAsync("LoginCredentials");
                if (settingEntity != null && !string.IsNullOrEmpty(settingEntity.Value))
                    return JsonConvert.DeserializeObject<LoginCredentials>(settingEntity.Value) ??
                           new LoginCredentials();

                // 如果沒有保存的登錄憑證，嘗試從默認賬號載入
                var defaultAccount = await _databaseService.GetDefaultAccountAsync();
                if (defaultAccount != null)
                {
                    var serverSettings = await LoadServerSettingsAsync();
                    return new LoginCredentials
                    {
                        ServerUrl = serverSettings?.ServerUrl ?? "",
                        AccessToken = defaultAccount.AccessToken ?? "",
                        RememberCredentials = !string.IsNullOrEmpty(defaultAccount.AccessToken)
                    };
                }

                return new LoginCredentials();
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
                await EnsureInitializedAsync();

                // 保存憑證到設定表
                var json = JsonConvert.SerializeObject(credentials);
                var settingEntity = new AppSettingsEntity
                {
                    Key = "LoginCredentials",
                    Value = json,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                await _databaseService.SaveSettingAsync(settingEntity);
                Debug.WriteLine("登入憑證已保存");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存登入憑證失敗: {ex.Message}");
                throw;
            }
        }

        public async Task ClearAllSettingsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                await _databaseService.ClearAllSettingsAsync();
                Debug.WriteLine("所有設定已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除所有設定失敗: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> HasSettingsAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                var settings = await _databaseService.GetAllSettingsAsync();
                return settings != null && settings.Count > 0;
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
                Debug.WriteLine("UwpSettingsService: InitializeAsync 開始");
                await _databaseService.InitializeDatabaseAsync();
                _isInitialized = true;
                Debug.WriteLine("UwpSettingsService 初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UwpSettingsService 初始化失敗: {ex.Message}");
            }
        }

        private async Task EnsureInitializedAsync()
        {
            Debug.WriteLine($"UwpSettingsService: EnsureInitializedAsync 檢查，當前狀態: {_isInitialized}");
            var maxRetries = 50; // 最多等待5秒
            var retryCount = 0;

            while (!_isInitialized && retryCount < maxRetries)
            {
                await Task.Delay(100);
                retryCount++;
                if (retryCount % 10 == 0) // 每1秒輸出一次
                    Debug.WriteLine($"UwpSettingsService: 等待初始化完成，重試次數: {retryCount}");
            }

            if (!_isInitialized)
            {
                Debug.WriteLine("UwpSettingsService: 初始化超時失敗");
                throw new InvalidOperationException("數據庫服務未完成初始化");
            }

            Debug.WriteLine("UwpSettingsService: 確保初始化檢查通過");
        }

        private static AppSettings GetDefaultAppSettings()
        {
            return new AppSettings
            {
                EnableNotifications = true,
                EnableSounds = true,
                MaxImageCacheSize = 100,
                MaxAvatarCacheSize = 50,
                MessageCacheDays = 30
            };
        }

        private static ServerSettings GetDefaultServerSettings()
        {
            return new ServerSettings
            {
                ServerUrl = "ws://localhost:3001",
                ConnectionTimeout = 30,
                EnableSsl = false,
                AutoReconnect = true,
                Account = "",
                AccessToken = ""
            };
        }
    }
}