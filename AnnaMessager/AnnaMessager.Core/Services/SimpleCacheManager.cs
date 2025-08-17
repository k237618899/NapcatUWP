using System.Collections.Generic;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    public class SimpleCacheManager : ICacheManager
    {
        public Task<List<ChatItem>> LoadCachedChatsAsync()
        {
            return Task.FromResult(new List<ChatItem>());
        }

        public Task CacheChatItemAsync(ChatItem chatItem)
        {
            return Task.FromResult(0);
        }

        public Task CacheChatItemsAsync(IEnumerable<ChatItem> chatItems)
        {
            return Task.FromResult(0);
        }

        public Task<List<ContactItem>> LoadCachedContactsAsync()
        {
            return Task.FromResult(new List<ContactItem>());
        }

        public Task CacheContactItemAsync(ContactItem contactItem)
        {
            return Task.FromResult(0);
        }

        public Task<List<GroupItem>> LoadCachedGroupsAsync()
        {
            return Task.FromResult(new List<GroupItem>());
        }

        public Task CacheGroupItemAsync(GroupItem groupItem)
        {
            return Task.FromResult(0);
        }

        public Task DeleteChatCacheAsync(long chatId, bool isGroup)
        {
            return Task.FromResult(0);
        }

        public Task<CacheInfo> CalculateCacheSizeAsync()
        {
            return Task.FromResult(new CacheInfo
            {
                TotalSize = 0,
                TotalMessages = 0,
                ImagesCacheSize = 0,
                MessagesCacheSize = 0
            });
        }

        public Task ClearAllCacheAsync()
        {
            return Task.FromResult(0);
        }

        public Task ClearExpiredCacheAsync()
        {
            return Task.FromResult(0);
        }

        public Task ClearCacheAsync(CacheType cacheType)
        {
            return Task.FromResult(0);
        }

        public Task CacheMessageAsync(long chatId, bool isGroup, MessageItem message)
        {
            return Task.FromResult(0);
        }

        public Task CacheMessagesAsync(long chatId, bool isGroup, IEnumerable<MessageItem> messages)
        {
            return Task.FromResult(0);
        }

        public Task<List<MessageItem>> LoadCachedMessagesAsync(long chatId, bool isGroup, int take = 50)
        {
            return Task.FromResult(new List<MessageItem>());
        }
    }
}

// 已移除 SimpleCacheManager 測試實作，使用平台實際 UwpCacheManager。
// 此檔案保留以防其他專案仍有引用，若無引用可刪除