using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Preferences;
using AnnaMessager.Core.Services;

namespace AnnaMessager.Droid.Services
{
    /// <summary>
    ///     Android 平台特定的設定服務實現 - 修復版本
    /// </summary>
    public class AndroidSettingsService : IPlatformSettingsService
    {
        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
#if __ANDROID__
                var context = Application.Context;
                _preferences = PreferenceManager.GetDefaultSharedPreferences(context);
#endif
            });
        }

        public async Task SetValueAsync(string key, string value)
        {
            await Task.Run(() =>
            {
#if __ANDROID__
                _editor = _preferences.Edit();
                _editor.PutString(key, value);
                _editor.Apply();
#endif
            });
        }

        public async Task<string> GetValueAsync(string key)
        {
            return await Task.Run(() =>
            {
#if __ANDROID__
                return _preferences.GetString(key, string.Empty);
#else
                return string.Empty;
#endif
            });
        }

        public async Task RemoveValueAsync(string key)
        {
            await Task.Run(() =>
            {
#if __ANDROID__
                _editor = _preferences.Edit();
                _editor.Remove(key);
                _editor.Apply();
#endif
            });
        }

        public async Task<bool> ContainsKeyAsync(string key)
        {
            return await Task.Run(() =>
            {
#if __ANDROID__
                return _preferences.Contains(key);
#else
                return false;
#endif
            });
        }

        public async Task ClearAsync()
        {
            await Task.Run(() =>
            {
#if __ANDROID__
                _editor = _preferences.Edit();
                _editor.Clear();
                _editor.Apply();
#endif
            });
        }
#if __ANDROID__
        private ISharedPreferences _preferences;
        private ISharedPreferencesEditor _editor;
#endif
    }
}