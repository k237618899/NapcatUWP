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
    public class SettingsViewModel : MvxViewModel
    {
        private readonly ICacheManager _cacheManager;
        private readonly ISettingsService _settingsService;
        private CacheInfo _cacheInfo;
        private bool _isCalculatingCache;
        private AppSettings _settings;

        public SettingsViewModel()
        {
            _settingsService = Mvx.Resolve<ISettingsService>();
            _cacheManager = Mvx.Resolve<ICacheManager>();

            Settings = new AppSettings();
            CacheInfo = new CacheInfo(); // 這現在明確引用 Services.CacheInfo

            ClearCacheCommand = new MvxCommand(async () => await ClearCacheAsync());
            CalculateCacheCommand = new MvxCommand(async () => await CalculateCacheAsync());
            OpenServerSettingsCommand = new MvxCommand(() => ShowViewModel<ServerSettingsViewModel>());
            LogoutCommand = new MvxCommand(() => ShowViewModel<LoginViewModel>());
            SaveSettingsCommand = new MvxCommand(async () => await SaveSettingsAsync());
            ClearExpiredCacheCommand = new MvxCommand(async () => await ClearExpiredCacheAsync());
        }

        public AppSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public CacheInfo CacheInfo
        {
            get => _cacheInfo;
            set => SetProperty(ref _cacheInfo, value);
        }

        public bool IsCalculatingCache
        {
            get => _isCalculatingCache;
            set => SetProperty(ref _isCalculatingCache, value);
        }

        public ICommand ClearCacheCommand { get; }
        public ICommand CalculateCacheCommand { get; }
        public ICommand OpenServerSettingsCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand ClearExpiredCacheCommand { get; }

        public override async Task Initialize()
        {
            await base.Initialize();
            await LoadSettingsAsync();
            await CalculateCacheAsync();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                Settings = await _settingsService.LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入設定失敗: {ex.Message}");
                Settings = new AppSettings
                {
                    EnableNotifications = true,
                    EnableSounds = true,
                    MaxImageCacheSize = 100,
                    MaxAvatarCacheSize = 50,
                    MessageCacheDays = 30
                };
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                await _settingsService.SaveSettingsAsync(Settings);
                Debug.WriteLine("設定已保存");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存設定失敗: {ex.Message}");
            }
        }

        private async Task CalculateCacheAsync()
        {
            try
            {
                IsCalculatingCache = true;

                // 使用緩存管理器計算真實的緩存大小
                CacheInfo = await _cacheManager.CalculateCacheSizeAsync();

                Debug.WriteLine($"緩存統計: 總大小 {CacheInfo.TotalSizeDisplay}, 消息 {CacheInfo.TotalMessages}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"計算快取大小失敗: {ex.Message}");
            }
            finally
            {
                IsCalculatingCache = false;
            }
        }

        private async Task ClearCacheAsync()
        {
            try
            {
                await _cacheManager.ClearAllCacheAsync();
                await CalculateCacheAsync();
                Debug.WriteLine("所有快取已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除快取失敗: {ex.Message}");
            }
        }

        private async Task ClearExpiredCacheAsync()
        {
            try
            {
                await _cacheManager.ClearExpiredCacheAsync();
                await CalculateCacheAsync();
                Debug.WriteLine("過期快取已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除過期快取失敗: {ex.Message}");
            }
        }
    }
}