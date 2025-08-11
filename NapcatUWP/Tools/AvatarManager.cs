using Microsoft.Data.Sqlite;
using NapcatUWP.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace NapcatUWP.Tools
{
    /// <summary>
    /// 頭像管理器 - 修復UI更新問題，確保線程安全，兼容UWP 15063（C# 7.3）
    /// </summary>
    public static class AvatarManager
    {
        private static readonly Dictionary<string, BitmapImage> _memoryCache = new Dictionary<string, BitmapImage>();
        private static readonly HashSet<string> _downloadingUrls = new HashSet<string>();
        private static HttpClient _httpClient;

        // 請求限制相關字段
        private static readonly Queue<DateTime> _requestTimes = new Queue<DateTime>();
        private static readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(1, 1);
        private static readonly int _maxRequestsPerSecond = 2; // 降低並發數避免頻繁請求

        // 數據庫操作信號量
        private static readonly SemaphoreSlim _databaseSemaphore = new SemaphoreSlim(1, 1);

        // UI更新回調
        public static event System.Action<string, BitmapImage> OnAvatarUpdated;

        private static HttpClient HttpClient
        {
            get
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                    _httpClient.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    _httpClient.Timeout = TimeSpan.FromSeconds(30); // 設置超時
                }

                return _httpClient;
            }
        }

        /// <summary>
        /// 獲取頭像 - 修復UI更新版本
        /// </summary>
        public static async Task<BitmapImage> GetAvatarAsync(string avatarType, long id, int priority = 2,
            bool useCache = false)
        {
            var cacheKey = $"{avatarType}_{id}";

            // 優先從內存緩存獲取
            if (_memoryCache.ContainsKey(cacheKey))
            {
                Debug.WriteLine($"從內存緩存獲取頭像: {cacheKey}");
                return _memoryCache[cacheKey];
            }

            // 從本地文件緩存獲取
            var localPath = await GetCachedAvatarPathAsync(avatarType, id);
            if (!string.IsNullOrEmpty(localPath))
            {
                try
                {
                    var bitmapImage = await LoadImageFromFileAsync(localPath);
                    if (bitmapImage != null)
                    {
                        // 添加到內存緩存
                        _memoryCache[cacheKey] = bitmapImage;
                        Debug.WriteLine($"從本地緩存獲取頭像: {cacheKey}");

                        // 觸發UI更新事件
                        await TriggerUIUpdate(cacheKey, bitmapImage);
                        return bitmapImage;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"從本地緩存載入頭像失敗: {ex.Message}");
                    // 刪除損壞的緩存文件
                    await DeleteCachedAvatarAsync(avatarType, id);
                }
            }

            // 如果僅使用緩存，返回null
            if (useCache)
            {
                Debug.WriteLine($"僅緩存模式，未找到緩存頭像: {cacheKey}");
                return null;
            }

            // 直接下載
            Debug.WriteLine($"開始直接下載頭像: {cacheKey}");
            return await DownloadAvatarDirectAsync(avatarType, id);
        }

        /// <summary>
        /// 從文件載入圖片 - UWP 15063兼容版本，確保在UI線程中創建BitmapImage
        /// </summary>
        private static async Task<BitmapImage> LoadImageFromFileAsync(string localPath)
        {
            try
            {
                BitmapImage bitmapImage = null;

                // 確保在UI線程中創建BitmapImage
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
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"在UI線程中創建BitmapImage失敗: {ex.Message}");
                            bitmapImage = null;
                        }
                    });

                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"從文件載入圖片失敗: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 觸發UI更新 - 確保在UI線程中執行
        /// </summary>
        private static async Task TriggerUIUpdate(string cacheKey, BitmapImage image)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            OnAvatarUpdated?.Invoke(cacheKey, image);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"UI更新回調執行失敗: {ex.Message}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"觸發UI更新失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 直接下載頭像 - 優化版本
        /// </summary>
        private static async Task<BitmapImage> DownloadAvatarDirectAsync(string avatarType, long id)
        {
            var cacheKey = $"{avatarType}_{id}";

            // 防止重複下載
            if (_downloadingUrls.Contains(cacheKey))
            {
                Debug.WriteLine($"頭像正在下載中，跳過: {cacheKey}");
                // 等待一小段時間，可能其他下載會完成
                await Task.Delay(500);
                if (_memoryCache.ContainsKey(cacheKey))
                {
                    return _memoryCache[cacheKey];
                }

                return await GetDefaultAvatarAsync();
            }

            _downloadingUrls.Add(cacheKey);

            try
            {
                // 限制請求頻率
                await _downloadSemaphore.WaitAsync();
                try
                {
                    var now = DateTime.Now;

                    // 清理超過1秒的請求記錄
                    while (_requestTimes.Count > 0 && (now - _requestTimes.Peek()).TotalSeconds >= 1)
                    {
                        _requestTimes.Dequeue();
                    }

                    // 檢查是否超過限制
                    if (_requestTimes.Count >= _maxRequestsPerSecond)
                    {
                        var waitTime = 1000 - (int)(now - _requestTimes.Peek()).TotalMilliseconds + 100;
                        if (waitTime > 0)
                        {
                            Debug.WriteLine($"請求限制，等待 {waitTime}ms");
                            await Task.Delay(waitTime);
                        }
                    }

                    _requestTimes.Enqueue(now);
                }
                finally
                {
                    _downloadSemaphore.Release();
                }

                var result = await DownloadAvatarInternalAsync(avatarType, id);
                return result;
            }
            finally
            {
                _downloadingUrls.Remove(cacheKey);
            }
        }

        /// <summary>
        /// 內部下載頭像方法 - 修復UI更新版本
        /// </summary>
        private static async Task<BitmapImage> DownloadAvatarInternalAsync(string avatarType, long id)
        {
            string avatarUrl;
            switch (avatarType)
            {
                case "friend":
                    avatarUrl = GetFriendAvatarUrl(id);
                    break;
                case "group":
                    avatarUrl = GetGroupAvatarUrl(id);
                    break;
                case "current":
                    avatarUrl = GetCurrentUserAvatarUrl(id);
                    break;
                default:
                    throw new ArgumentException($"不支援的頭像類型: {avatarType}");
            }

            var cacheKey = $"{avatarType}_{id}";

            try
            {
                Debug.WriteLine($"開始下載頭像: {avatarUrl}");

                using (var response = await HttpClient.GetAsync(avatarUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var imageData = await response.Content.ReadAsByteArrayAsync();

                        if (imageData == null || imageData.Length == 0)
                        {
                            Debug.WriteLine($"頭像下載返回空數據: {avatarUrl}");
                            return await GetDefaultAvatarAsync();
                        }

                        // 保存到本地緩存
                        var localPath = await SaveAvatarToCacheAsync(avatarType, id, avatarUrl, imageData);

                        // 在UI線程中創建BitmapImage
                        BitmapImage bitmapImage = null;

                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, async () =>
                            {
                                try
                                {
                                    bitmapImage = new BitmapImage();

                                    using (var stream = new InMemoryRandomAccessStream())
                                    {
                                        await stream.WriteAsync(imageData.AsBuffer());
                                        stream.Seek(0);
                                        await bitmapImage.SetSourceAsync(stream);
                                    }
                                }
                                catch (Exception bitmapEx)
                                {
                                    Debug.WriteLine($"在UI線程中創建BitmapImage失敗: {bitmapEx.Message}");
                                    bitmapImage = null;
                                }
                            });

                        if (bitmapImage != null)
                        {
                            // 驗證圖片是否有效
                            if (bitmapImage.PixelWidth > 0 && bitmapImage.PixelHeight > 0)
                            {
                                // 添加到內存緩存
                                _memoryCache[cacheKey] = bitmapImage;
                                Debug.WriteLine(
                                    $"頭像下載成功: {cacheKey}, 大小: {imageData.Length} 字節, 尺寸: {bitmapImage.PixelWidth}x{bitmapImage.PixelHeight}");

                                // 異步觸發UI更新
                                _ = TriggerUIUpdate(cacheKey, bitmapImage);

                                return bitmapImage;
                            }
                            else
                            {
                                Debug.WriteLine(
                                    $"頭像圖片無效: {cacheKey}, 尺寸: {bitmapImage.PixelWidth}x{bitmapImage.PixelHeight}");
                                return await GetDefaultAvatarAsync();
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"創建BitmapImage失敗: {cacheKey}");
                            return await GetDefaultAvatarAsync();
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"頭像下載失敗: {avatarUrl}, 狀態碼: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"下載頭像時發生錯誤: {ex.Message}");
            }

            return await GetDefaultAvatarAsync();
        }

        /// <summary>
        /// 獲取默認頭像 - 修復線程安全版本
        /// </summary>
        private static async Task<BitmapImage> GetDefaultAvatarAsync()
        {
            try
            {
                BitmapImage defaultAvatar = null;

                // 確保在UI線程中創建BitmapImage
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            defaultAvatar = new BitmapImage();
                            Debug.WriteLine("創建默認頭像成功");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"創建默認頭像失敗: {ex.Message}");
                        }
                    });

                return defaultAvatar;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取默認頭像失敗: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 獲取隊列狀態信息
        /// </summary>
        public static (int QueueLength, bool IsProcessing) GetQueueStatus()
        {
            return (_downloadingUrls.Count, false);
        }

        /// <summary>
        /// 初始化頭像管理器
        /// </summary>
        public static async Task InitializeAsync()
        {
            try
            {
                // 創建頭像緩存文件夾
                await ApplicationData.Current.LocalFolder.CreateFolderAsync("AvatarCache",
                    CreationCollisionOption.OpenIfExists);

                // 創建頭像緩存數據庫表
                await CreateAvatarCacheTable();

                Debug.WriteLine("頭像管理器初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"頭像管理器初始化失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 創建頭像緩存數據庫表
        /// </summary>
        private static async Task CreateAvatarCacheTable()
        {
            await _databaseSemaphore.WaitAsync();
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                    "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    await db.OpenAsync();

                    var createTableCommand = new SqliteCommand(@"
                    CREATE TABLE IF NOT EXISTS AvatarCache (
                        AvatarKey TEXT PRIMARY KEY,
                        AvatarType TEXT NOT NULL,
                        UserId INTEGER,
                        GroupId INTEGER,
                        AvatarUrl TEXT NOT NULL,
                        LocalPath TEXT NOT NULL,
                        LastUpdated TEXT NOT NULL,
                        FileSize INTEGER DEFAULT 0
                    )", db);

                    await createTableCommand.ExecuteNonQueryAsync();

                    // 創建索引
                    var createIndexCommand = new SqliteCommand(@"
                    CREATE INDEX IF NOT EXISTS idx_avatar_cache_type_id 
                    ON AvatarCache(AvatarType, UserId, GroupId)", db);

                    await createIndexCommand.ExecuteNonQueryAsync();

                    Debug.WriteLine("頭像緩存數據庫表創建完成");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"創建頭像緩存表失敗: {ex.Message}");
            }
            finally
            {
                _databaseSemaphore.Release();
            }
        }

        /// <summary>
        /// 生成好友頭像URL
        /// </summary>
        public static string GetFriendAvatarUrl(long userId)
        {
            return $"https://q1.qlogo.cn/g?b=qq&nk={userId}&s=640";
        }

        /// <summary>
        /// 生成群組頭像URL
        /// </summary>
        public static string GetGroupAvatarUrl(long groupId)
        {
            return $"https://p.qlogo.cn/gh/{groupId}/{groupId}/640/";
        }

        /// <summary>
        /// 生成當前用戶頭像URL
        /// </summary>
        public static string GetCurrentUserAvatarUrl(long userId)
        {
            return $"https://q1.qlogo.cn/g?b=qq&nk={userId}&s=640";
        }

        /// <summary>
        /// 保存頭像到本地緩存
        /// </summary>
        /// <param name="avatarType">頭像類型</param>
        /// <param name="id">ID</param>
        /// <param name="avatarUrl">頭像URL</param>
        /// <param name="imageData">圖片數據</param>
        /// <returns>本地文件路徑</returns>
        private static async Task<string> SaveAvatarToCacheAsync(string avatarType, long id, string avatarUrl,
            byte[] imageData)
        {
            try
            {
                var cacheFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync("AvatarCache");
                var fileName = $"{avatarType}_{id}.jpg";
                var file = await cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteBytesAsync(file, imageData);

                var localPath = file.Path;

                // 保存到數據庫
                await SaveAvatarCacheInfoAsync(avatarType, id, avatarUrl, localPath, imageData.Length);

                return localPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存頭像緩存失敗: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存頭像緩存信息到數據庫（添加信號量保護）
        /// </summary>
        private static async Task SaveAvatarCacheInfoAsync(string avatarType, long id, string avatarUrl,
            string localPath, int fileSize)
        {
            await _databaseSemaphore.WaitAsync();
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    await db.OpenAsync();

                    var insertCommand = new SqliteCommand(@"
                INSERT OR REPLACE INTO AvatarCache 
                (AvatarKey, AvatarType, UserId, GroupId, AvatarUrl, LocalPath, LastUpdated, FileSize) 
                VALUES (@avatarKey, @avatarType, @userId, @groupId, @avatarUrl, @localPath, @lastUpdated, @fileSize)",
                        db);

                    var avatarKey = $"{avatarType}_{id}";
                    var userId = avatarType != "group" ? id : 0;
                    var groupId = avatarType == "group" ? id : 0;

                    insertCommand.Parameters.AddWithValue("@avatarKey", avatarKey);
                    insertCommand.Parameters.AddWithValue("@avatarType", avatarType);
                    insertCommand.Parameters.AddWithValue("@userId", userId);
                    insertCommand.Parameters.AddWithValue("@groupId", groupId);
                    insertCommand.Parameters.AddWithValue("@avatarUrl", avatarUrl);
                    insertCommand.Parameters.AddWithValue("@localPath", localPath);
                    insertCommand.Parameters.AddWithValue("@lastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    insertCommand.Parameters.AddWithValue("@fileSize", fileSize);

                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存頭像緩存信息失敗: {ex.Message}");
            }
            finally
            {
                _databaseSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取缓存的头像路径（添加信号量保护）
        /// </summary>
        private static async Task<string> GetCachedAvatarPathAsync(string avatarType, long id)
        {
            await _databaseSemaphore.WaitAsync();
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    await db.OpenAsync();

                    var selectCommand = new SqliteCommand(@"
                SELECT LocalPath, LastUpdated FROM AvatarCache 
                WHERE AvatarKey = @avatarKey", db);

                    var avatarKey = $"{avatarType}_{id}";
                    selectCommand.Parameters.AddWithValue("@avatarKey", avatarKey);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var localPath = reader.GetString(0);
                            var lastUpdatedStr = reader.GetString(1);

                            // 检查文件是否存在
                            if (File.Exists(localPath))
                            {
                                // 检查缓存是否过期（7天）
                                if (DateTime.TryParse(lastUpdatedStr, out var lastUpdated))
                                {
                                    if (DateTime.Now - lastUpdated < TimeSpan.FromDays(7))
                                    {
                                        return localPath;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取缓存头像路径失败: {ex.Message}");
            }
            finally
            {
                _databaseSemaphore.Release();
            }

            return null;
        }

        /// <summary>
        /// 删除缓存的头像
        /// </summary>
        /// <param name="avatarType">头像类型</param>
        /// <param name="id">ID</param>
        private static async Task DeleteCachedAvatarAsync(string avatarType, long id)
        {
            try
            {
                var avatarKey = $"{avatarType}_{id}";

                // 从内存缓存中删除
                if (_memoryCache.ContainsKey(avatarKey))
                {
                    _memoryCache.Remove(avatarKey);
                }

                // 从数据库中获取文件路径并删除文件
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    await db.OpenAsync();

                    var selectCommand = new SqliteCommand(@"
                    SELECT LocalPath FROM AvatarCache 
                    WHERE AvatarKey = @avatarKey", db);
                    selectCommand.Parameters.AddWithValue("@avatarKey", avatarKey);

                    var localPath = await selectCommand.ExecuteScalarAsync() as string;
                    if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }

                    // 从数据库中删除记录
                    var deleteCommand = new SqliteCommand(@"
                    DELETE FROM AvatarCache WHERE AvatarKey = @avatarKey", db);
                    deleteCommand.Parameters.AddWithValue("@avatarKey", avatarKey);
                    await deleteCommand.ExecuteNonQueryAsync();
                }

                Debug.WriteLine($"删除缓存头像: {avatarKey}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"删除缓存头像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理过期的头像缓存
        /// </summary>
        /// <param name="daysToKeep">保留天数</param>
        public static async Task CleanExpiredCacheAsync(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");

                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    await db.OpenAsync();

                    // 获取过期的头像记录
                    var selectCommand = new SqliteCommand(@"
                    SELECT AvatarKey, LocalPath FROM AvatarCache 
                    WHERE datetime(LastUpdated) < @cutoffDate", db);
                    selectCommand.Parameters.AddWithValue("@cutoffDate", cutoffDate.ToString("yyyy-MM-dd HH:mm:ss"));

                    var expiredRecords = new List<(string AvatarKey, string LocalPath)>();
                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            expiredRecords.Add((reader.GetString(0), reader.GetString(1)));
                        }
                    }

                    // 删除过期的文件和数据库记录
                    foreach (var record in expiredRecords)
                    {
                        try
                        {
                            if (File.Exists(record.LocalPath))
                            {
                                File.Delete(record.LocalPath);
                            }

                            // 从内存缓存中删除
                            if (_memoryCache.ContainsKey(record.AvatarKey))
                            {
                                _memoryCache.Remove(record.AvatarKey);
                            }
                        }
                        catch (Exception fileEx)
                        {
                            Debug.WriteLine($"删除过期头像文件失败: {fileEx.Message}");
                        }
                    }

                    // 从数据库中删除过期记录
                    var deleteCommand = new SqliteCommand(@"
                    DELETE FROM AvatarCache WHERE datetime(LastUpdated) < @cutoffDate", db);
                    deleteCommand.Parameters.AddWithValue("@cutoffDate", cutoffDate.ToString("yyyy-MM-dd HH:mm:ss"));
                    var deletedCount = await deleteCommand.ExecuteNonQueryAsync();

                    Debug.WriteLine($"清理过期头像缓存完成: 删除 {deletedCount} 个记录");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理过期头像缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取头像缓存统计信息
        /// </summary>
        /// <returns>缓存统计信息</returns>
        public static async Task<AvatarCacheStats> GetCacheStatsAsync()
        {
            var stats = new AvatarCacheStats();

            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    await db.OpenAsync();

                    // 获取总数和总大小
                    var countCommand = new SqliteCommand(@"
                    SELECT COUNT(*) as TotalCount, 
                           COALESCE(SUM(FileSize), 0) as TotalSize,
                           COUNT(CASE WHEN AvatarType = 'friend' THEN 1 END) as FriendCount,
                           COUNT(CASE WHEN AvatarType = 'group' THEN 1 END) as GroupCount,
                           COUNT(CASE WHEN AvatarType = 'current' THEN 1 END) as CurrentCount
                    FROM AvatarCache", db);

                    using (var reader = await countCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            stats.TotalCount = reader.GetInt32(0);
                            stats.TotalSizeBytes = reader.GetInt64(1);
                            stats.FriendAvatarCount = reader.GetInt32(2);
                            stats.GroupAvatarCount = reader.GetInt32(3);
                            stats.CurrentUserAvatarCount = reader.GetInt32(4);
                        }
                    }
                }

                stats.MemoryCacheCount = _memoryCache.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取头像缓存统计信息失败: {ex.Message}");
            }

            return stats;
        }

        /// <summary>
        /// 预加载好友头像
        /// </summary>
        /// <param name="userIds">用户ID列表</param>
        // 预加载方法 - 安全版本
        public static async Task PreloadFriendAvatarsAsync(System.Collections.Generic.IEnumerable<long> userIds)
        {
            // 限制并发数避免崩溃
            const int batchSize = 5;
            var batch = new List<Task>();

            foreach (var userId in userIds)
            {
                batch.Add(GetAvatarAsync("friend", userId, 2, false));

                if (batch.Count >= batchSize)
                {
                    await Task.WhenAll(batch);
                    batch.Clear();
                    await Task.Delay(500); // 批次间延迟
                }
            }

            if (batch.Count > 0)
            {
                await Task.WhenAll(batch);
            }
        }

        /// <summary>
        /// 预加载群组头像
        /// </summary>
        /// <param name="groupIds">群组ID列表</param>
        public static async Task PreloadGroupAvatarsAsync(System.Collections.Generic.IEnumerable<long> groupIds)
        {
            // 限制并发数避免崩溃  
            const int batchSize = 5;
            var batch = new List<Task>();

            foreach (var groupId in groupIds)
            {
                batch.Add(GetAvatarAsync("group", groupId, 2, false));

                if (batch.Count >= batchSize)
                {
                    await Task.WhenAll(batch);
                    batch.Clear();
                    await Task.Delay(500); // 批次间延迟
                }
            }

            if (batch.Count > 0)
            {
                await Task.WhenAll(batch);
            }
        }
    }

    /// <summary>
    /// 頭像緩存統計信息
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