using System.Collections.Generic;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     緩存管理器接口 - 統一管理各類數據緩存
    /// </summary>
    public interface ICacheManager
    {
        /// <summary>
        ///     緩存聊天數據
        /// </summary>
        Task CacheChatItemAsync(ChatItem chatItem);

        /// <summary>
        ///     批量緩存聊天數據
        /// </summary>
        Task CacheChatItemsAsync(IEnumerable<ChatItem> chatItems);

        /// <summary>
        ///     載入緩存的聊天列表
        /// </summary>
        Task<List<ChatItem>> LoadCachedChatsAsync();

        /// <summary>
        ///     緩存聯絡人數據
        /// </summary>
        Task CacheContactItemAsync(ContactItem contactItem);

        /// <summary>
        ///     載入緩存的聯絡人列表
        /// </summary>
        Task<List<ContactItem>> LoadCachedContactsAsync();

        /// <summary>
        ///     緩存群組數據
        /// </summary>
        Task CacheGroupItemAsync(GroupItem groupItem);

        /// <summary>
        ///     載入緩存的群組列表
        /// </summary>
        Task<List<GroupItem>> LoadCachedGroupsAsync();

        /// <summary>
        ///     計算緩存大小信息
        /// </summary>
        Task<CacheInfo> CalculateCacheSizeAsync();

        /// <summary>
        ///     清除指定類型的緩存
        /// </summary>
        Task ClearCacheAsync(CacheType cacheType);

        /// <summary>
        ///     清除所有緩存
        /// </summary>
        Task ClearAllCacheAsync();

        /// <summary>
        ///     清除過期緩存
        /// </summary>
        Task ClearExpiredCacheAsync();

        /// <summary>
        ///     刪除特定聊天的緩存數據
        /// </summary>
        Task DeleteChatCacheAsync(long chatId, bool isGroup);
    }

    /// <summary>
    ///     緩存類型枚舉
    /// </summary>
    public enum CacheType
    {
        Chats,
        Contacts,
        Groups,
        Messages,
        Images,
        Avatars,
        All
    }
}