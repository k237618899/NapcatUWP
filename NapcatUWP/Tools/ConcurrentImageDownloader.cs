using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Data.Sqlite;

namespace NapcatUWP.Tools
{
    /// <summary>
    ///     並發圖片下載器 - 支援分批下載和串列資料庫寫入，兼容UWP 15063（C# 7.3）
    /// </summary>
    public class ConcurrentImageDownloader
    {
        // 設定參數
        private const int BatchSize = 5; // 批次下載大小
        private const int MaxRetryCount = 3; // 最大重試次數
        private const int RequestDelayMs = 300; // 請求間隔（毫秒）
        private const int TimeoutSeconds = 30; // 下載逾時
        private const int BatchDelayMs = 1000; // 批次間延遲
        private const int MaxQueueSize = 50; // 最大佇列大小，避免記憶體問題

        private static ConcurrentImageDownloader _instance;
        private readonly SemaphoreSlim _batchSemaphore; // 控制批次下載
        private readonly SemaphoreSlim _databaseSemaphore; // 控制資料庫寫入（串列）
        private readonly ConcurrentQueue<DownloadTask> _downloadQueue;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, DateTime> _lastRequestTime;
        private readonly ConcurrentDictionary<string, BitmapImage> _memoryCache;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<BitmapImage>> _pendingDownloads;
        private readonly object _processLock = new object();
        private readonly Timer _processTimer;
        private volatile bool _batchProcessing;
        private volatile bool _isProcessing;

        private ConcurrentImageDownloader()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            _batchSemaphore = new SemaphoreSlim(1, 1); // 只允許一個批次同時處理
            _databaseSemaphore = new SemaphoreSlim(1, 1); // 只允許一個執行緒寫資料庫
            _downloadQueue = new ConcurrentQueue<DownloadTask>();
            _pendingDownloads = new ConcurrentDictionary<string, TaskCompletionSource<BitmapImage>>();
            _memoryCache = new ConcurrentDictionary<string, BitmapImage>();
            _lastRequestTime = new ConcurrentDictionary<string, DateTime>();

            // 每500ms檢查一次佇列
            _processTimer = new Timer(ProcessQueue, null, TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(500));

            Debug.WriteLine("ConcurrentImageDownloader 初始化完成");
        }

        public static ConcurrentImageDownloader Instance => _instance ?? (_instance = new ConcurrentImageDownloader());

        public event Action<string, BitmapImage> OnImageLoaded;

        /// <summary>
        ///     請求下載圖片
        /// </summary>
        public async Task<BitmapImage> RequestImageAsync(string cacheKey, string imageUrl, ImageType imageType, long id)
        {
            try
            {
                // 首先檢查記憶體快取
                if (_memoryCache.TryGetValue(cacheKey, out var cachedImage))
                {
                    Debug.WriteLine($"從記憶體快取回傳圖片: {cacheKey}");
                    return cachedImage;
                }

                // 檢查本地檔案快取
                var localPath = await GetCachedImagePathAsync(cacheKey);
                if (!string.IsNullOrEmpty(localPath))
                    try
                    {
                        var image = await LoadImageFromFileAsync(localPath);
                        if (image != null)
                        {
                            _memoryCache.TryAdd(cacheKey, image);
                            Debug.WriteLine($"從本地快取回傳圖片: {cacheKey}");

                            // 非同步觸發UI更新
                            _ = TriggerUIUpdateAsync(cacheKey, image);
                            return image;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"從本地快取載入圖片失敗: {cacheKey}, {ex.Message}");
                        // 刪除損壞的快取檔案
                        await DeleteCachedImageAsync(cacheKey);
                    }

                // 檢查是否已在等待下載
                if (_pendingDownloads.TryGetValue(cacheKey, out var existingTask))
                {
                    Debug.WriteLine($"圖片正在下載中，回傳現有任務: {cacheKey}");
                    return await existingTask.Task;
                }

                // 檢查佇列大小，避免記憶體問題
                if (_downloadQueue.Count >= MaxQueueSize)
                {
                    Debug.WriteLine($"下載佇列已滿({MaxQueueSize})，暫停新任務: {cacheKey}");
                    return await CreateDefaultImageAsync();
                }

                // 創建新的下載任務
                var tcs = new TaskCompletionSource<BitmapImage>();
                if (_pendingDownloads.TryAdd(cacheKey, tcs))
                {
                    var downloadTask = new DownloadTask
                    {
                        CacheKey = cacheKey,
                        ImageUrl = imageUrl,
                        ImageType = imageType,
                        Id = id,
                        TaskCompletionSource = tcs,
                        Priority = GetPriority(imageType),
                        CreatedTime = DateTime.Now,
                        RetryCount = 0
                    };

                    _downloadQueue.Enqueue(downloadTask);
                    Debug.WriteLine($"圖片下載任務已加入佇列: {cacheKey}, 佇列長度: {GetQueueLength()}");
                }

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求圖片下載失敗: {cacheKey}, {ex.Message}");
                return await CreateDefaultImageAsync();
            }
        }

