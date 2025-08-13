using System.Threading.Tasks;
using AnnaMessager.Core.Services;
using Foundation;

namespace AnnaMessager.iOS.Services
{
    /// <summary>
    ///     iOS 平台特定的設定服務實現 - 修復版本
    /// </summary>
    public class iOSSettingsService : IPlatformSettingsService
    {
        public async Task InitializeAsync()
        {
            // iOS NSUserDefaults 總是可用，無需初始化
            await Task.FromResult(0);
        }

        public async Task SetValueAsync(string key, string value)
        {
            await Task.Run(() =>
            {
#if __IOS__
                NSUserDefaults.StandardUserDefaults.SetString(value, key);
                NSUserDefaults.StandardUserDefaults.Synchronize();
#endif
            });
        }

        public async Task<string> GetValueAsync(string key)
        {
            return await Task.Run(() =>
            {
#if __IOS__
                return NSUserDefaults.StandardUserDefaults.StringForKey(key) ?? string.Empty;
#else
                return string.Empty;
#endif
            });
        }

        public async Task RemoveValueAsync(string key)
        {
            await Task.Run(() =>
            {
#if __IOS__
                NSUserDefaults.StandardUserDefaults.RemoveObject(key);
                NSUserDefaults.StandardUserDefaults.Synchronize();
#endif
            });
        }

        public async Task<bool> ContainsKeyAsync(string key)
        {
            return await Task.Run(() =>
            {
#if __IOS__
                return NSUserDefaults.StandardUserDefaults.StringForKey(key) != null;
#else
                return false;
#endif
            });
        }

        public async Task ClearAsync()
        {
            await Task.Run(() =>
            {
#if __IOS__
                var appDomain = NSBundle.MainBundle.BundleIdentifier;
                // 修復：使用正確的方法名稱
                NSUserDefaults.StandardUserDefaults.RemovePersistentDomain(appDomain);
                NSUserDefaults.StandardUserDefaults.Synchronize();
#endif
            });
        }
    }
}