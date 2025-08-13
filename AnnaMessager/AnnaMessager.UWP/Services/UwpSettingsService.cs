using System.Threading.Tasks;
using Windows.Storage;
using AnnaMessager.Core.Services;

namespace AnnaMessager.UWP.Services
{
    /// <summary>
    ///     UWP 平台特定的設定服務實現
    /// </summary>
    public class UwpSettingsService : IPlatformSettingsService
    {
        private ApplicationDataContainer _localSettings;

        public async Task InitializeAsync()
        {
            await Task.Run(() => { _localSettings = ApplicationData.Current.LocalSettings; });
        }

        public async Task SetValueAsync(string key, string value)
        {
            await Task.Run(() => { _localSettings.Values[key] = value; });
        }

        public async Task<string> GetValueAsync(string key)
        {
            return await Task.Run(() =>
            {
                return _localSettings.Values.ContainsKey(key)
                    ? _localSettings.Values[key]?.ToString() ?? string.Empty
                    : string.Empty;
            });
        }

        public async Task RemoveValueAsync(string key)
        {
            await Task.Run(() =>
            {
                if (_localSettings.Values.ContainsKey(key)) _localSettings.Values.Remove(key);
            });
        }

        public async Task<bool> ContainsKeyAsync(string key)
        {
            return await Task.Run(() => { return _localSettings.Values.ContainsKey(key); });
        }

        public async Task ClearAsync()
        {
            await Task.Run(() => { _localSettings.Values.Clear(); });
        }
    }
}