        /// <summary>
        ///     清理所有快取和資源 - 修復刪除記錄後快取未清空問題
        /// </summary>
        public async Task ClearAllCacheAsync()
        {
            try
            {
                Debug.WriteLine("開始清理所有圖片快取");

                // 1. 清空記憶體快取
                _memoryCache.Clear();
                Debug.WriteLine("記憶體快取已清空");

                // 2. 清空待處理任務
                _pendingDownloads.Clear();
                Debug.WriteLine("待處理任務已清空");

                // 3. 清空下載佇列
                while (_downloadQueue.TryDequeue(out _))
                {
                }

                Debug.WriteLine("下載佇列已清空");

                // 4. 刪除快取資料夾中的所有檔案
                await DeleteAllCacheFilesAsync();

                // 5. 清理資料庫記錄
                await _databaseSemaphore.WaitAsync();
                try
                {
                    var dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                    using (var db = new SqliteConnection($"Filename={dbPath}"))
                    {
                        await db.OpenAsync();

                        var deleteCommand = new SqliteCommand("DELETE FROM ImageCache", db);
                        var deletedCount = await deleteCommand.ExecuteNonQueryAsync();
                        Debug.WriteLine($"資料庫快取記錄已清理: {deletedCount} 條記錄");
                    }
                }
                finally
                {
                    _databaseSemaphore.Release();
                }

                Debug.WriteLine("所有圖片快取清理完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理圖片快取失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     刪除快取資料夾中的所有檔案
        /// </summary>
        private async Task DeleteAllCacheFilesAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var cacheFolder = await localFolder.TryGetItemAsync("ImageCache") as StorageFolder;

                if (cacheFolder != null)
                {
                    var files = await cacheFolder.GetFilesAsync();
                    var deletedCount = 0;

                    foreach (var file in files)
                        try
                        {
                            await file.DeleteAsync();
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"刪除快取檔案失敗: {file.Name}, {ex.Message}");
                        }

                    Debug.WriteLine($"成功刪除 {deletedCount} 個快取檔案");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除快取檔案失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     清理過期的圖片快取
        /// </summary>
        /// <param name="daysToKeep">保留天數</param>
        public async Task CleanExpiredCacheAsync(int daysToKeep = 30)
        {
            try
            {
                Debug.WriteLine($"開始清理過期圖片快取，保留 {daysToKeep} 天");

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

                await _databaseSemaphore.WaitAsync();
                try
                {
                    var dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                    using (var db = new SqliteConnection($"Filename={dbPath}"))
                    {
                        await db.OpenAsync();

                        // 獲取過期的圖片記錄
                        var selectCommand = new SqliteCommand(@"
                            SELECT CacheKey, LocalPath FROM ImageCache 
                            WHERE datetime(LastUpdated) < @cutoffDate", db);
                        selectCommand.Parameters.AddWithValue("@cutoffDate",
                            cutoffDate.ToString("yyyy-MM-dd HH:mm:ss"));

                        var expiredRecords = new List<(string CacheKey, string LocalPath)>();
                        using (var reader = await selectCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                                expiredRecords.Add((reader.GetString(0), reader.GetString(1)));
                        }

                        // 刪除過期的檔案和記憶體快取
                        foreach (var record in expiredRecords)
                            try
                            {
                                if (File.Exists(record.LocalPath)) File.Delete(record.LocalPath);

                                // 從記憶體快取中刪除
                                _memoryCache.TryRemove(record.CacheKey, out _);
                            }
                            catch (Exception fileEx)
                            {
                                Debug.WriteLine($"刪除過期圖片檔案失敗: {fileEx.Message}");
                            }

                        // 從資料庫中刪除過期記錄
                        var deleteCommand = new SqliteCommand(@"
                            DELETE FROM ImageCache WHERE datetime(LastUpdated) < @cutoffDate", db);
                        deleteCommand.Parameters.AddWithValue("@cutoffDate",
                            cutoffDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        var deletedCount = await deleteCommand.ExecuteNonQueryAsync();

                        Debug.WriteLine($"清理過期圖片快取完成: 刪除 {deletedCount} 個記錄");
                    }
                }
                finally
                {
                    _databaseSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理過期圖片快取失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取圖片快取統計資訊 - 修復檔案大小統計問題
        /// </summary>
        /// <returns>快取統計資訊</returns>
        public async Task<ImageCacheStats> GetCacheStatsAsync()
        {
            var stats = new ImageCacheStats();

            try
            {
                await _databaseSemaphore.WaitAsync();
                try
                {
                    var dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                    using (var db = new SqliteConnection($"Filename={dbPath}"))
                    {
                        await db.OpenAsync();

                        // 確保表存在
                        var createTableCommand = new SqliteCommand(@"
                            CREATE TABLE IF NOT EXISTS ImageCache (
                                CacheKey TEXT PRIMARY KEY,
                                ImageType INTEGER NOT NULL,
                                EntityId INTEGER NOT NULL,
                                ImageUrl TEXT NOT NULL,
                                LocalPath TEXT NOT NULL,
                                LastUpdated TEXT NOT NULL,
                                FileSize INTEGER DEFAULT 0
                            )", db);
                        await createTableCommand.ExecuteNonQueryAsync();

                        // 先嘗試重新計算所有檔案大小
                        await RecalculateAllFileSizesAsync(db);

                        // 獲取統計資訊
                        var countCommand = new SqliteCommand(@"
                            SELECT COUNT(*) as TotalCount, 
                                   COALESCE(SUM(CAST(FileSize AS INTEGER)), 0) as TotalSize,
                                   COUNT(CASE WHEN ImageType = 1 THEN 1 END) as FriendCount,
                                   COUNT(CASE WHEN ImageType = 2 THEN 1 END) as GroupCount,
                                   COUNT(CASE WHEN ImageType = 3 THEN 1 END) as CurrentCount,
                                   COUNT(CASE WHEN ImageType = 4 THEN 1 END) as MessageCount
                            FROM ImageCache", db);

                        using (var reader = await countCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                stats.TotalCount = reader.GetInt32(0);
                                stats.TotalSizeBytes = reader.GetInt64(1);
                                stats.FriendImageCount = reader.GetInt32(2);
                                stats.GroupImageCount = reader.GetInt32(3);
                                stats.CurrentUserImageCount = reader.GetInt32(4);
                                stats.MessageImageCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                            }
                        }
                    }
                }
                finally
                {
                    _databaseSemaphore.Release();
                }

                stats.MemoryCacheCount = _memoryCache.Count;
                Debug.WriteLine(
                    $"圖片快取統計: 總數 {stats.TotalCount}, 記憶體快取 {stats.MemoryCacheCount}, 總大小 {stats.TotalSizeFormatted}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取圖片快取統計資訊失敗: {ex.Message}");
            }

            return stats;
        }

        /// <summary>
        ///     重新計算所有檔案大小 - 修復統計問題
        /// </summary>
        private async Task RecalculateAllFileSizesAsync(SqliteConnection db)
        {
            try
            {
                var selectCommand = new SqliteCommand(@"
                    SELECT CacheKey, LocalPath, FileSize FROM ImageCache", db);

                var recordsToUpdate = new List<(string CacheKey, string LocalPath, long CurrentSize)>();
                using (var reader = await selectCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var currentSize = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                        recordsToUpdate.Add((reader.GetString(0), reader.GetString(1), currentSize));
                    }
                }

                var updatedCount = 0;
                foreach (var record in recordsToUpdate)
                    try
                    {
                        if (File.Exists(record.LocalPath))
                        {
                            var fileInfo = new FileInfo(record.LocalPath);
                            var actualSize = fileInfo.Length;

                            // 只有當檔案大小不同時才更新
                            if (actualSize != record.CurrentSize)
                            {
                                var updateCommand = new SqliteCommand(@"
                                    UPDATE ImageCache SET FileSize = @fileSize 
                                    WHERE CacheKey = @cacheKey", db);
                                updateCommand.Parameters.AddWithValue("@fileSize", actualSize);
                                updateCommand.Parameters.AddWithValue("@cacheKey", record.CacheKey);
                                await updateCommand.ExecuteNonQueryAsync();

                                updatedCount++;
                                Debug.WriteLine($"更新檔案大小: {record.CacheKey} = {actualSize} bytes");
                            }
                        }
                        else
                        {
                            // 檔案不存在，從資料庫中移除記錄
                            var deleteCommand = new SqliteCommand(@"
                                DELETE FROM ImageCache WHERE CacheKey = @cacheKey", db);
                            deleteCommand.Parameters.AddWithValue("@cacheKey", record.CacheKey);
                            await deleteCommand.ExecuteNonQueryAsync();

                            Debug.WriteLine($"移除不存在的檔案記錄: {record.CacheKey}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"處理檔案大小失敗: {record.CacheKey}, {ex.Message}");
                    }

                if (updatedCount > 0) Debug.WriteLine($"重新計算檔案大小完成，更新了 {updatedCount} 條記錄");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重新計算檔案大小失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理下載佇列 - 改進的分批處理，解決中斷問題
        /// </summary>
        private async void ProcessQueue(object state)
        {
            if (_isProcessing || _batchProcessing)
                return;

            lock (_processLock)
            {
                if (_isProcessing || _batchProcessing)
                    return;
                _isProcessing = true;
            }

            try
            {
                // 只有在有待處理任務且可以獲取批次鎖時才處理
                if (_downloadQueue.Count > 0 && _batchSemaphore.Wait(0))
                {
                    _batchProcessing = true;

                    try
                    {
                        var batch = new List<DownloadTask>();

                        // 收集一個批次的任務
                        for (var i = 0; i < BatchSize && _downloadQueue.TryDequeue(out var task); i++)
                            if (!task.TaskCompletionSource.Task.IsCompleted)
                                batch.Add(task);

                        if (batch.Count > 0)
                        {
                            Debug.WriteLine($"開始處理批次下載: {batch.Count} 個任務，剩餘佇列: {_downloadQueue.Count}");
                            await ProcessBatchAsync(batch);
                            Debug.WriteLine($"批次下載完成，等待 {BatchDelayMs}ms");
                            await Task.Delay(BatchDelayMs); // 批次間延遲
                        }
                    }
                    finally
                    {
                        _batchProcessing = false;
                        _batchSemaphore.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理下載佇列時發生錯誤: {ex.Message}");
                _batchProcessing = false;
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        ///     處理一個批次的下載任務 - 改進錯誤處理
        /// </summary>
        private async Task ProcessBatchAsync(List<DownloadTask> batch)
        {
            var tasks = new List<Task>();

            foreach (var task in batch)
            {
                tasks.Add(ProcessDownloadTaskAsync(task));

                // 任務間小延遲，確保不會過載
                if (tasks.Count < batch.Count) await Task.Delay(RequestDelayMs);
            }

            if (tasks.Count > 0)
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"批次下載過程中發生錯誤: {ex.Message}");
                }
        }

        /// <summary>
        ///     處理單個下載任務 - 改進重試邏輯
        /// </summary>
        private async Task ProcessDownloadTaskAsync(DownloadTask task)
        {
            try
            {
                // 檢查任務是否仍然有效
                if (task.TaskCompletionSource.Task.IsCompleted) return;

                // 檢查是否需要限制請求頻率
                await ThrottleRequestAsync(task.ImageUrl);

                var result = await DownloadImageAsync(task);

                if (result != null)
                {
                    _memoryCache.TryAdd(task.CacheKey, result);
                    task.TaskCompletionSource.TrySetResult(result);

                    // 非同步觸發UI更新
                    _ = TriggerUIUpdateAsync(task.CacheKey, result);

                    Debug.WriteLine($"圖片下載完成: {task.CacheKey}");
                }
                else
                {
                    // 重試邏輯
                    if (task.RetryCount < MaxRetryCount)
                    {
                        task.RetryCount++;
                        task.CreatedTime = DateTime.Now.AddSeconds(Math.Pow(2, task.RetryCount)); // 指數退避

                        // 重新加入佇列，但不要無限增長佇列
                        if (_downloadQueue.Count < MaxQueueSize)
                        {
                            _downloadQueue.Enqueue(task);
                            Debug.WriteLine($"圖片下載失敗，將重試: {task.CacheKey}, 重試次數: {task.RetryCount}");
                        }
                        else
                        {
                            Debug.WriteLine($"佇列已滿，放棄重試: {task.CacheKey}");
                            var defaultImage = await CreateDefaultImageAsync();
                            task.TaskCompletionSource.TrySetResult(defaultImage);
                        }
                    }
                    else
                    {
                        var defaultImage = await CreateDefaultImageAsync();
                        task.TaskCompletionSource.TrySetResult(defaultImage);
                        Debug.WriteLine($"圖片下載最終失敗: {task.CacheKey}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理下載任務時發生錯誤: {task.CacheKey}, {ex.Message}");

                try
                {
                    var defaultImage = await CreateDefaultImageAsync();
                    task.TaskCompletionSource.TrySetResult(defaultImage);
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"設定預設圖片失敗: {ex2.Message}");
                    task.TaskCompletionSource.TrySetException(ex);
                }
            }
            finally
            {
                _pendingDownloads.TryRemove(task.CacheKey, out _);
            }
        }

        /// <summary>
        ///     下載圖片
        /// </summary>
        private async Task<BitmapImage> DownloadImageAsync(DownloadTask task)
        {
            try
            {
                Debug.WriteLine($"開始下載圖片: {task.CacheKey} -> {task.ImageUrl}");

                using (var response = await _httpClient.GetAsync(task.ImageUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var imageData = await response.Content.ReadAsByteArrayAsync();

                        if (imageData?.Length > 0)
                        {
                            // 非同步保存到本地快取（不阻塞UI）
                            _ = SaveImageToCacheAsync(task.CacheKey, task.ImageUrl, imageData, task.ImageType, task.Id);

                            // 在UI執行緒創建BitmapImage
                            return await CreateBitmapImageAsync(imageData);
                        }
                    }

                    Debug.WriteLine($"圖片下載失敗: {task.CacheKey}, 狀態碼: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"下載圖片時發生例外: {task.CacheKey}, {ex.Message}");
            }

            return null;
        }

        /// <summary>
        ///     在UI執行緒創建BitmapImage
        /// </summary>
        private async Task<BitmapImage> CreateBitmapImageAsync(byte[] imageData)
        {
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
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"創建BitmapImage失敗: {ex.Message}");
                        bitmapImage = null;
                    }
                });

            return bitmapImage;
        }

        /// <summary>
        ///     從檔案載入圖片
        /// </summary>
        private async Task<BitmapImage> LoadImageFromFileAsync(string localPath)
        {
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
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"從檔案載入圖片失敗: {ex.Message}");
                        bitmapImage = null;
                    }
                });

            return bitmapImage;
        }

        /// <summary>
        ///     創建預設圖片
        /// </summary>
        private async Task<BitmapImage> CreateDefaultImageAsync()
        {
            BitmapImage defaultImage = null;

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        defaultImage = new BitmapImage();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"創建預設圖片失敗: {ex.Message}");
                    }
                });

            return defaultImage;
        }

        /// <summary>
        ///     限制請求頻率
        /// </summary>
        private async Task ThrottleRequestAsync(string url)
        {
            var host = new Uri(url).Host;

            if (_lastRequestTime.TryGetValue(host, out var lastTime))
            {
                var elapsed = DateTime.Now - lastTime;
                if (elapsed.TotalMilliseconds < RequestDelayMs)
                {
                    var delay = RequestDelayMs - (int)elapsed.TotalMilliseconds;
                    if (delay > 0) await Task.Delay(delay);
                }
            }

            _lastRequestTime.AddOrUpdate(host, DateTime.Now, (key, value) => DateTime.Now);
        }

        /// <summary>
        ///     非同步保存圖片到本地快取（串列寫入資料庫）
        /// </summary>
        private async Task SaveImageToCacheAsync(string cacheKey, string imageUrl, byte[] imageData,
            ImageType imageType, long id)
        {
            try
            {
                // 保存檔案
                var cacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    "ImageCache", CreationCollisionOption.OpenIfExists);
                var fileName = $"{cacheKey}.jpg";
                var file = await cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                // 使用 FileIO.WriteBytesAsync 替代
                await FileIO.WriteBytesAsync(file, imageData);

                // 串列寫入資料庫
                await _databaseSemaphore.WaitAsync();
                try
                {
                    await SaveImageCacheInfoAsync(cacheKey, imageType, id, imageUrl, file.Path, imageData.Length);
                    Debug.WriteLine($"圖片快取保存完成: {cacheKey}, 大小: {imageData.Length} 位元組");
                }
                finally
                {
                    _databaseSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存圖片快取失敗: {cacheKey}, {ex.Message}");
            }
        }

        /// <summary>
        ///     保存圖片快取資訊到資料庫
        /// </summary>
        private async Task SaveImageCacheInfoAsync(string cacheKey, ImageType imageType, long id,
            string imageUrl, string localPath, int fileSize)
        {
            try
            {
                var dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbPath}"))
                {
                    await db.OpenAsync();

                    // 確保表存在
                    var createTableCommand = new SqliteCommand(@"
                        CREATE TABLE IF NOT EXISTS ImageCache (
                            CacheKey TEXT PRIMARY KEY,
                            ImageType INTEGER NOT NULL,
                            EntityId INTEGER NOT NULL,
                            ImageUrl TEXT NOT NULL,
                            LocalPath TEXT NOT NULL,
                            LastUpdated TEXT NOT NULL,
                            FileSize INTEGER DEFAULT 0
                        )", db);
                    await createTableCommand.ExecuteNonQueryAsync();

                    // 插入或更新記錄
                    var insertCommand = new SqliteCommand(@"
                        INSERT OR REPLACE INTO ImageCache 
                        (CacheKey, ImageType, EntityId, ImageUrl, LocalPath, LastUpdated, FileSize) 
                        VALUES (@cacheKey, @imageType, @entityId, @imageUrl, @localPath, @lastUpdated, @fileSize)",
                        db);

                    insertCommand.Parameters.AddWithValue("@cacheKey", cacheKey);
                    insertCommand.Parameters.AddWithValue("@imageType", (int)imageType);
                    insertCommand.Parameters.AddWithValue("@entityId", id);
                    insertCommand.Parameters.AddWithValue("@imageUrl", imageUrl);
                    insertCommand.Parameters.AddWithValue("@localPath", localPath);
                    insertCommand.Parameters.AddWithValue("@lastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    insertCommand.Parameters.AddWithValue("@fileSize", fileSize);

                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存圖片快取資訊到資料庫失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取快取的圖片路徑
        /// </summary>
        private async Task<string> GetCachedImagePathAsync(string cacheKey)
        {
            await _databaseSemaphore.WaitAsync();
            try
            {
                var dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbPath}"))
                {
                    await db.OpenAsync();

                    var selectCommand = new SqliteCommand(@"
                        SELECT LocalPath, LastUpdated FROM ImageCache 
                        WHERE CacheKey = @cacheKey", db);
                    selectCommand.Parameters.AddWithValue("@cacheKey", cacheKey);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var localPath = reader.GetString(0);
                            var lastUpdatedStr = reader.GetString(1);

                            if (File.Exists(localPath))
                                if (DateTime.TryParse(lastUpdatedStr, out var lastUpdated) &&
                                    DateTime.Now - lastUpdated < TimeSpan.FromDays(7))
                                    return localPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取快取圖片路徑失敗: {ex.Message}");
            }
            finally
            {
                _databaseSemaphore.Release();
            }

            return null;
        }

        /// <summary>
        ///     刪除快取的圖片
        /// </summary>
        private async Task DeleteCachedImageAsync(string cacheKey)
        {
            try
            {
                _memoryCache.TryRemove(cacheKey, out _);

                await _databaseSemaphore.WaitAsync();
                try
                {
                    var dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                    using (var db = new SqliteConnection($"Filename={dbPath}"))
                    {
                        await db.OpenAsync();

                        var selectCommand = new SqliteCommand(@"
                            SELECT LocalPath FROM ImageCache WHERE CacheKey = @cacheKey", db);
                        selectCommand.Parameters.AddWithValue("@cacheKey", cacheKey);

                        var localPath = await selectCommand.ExecuteScalarAsync() as string;
                        if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath)) File.Delete(localPath);

                        var deleteCommand = new SqliteCommand(@"
                            DELETE FROM ImageCache WHERE CacheKey = @cacheKey", db);
                        deleteCommand.Parameters.AddWithValue("@cacheKey", cacheKey);
                        await deleteCommand.ExecuteNonQueryAsync();
                    }
                }
                finally
                {
                    _databaseSemaphore.Release();
                }

                Debug.WriteLine($"刪除快取圖片: {cacheKey}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除快取圖片失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     觸發UI更新
        /// </summary>
        private async Task TriggerUIUpdateAsync(string cacheKey, BitmapImage image)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () => { OnImageLoaded?.Invoke(cacheKey, image); });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"觸發UI更新失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取優先級
        /// </summary>
        private int GetPriority(ImageType imageType)
        {
            switch (imageType)
            {
                case ImageType.CurrentUser:
                    return 1; // 最高優先級
                case ImageType.Friend:
                    return 2;
                case ImageType.Group:
                    return 3;
                case ImageType.MessageImage:
                    return 4;
                default:
                    return 5;
            }
        }

        /// <summary>
        ///     獲取佇列長度
        /// </summary>
        public int GetQueueLength()
        {
            return _downloadQueue.Count;
        }

        /// <summary>
        ///     獲取統計資訊
        /// </summary>
        public ImageDownloadStats GetStats()
        {
            return new ImageDownloadStats
            {
                QueueLength = _downloadQueue.Count,
                PendingDownloads = _pendingDownloads.Count,
                MemoryCacheCount = _memoryCache.Count,
                IsProcessing = _isProcessing || _batchProcessing
            };
        }

        /// <summary>
        ///     清理資源
        /// </summary>
        public void Dispose()
        {
            _processTimer?.Dispose();
            _httpClient?.Dispose();
            _batchSemaphore?.Dispose();
            _databaseSemaphore?.Dispose();
        }
    }

    /// <summary>
    ///     下載任務
    /// </summary>
    internal class DownloadTask
    {
        public string CacheKey { get; set; }
        public string ImageUrl { get; set; }
        public ImageType ImageType { get; set; }
        public long Id { get; set; }
        public TaskCompletionSource<BitmapImage> TaskCompletionSource { get; set; }
        public int Priority { get; set; }
        public DateTime CreatedTime { get; set; }
        public int RetryCount { get; set; }
    }

    /// <summary>
    ///     圖片類型枚舉
    /// </summary>
    public enum ImageType
    {
        Friend = 1,
        Group = 2,
        CurrentUser = 3,
        MessageImage = 4
    }

    /// <summary>
    ///     圖片下載統計資訊
    /// </summary>
    public class ImageDownloadStats
    {
        public int QueueLength { get; set; }
        public int PendingDownloads { get; set; }
        public int MemoryCacheCount { get; set; }
        public bool IsProcessing { get; set; }
    }

    /// <summary>
    ///     圖片快取統計資訊 - 用於ConcurrentImageDownloader
    /// </summary>
    public class ImageCacheStats
    {
        public int TotalCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public int FriendImageCount { get; set; }
        public int GroupImageCount { get; set; }
        public int CurrentUserImageCount { get; set; }
        public int MessageImageCount { get; set; }
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