using System;
using System.Diagnostics;
using System.Net.Http;
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

        public async Task<string> PrefetchAsync(string remoteUrl, string category, long id)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl) || id <= 0) return null;
            try
            {
                var folder = ApplicationData.Current.TemporaryFolder;
                var fileName = $"ava_{category}_{id}.jpg";
                var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                var props = await file.GetBasicPropertiesAsync();
                if (props.Size > 0 && !IsExpired(file.DateCreated.DateTime)) return file.Path; // hit

                for (var attempt = 0; attempt <= MaxRetry; attempt++)
                {
                    try
                    {
                        var bytes = await _http.GetByteArrayAsync(remoteUrl);
                        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            var dw = new Windows.Storage.Streams.DataWriter(stream);
                            dw.WriteBytes(bytes);
                            await dw.StoreAsync();
                            await stream.FlushAsync();
                        }
                        return file.Path;
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
            return null;
        }

        private bool IsExpired(DateTime created) => (DateTime.Now - created) > Expire;
    }
}