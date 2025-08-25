using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using AnnaMessager.Core.Services;

namespace AnnaMessager.UWP.Services
{
    public class UwpAvatarCacheService : IAvatarCacheService
    {
        private static readonly HttpClient _http = new HttpClient();
        private const int MaxRetry = 2;
        private static readonly TimeSpan Expire = TimeSpan.FromDays(3);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public async Task<string> PrefetchAsync(string remoteUrl, string category, long id)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl) || id <= 0) return null;
            
            var fileName = $"ava_{category}_{id}.jpg";
            var msAppDataUri = $"ms-appdata:///temp/{fileName}";
            
            // 使用文件名作为锁的键，避免并发访问同一文件
            var fileLock = _fileLocks.GetOrAdd(fileName, _ => new SemaphoreSlim(1, 1));
            
            try
            {
                await fileLock.WaitAsync();
                
                var folder = ApplicationData.Current.TemporaryFolder;
                var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                var props = await file.GetBasicPropertiesAsync();
                
                if (props.Size > 0 && !IsExpired(file.DateCreated.DateTime)) 
                {
                    Debug.WriteLine($"头像缓存命中: {id} -> {msAppDataUri}");
                    return msAppDataUri; // hit - 直接返回 ms-appdata URI
                }

                for (var attempt = 0; attempt <= MaxRetry; attempt++)
                {
                    try
                    {
                        var bytes = await _http.GetByteArrayAsync(remoteUrl);
                        
                        // 重新创建文件以避免访问冲突
                        file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                        
                        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            var dw = new Windows.Storage.Streams.DataWriter(stream);
                            dw.WriteBytes(bytes);
                            await dw.StoreAsync();
                            await stream.FlushAsync();
                            dw.Dispose();
                        }
                        
                        Debug.WriteLine($"头像缓存成功: {id} -> {msAppDataUri}");
                        return msAppDataUri; // 返回 ms-appdata URI 格式
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Avatar fetch fail (attempt {attempt+1}): {remoteUrl} - {ex.Message}");
                        if (attempt == MaxRetry) return null;
                        await Task.Delay(200 * (attempt + 1));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Avatar prefetch error: {ex.Message}");
            }
            finally
            {
                fileLock.Release();
            }
            return null;
        }

        private bool IsExpired(DateTime created) => (DateTime.Now - created) > Expire;
    }
}