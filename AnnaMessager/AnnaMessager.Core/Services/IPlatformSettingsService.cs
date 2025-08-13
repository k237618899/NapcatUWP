using System.Threading.Tasks;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     平台特定的設定服務接口 - PCL 兼容
    /// </summary>
    public interface IPlatformSettingsService
    {
        /// <summary>
        ///     初始化平台特定的存儲
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        ///     保存鍵值對
        /// </summary>
        Task SetValueAsync(string key, string value);

        /// <summary>
        ///     獲取鍵值對
        /// </summary>
        Task<string> GetValueAsync(string key);

        /// <summary>
        ///     移除鍵值對
        /// </summary>
        Task RemoveValueAsync(string key);

        /// <summary>
        ///     檢查鍵是否存在
        /// </summary>
        Task<bool> ContainsKeyAsync(string key);

        /// <summary>
        ///     清除所有設定
        /// </summary>
        Task ClearAsync();
    }
}