using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     設定服務接口 - 用於管理應用程式設定的本地存儲
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        ///     載入應用程式設定
        /// </summary>
        Task<AppSettings> LoadSettingsAsync();

        /// <summary>
        ///     保存應用程式設定
        /// </summary>
        Task SaveSettingsAsync(AppSettings settings);

        /// <summary>
        ///     載入伺服器設定
        /// </summary>
        Task<ServerSettings> LoadServerSettingsAsync();

        /// <summary>
        ///     保存伺服器設定
        /// </summary>
        Task SaveServerSettingsAsync(ServerSettings settings);

        /// <summary>
        ///     載入登入憑證
        /// </summary>
        Task<LoginCredentials> LoadLoginCredentialsAsync();

        /// <summary>
        ///     保存登入憑證
        /// </summary>
        Task SaveLoginCredentialsAsync(LoginCredentials credentials);

        /// <summary>
        ///     清除所有設定
        /// </summary>
        Task ClearAllSettingsAsync();

        /// <summary>
        ///     檢查設定是否存在
        /// </summary>
        Task<bool> HasSettingsAsync();
    }
}