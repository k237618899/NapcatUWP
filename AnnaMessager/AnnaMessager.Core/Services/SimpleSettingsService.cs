using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    // 由 public 改為 internal，避免被 MvvmCross 自動掃描註冊
    internal class SimpleSettingsService : ISettingsService
    {
        public Task<AppSettings> LoadSettingsAsync()
        {
            return Task.FromResult(new AppSettings
            {
                EnableNotifications = true,
                EnableSounds = true,
                MaxImageCacheSize = 100,
                MaxAvatarCacheSize = 50,
                MessageCacheDays = 30
            });
        }

        public Task SaveSettingsAsync(AppSettings settings)
        {
            return Task.FromResult(0);
        }

        public Task<ServerSettings> LoadServerSettingsAsync()
        {
            return Task.FromResult(new ServerSettings
            {
                ServerUrl = "ws://localhost:3001",
                AccessToken = "",
                ConnectionTimeout = 30,
                EnableSsl = false,
                AutoReconnect = true
            });
        }

        public Task SaveServerSettingsAsync(ServerSettings settings)
        {
            return Task.FromResult(0);
        }

        public Task<LoginCredentials> LoadLoginCredentialsAsync()
        {
            return Task.FromResult(new LoginCredentials
            {
                ServerUrl = "ws://localhost:3001",
                AccessToken = "",
                RememberCredentials = false
            });
        }

        public Task SaveLoginCredentialsAsync(LoginCredentials credentials)
        {
            return Task.FromResult(0);
        }

        public Task ClearAllSettingsAsync()
        {
            return Task.FromResult(0);
        }

        public Task<bool> HasSettingsAsync()
        {
            return Task.FromResult(true);
        }
    }
}

// 已移除 SimpleSettingsService 測試實作，使用平台實際 UwpSettingsService。
// 此檔案保留以防其他專案仍有引用，若無引用可刪除