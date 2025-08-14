using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    public class SimpleSettingsService : ISettingsService
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
            // 簡單實現：不實際保存
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
            // 簡單實現：不實際保存
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
            // 簡單實現：不實際保存
            return Task.FromResult(0);
        }

        public Task ClearAllSettingsAsync()
        {
            // 簡單實現：不實際操作
            return Task.FromResult(0);
        }

        public Task<bool> HasSettingsAsync()
        {
            // 簡單實現：假設設定存在
            return Task.FromResult(true);
        }
    }
}