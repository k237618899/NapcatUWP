using System.Threading.Tasks;

namespace AnnaMessager.Core.Services
{
    public interface IAvatarCacheService
    {
        /// <summary>
        /// 預先抓取並快取頭像，返回本地路徑 (ms-appdata 路徑)。
        /// 若失敗返回 null。
        /// </summary>
        Task<string> PrefetchAsync(string remoteUrl, string category, long id);
    }
}