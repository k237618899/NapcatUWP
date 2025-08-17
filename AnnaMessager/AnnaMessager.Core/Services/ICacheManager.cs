using System.Collections.Generic;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;
using MvvmCross.Core.ViewModels;

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

        // ===== 新增: 消息緩存 =====
        /// <summary>
        /// 緩存一條消息
        /// </summary>
        Task CacheMessageAsync(long chatId, bool isGroup, MessageItem message);

        /// <summary>
        /// 批量緩存消息
        /// </summary>
        Task CacheMessagesAsync(long chatId, bool isGroup, IEnumerable<MessageItem> messages);

        /// <summary>
        /// 載入聊天的已緩存消息（按時間升序返回最新 N 條）
        /// </summary>
        Task<List<MessageItem>> LoadCachedMessagesAsync(long chatId, bool isGroup, int take = 50);
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

    /// <summary>
    ///     緩存信息類
    /// </summary>
    public class CacheInfo : MvxNotifyPropertyChanged
    {
        private long _avatarsCacheSize;
        private long _imagesCacheSize;
        private long _messagesCacheSize;
        private int _totalAvatars;
        private int _totalImages;
        private int _totalMessages;
        private long _totalSize;

        public long TotalSize
        {
            get => _totalSize;
            set => SetProperty(ref _totalSize, value);
        }

        public long ImagesCacheSize
        {
            get => _imagesCacheSize;
            set => SetProperty(ref _imagesCacheSize, value);
        }

        public long AvatarsCacheSize
        {
            get => _avatarsCacheSize;
            set => SetProperty(ref _avatarsCacheSize, value);
        }

        public long MessagesCacheSize
        {
            get => _messagesCacheSize;
            set => SetProperty(ref _messagesCacheSize, value);
        }

        public int TotalMessages
        {
            get => _totalMessages;
            set => SetProperty(ref _totalMessages, value);
        }

        public int TotalImages
        {
            get => _totalImages;
            set => SetProperty(ref _totalImages, value);
        }

        public int TotalAvatars
        {
            get => _totalAvatars;
            set => SetProperty(ref _totalAvatars, value);
        }

        // 格式化大小顯示
        public string TotalSizeDisplay => FormatBytes(TotalSize);
        public string ImagesCacheSizeDisplay => FormatBytes(ImagesCacheSize);
        public string AvatarsCacheSizeDisplay => FormatBytes(AvatarsCacheSize);
        public string MessagesCacheSizeDisplay => FormatBytes(MessagesCacheSize);

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}