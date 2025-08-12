using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace NapcatUWP.Tools
{
    /// <summary>
    ///     頭像管理器 - 使用並發下載器的簡化版本，兼容UWP 15063（C# 7.3）
    /// </summary>
    public static class AvatarManager
    {
        private static readonly ConcurrentImageDownloader _downloader = ConcurrentImageDownloader.Instance;

        static AvatarManager()
        {
            _downloader.OnImageLoaded += (cacheKey, image) => { OnAvatarUpdated?.Invoke(cacheKey, image); };
        }

        // UI更新回調
        public static event Action<string, BitmapImage> OnAvatarUpdated;

        /// <summary>
        ///     智能獲取頭像 - 優化7天緩存策略
        /// </summary>
        public static async Task<BitmapImage> GetAvatarAsync(string avatarType, long id, int priority = 2,
            bool useCache = false)
        {
            try
            {
                var cacheKey = $"{avatarType}_{id}";
                var imageUrl = GetAvatarUrl(avatarType, id);
                var imageType = GetImageType(avatarType);

                Debug.WriteLine($"請求頭像: {cacheKey}, URL: {imageUrl}, 僅緩存: {useCache}");

                // 優先使用本地緩存（7天有效期）
                var cachedImage = await _downloader.RequestImageAsync(cacheKey, imageUrl, imageType, id);

                // 如果是僅緩存模式且沒有緩存，直接返回null
                if (useCache && cachedImage == null)
                {
                    Debug.WriteLine($"僅緩存模式，未找到緩存頭像: {cacheKey}");
                    return null;
                }

                return cachedImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取頭像失敗: {avatarType}_{id}, {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     獲取頭像URL
        /// </summary>
        private static string GetAvatarUrl(string avatarType, long id)
        {
            switch (avatarType)
            {
                case "friend":
                case "current":
                    return $"https://q1.qlogo.cn/g?b=qq&nk={id}&s=640";
                case "group":
                    return $"https://p.qlogo.cn/gh/{id}/{id}/640/";
                default:
                    throw new ArgumentException($"不支援的頭像類型: {avatarType}");
            }
        }

        /// <summary>
        ///     獲取圖片類型枚舉
        /// </summary>
        private static ImageType GetImageType(string avatarType)
        {
            switch (avatarType)
            {
                case "friend":
                    return ImageType.Friend;
                case "group":
                    return ImageType.Group;
                case "current":
                    return ImageType.CurrentUser;
                default:
                    return ImageType.Friend;
            }
        }

        /// <summary>
        ///     預載入好友頭像
        /// </summary>
        public static async Task PreloadFriendAvatarsAsync(IEnumerable<long> userIds)
        {
            var tasks = new List<Task>();
            const int batchSize = 10;

            foreach (var userId in userIds)
            {
                tasks.Add(GetAvatarAsync("friend", userId, 3));

                if (tasks.Count >= batchSize)
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                    await Task.Delay(100); // 批次間小延遲
                }
            }

            if (tasks.Count > 0) await Task.WhenAll(tasks);
        }

        /// <summary>
        ///     預載入群組頭像
        /// </summary>
        public static async Task PreloadGroupAvatarsAsync(IEnumerable<long> groupIds)
        {
            var tasks = new List<Task>();
            const int batchSize = 10;

            foreach (var groupId in groupIds)
            {
                tasks.Add(GetAvatarAsync("group", groupId, 3));

                if (tasks.Count >= batchSize)
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                    await Task.Delay(100); // 批次間小延遲
                }
            }

            if (tasks.Count > 0) await Task.WhenAll(tasks);
        }

        /// <summary>
        ///     獲取佇列狀態
        /// </summary>
        public static (int QueueLength, bool IsProcessing) GetQueueStatus()
        {
            var stats = _downloader.GetStats();
            return (stats.QueueLength, stats.IsProcessing);
        }

        /// <summary>
        ///     初始化頭像管理器
        /// </summary>
        public static async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("頭像管理器使用並發下載器初始化完成");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"頭像管理器初始化失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     清理過期的頭像緩存 - 委託給並發下載器
        /// </summary>
        /// <param name="daysToKeep">保留天數，默認7天</param>
        public static async Task CleanExpiredCacheAsync(int daysToKeep = 7)
        {
            try
            {
                Debug.WriteLine($"開始清理過期頭像緩存，保留 {daysToKeep} 天");

                // 通過並發下載器清理緩存
                await _downloader.CleanExpiredCacheAsync(daysToKeep);

                Debug.WriteLine("頭像緩存清理完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理過期頭像緩存失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取頭像緩存統計資訊 - 修復大小統計問題
        /// </summary>
        /// <returns>緩存統計資訊</returns>
        public static async Task<AvatarCacheStats> GetCacheStatsAsync()
        {
            try
            {
                // 從並發下載器獲取統計資訊
                var imageStats = await _downloader.GetCacheStatsAsync();

                // 轉換為頭像統計資訊
                var avatarStats = new AvatarCacheStats
                {
                    TotalCount = imageStats.TotalCount,
                    TotalSizeBytes = imageStats.TotalSizeBytes, // 現在會有正確的大小
                    FriendAvatarCount = imageStats.FriendImageCount,
                    GroupAvatarCount = imageStats.GroupImageCount,
                    CurrentUserAvatarCount = imageStats.CurrentUserImageCount,
                    MemoryCacheCount = imageStats.MemoryCacheCount
                };

                Debug.WriteLine(
                    $"頭像緩存統計: 總數 {avatarStats.TotalCount}, 記憶體緩存 {avatarStats.MemoryCacheCount}, 總大小 {avatarStats.TotalSizeFormatted}");
                return avatarStats;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取頭像緩存統計資訊失敗: {ex.Message}");
                return new AvatarCacheStats(); // 返回空的統計資訊
            }
        }

        /// <summary>
        ///     從檔案載入圖片 - 新增方法
        /// </summary>
        public static async Task<BitmapImage> LoadImageFromFileAsync(string localPath)
        {
            try
            {
                if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
                {
                    Debug.WriteLine($"圖片檔案不存在: {localPath}");
                    return null;
                }

                BitmapImage bitmapImage = null;

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            bitmapImage = new BitmapImage();
                            var file = await StorageFile.GetFileFromPathAsync(localPath);
                            using (var stream = await file.OpenReadAsync())
                            {
                                await bitmapImage.SetSourceAsync(stream);
                            }

                            Debug.WriteLine($"成功從檔案載入圖片: {localPath}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"從檔案載入圖片失敗: {localPath}, {ex.Message}");
                            bitmapImage = null;
                        }
                    });

                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadImageFromFileAsync 發生錯誤: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     生成好友頭像URL
        /// </summary>
        public static string GetFriendAvatarUrl(long userId)
        {
            return $"https://q1.qlogo.cn/g?b=qq&nk={userId}&s=640";
        }

        /// <summary>
        ///     生成群組頭像URL
        /// </summary>
        public static string GetGroupAvatarUrl(long groupId)
        {
            return $"https://p.qlogo.cn/gh/{groupId}/{groupId}/640/";
        }

        /// <summary>
        ///     生成當前用戶頭像URL
        /// </summary>
        public static string GetCurrentUserAvatarUrl(long userId)
        {
            return $"https://q1.qlogo.cn/g?b=qq&nk={userId}&s=640";
        }
    }

    /// <summary>
    ///     頭像緩存統計資訊 - 修復版本
    /// </summary>
    public class AvatarCacheStats
    {
        public int TotalCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public int FriendAvatarCount { get; set; }
        public int GroupAvatarCount { get; set; }
        public int CurrentUserAvatarCount { get; set; }
        public int MemoryCacheCount { get; set; }

        public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            return $"{bytes / (1024 * 1024):F1} MB";
        }
    }
}