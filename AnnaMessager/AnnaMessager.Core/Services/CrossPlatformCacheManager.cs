using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;
using MvvmCross.Platform;
using Newtonsoft.Json;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     跨平台緩存管理器實現 - PCL 兼容，基于平台特定存儲
    /// </summary>
    public class CrossPlatformCacheManager : ICacheManager
    {
        private readonly object _lockObject = new object();

        // 緩存超時時間（分鐘）
        private readonly int _memoryTTL = 5;
        private readonly IPlatformSettingsService _platformService;

        // 內存緩存 - 提高訪問速度
        private List<ChatItem> _chatListCache;
        private DateTime _chatListCacheTime;
        private List<ContactItem> _contactListCache;
        private DateTime _contactListCacheTime;
        private List<GroupItem> _groupListCache;
        private DateTime _groupListCacheTime;

        // 新增：簡易消息緩存內存字典（只做短暫快取，避免破壞原有架構）
        private readonly Dictionary<string, List<MessageItem>> _messageMemoryCache = new Dictionary<string, List<MessageItem>>();
        private readonly int _messageMemoryLimitPerChat = 100; // 每個聊天最多暫存 100 條

        public CrossPlatformCacheManager()
        {
            _platformService = Mvx.Resolve<IPlatformSettingsService>();
        }

        #region Chat Item 緩存操作

        public async Task CacheChatItemAsync(ChatItem chatItem)
        {
            try
            {
                var cachedChats = await LoadCachedChatsAsync();

                // 查找是否已存在
                var existingIndex =
                    cachedChats.FindIndex(c => c.ChatId == chatItem.ChatId && c.IsGroup == chatItem.IsGroup);

                if (existingIndex >= 0)
                    // 更新現有項目
                    cachedChats[existingIndex] = chatItem;
                else
                    // 添加新項目
                    cachedChats.Add(chatItem);

                await SaveChatListAsync(cachedChats);

                // 更新內存緩存
                lock (_lockObject)
                {
                    _chatListCache = cachedChats;
                    _chatListCacheTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"緩存聊天項目失敗: {ex.Message}");
            }
        }

        public async Task CacheChatItemsAsync(IEnumerable<ChatItem> chatItems)
        {
            try
            {
                var chatList = chatItems?.ToList() ?? new List<ChatItem>();
                await SaveChatListAsync(chatList);

                // 更新內存緩存
                lock (_lockObject)
                {
                    _chatListCache = chatList;
                    _chatListCacheTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"批量緩存聊天項目失敗: {ex.Message}");
            }
        }

        public async Task<List<ChatItem>> LoadCachedChatsAsync()
        {
            try
            {
                // 檢查內存緩存
                lock (_lockObject)
                {
                    if (_chatListCache != null &&
                        (DateTime.Now - _chatListCacheTime).TotalMinutes < _memoryTTL)
                        return new List<ChatItem>(_chatListCache);
                }

                // 從持久化存儲載入
                var json = await _platformService.GetValueAsync("CachedChatList");
                if (string.IsNullOrEmpty(json)) return new List<ChatItem>();

                var chatList = JsonConvert.DeserializeObject<List<ChatItem>>(json) ?? new List<ChatItem>();

                // 更新內存緩存
                lock (_lockObject)
                {
                    _chatListCache = chatList;
                    _chatListCacheTime = DateTime.Now;
                }

                return chatList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存聊天列表失敗: {ex.Message}");
                return new List<ChatItem>();
            }
        }

        private async Task SaveChatListAsync(List<ChatItem> chatList)
        {
            try
            {
                var json = JsonConvert.SerializeObject(chatList);
                await _platformService.SetValueAsync("CachedChatList", json);
                await _platformService.SetValueAsync("CachedChatList_LastUpdated",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存聊天列表緩存失敗: {ex.Message}");
            }
        }

        #endregion

        #region Contact Item 緩存操作

        public async Task CacheContactItemAsync(ContactItem contactItem)
        {
            try
            {
                var cachedContacts = await LoadCachedContactsAsync();

                // 查找是否已存在
                var existingIndex = cachedContacts.FindIndex(c => c.UserId == contactItem.UserId);

                if (existingIndex >= 0)
                    // 更新現有項目
                    cachedContacts[existingIndex] = contactItem;
                else
                    // 添加新項目
                    cachedContacts.Add(contactItem);

                await SaveContactListAsync(cachedContacts);

                // 更新內存緩存
                lock (_lockObject)
                {
                    _contactListCache = cachedContacts;
                    _contactListCacheTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"緩存聯絡人項目失敗: {ex.Message}");
            }
        }

        public async Task<List<ContactItem>> LoadCachedContactsAsync()
        {
            try
            {
                // 檢查內存緩存
                lock (_lockObject)
                {
                    if (_contactListCache != null &&
                        (DateTime.Now - _contactListCacheTime).TotalMinutes < _memoryTTL)
                        return new List<ContactItem>(_contactListCache);
                }

                // 從持久化存儲載入
                var json = await _platformService.GetValueAsync("CachedContactList");
                if (string.IsNullOrEmpty(json)) return new List<ContactItem>();

                var contactList = JsonConvert.DeserializeObject<List<ContactItem>>(json) ?? new List<ContactItem>();

                // 更新內存緩存
                lock (_lockObject)
                {
                    _contactListCache = contactList;
                    _contactListCacheTime = DateTime.Now;
                }

                return contactList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存聯絡人列表失敗: {ex.Message}");
                return new List<ContactItem>();
            }
        }

        private async Task SaveContactListAsync(List<ContactItem> contactList)
        {
            try
            {
                var json = JsonConvert.SerializeObject(contactList);
                await _platformService.SetValueAsync("CachedContactList", json);
                await _platformService.SetValueAsync("CachedContactList_LastUpdated",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存聯絡人列表緩存失敗: {ex.Message}");
            }
        }

        #endregion

        #region Group Item 緩存操作

        public async Task CacheGroupItemAsync(GroupItem groupItem)
        {
            try
            {
                var cachedGroups = await LoadCachedGroupsAsync();

                // 查找是否已存在
                var existingIndex = cachedGroups.FindIndex(g => g.GroupId == groupItem.GroupId);

                if (existingIndex >= 0)
                    // 更新現有項目
                    cachedGroups[existingIndex] = groupItem;
                else
                    // 添加新項目
                    cachedGroups.Add(groupItem);

                await SaveGroupListAsync(cachedGroups);

                // 更新內存緩存
                lock (_lockObject)
                {
                    _groupListCache = cachedGroups;
                    _groupListCacheTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"緩存群組項目失敗: {ex.Message}");
            }
        }

        public async Task<List<GroupItem>> LoadCachedGroupsAsync()
        {
            try
            {
                // 檢查內存緩存
                lock (_lockObject)
                {
                    if (_groupListCache != null &&
                        (DateTime.Now - _groupListCacheTime).TotalMinutes < _memoryTTL)
                        return new List<GroupItem>(_groupListCache);
                }

                // 從持久化存儲載入
                var json = await _platformService.GetValueAsync("CachedGroupList");
                if (string.IsNullOrEmpty(json)) return new List<GroupItem>();

                var groupList = JsonConvert.DeserializeObject<List<GroupItem>>(json) ?? new List<GroupItem>();

                // 更新內存緩存
                lock (_lockObject)
                {
                    _groupListCache = groupList;
                    _groupListCacheTime = DateTime.Now;
                }

                return groupList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存群組列表失敗: {ex.Message}");
                return new List<GroupItem>();
            }
        }

        private async Task SaveGroupListAsync(List<GroupItem> groupList)
        {
            try
            {
                var json = JsonConvert.SerializeObject(groupList);
                await _platformService.SetValueAsync("CachedGroupList", json);
                await _platformService.SetValueAsync("CachedGroupList_LastUpdated",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存群組列表緩存失敗: {ex.Message}");
            }
        }

        #endregion

        #region 緩存管理操作

        public async Task<CacheInfo> CalculateCacheSizeAsync()
        {
            try
            {
                var cacheInfo = new CacheInfo();

                // 計算各類緩存的大小和數量
                var chatListJson = await _platformService.GetValueAsync("CachedChatList") ?? string.Empty;
                var contactListJson = await _platformService.GetValueAsync("CachedContactList") ?? string.Empty;
                var groupListJson = await _platformService.GetValueAsync("CachedGroupList") ?? string.Empty;

                // 計算大小（以字節為單位）
                var chatListSize = Encoding.UTF8.GetByteCount(chatListJson);
                var contactListSize = Encoding.UTF8.GetByteCount(contactListJson);
                var groupListSize = Encoding.UTF8.GetByteCount(groupListJson);

                // 計算數量
                var chatList = string.IsNullOrEmpty(chatListJson)
                    ? new List<ChatItem>()
                    : JsonConvert.DeserializeObject<List<ChatItem>>(chatListJson) ?? new List<ChatItem>();
                var contactList = string.IsNullOrEmpty(contactListJson)
                    ? new List<ContactItem>()
                    : JsonConvert.DeserializeObject<List<ContactItem>>(contactListJson) ?? new List<ContactItem>();
                var groupList = string.IsNullOrEmpty(groupListJson)
                    ? new List<GroupItem>()
                    : JsonConvert.DeserializeObject<List<GroupItem>>(groupListJson) ?? new List<GroupItem>();

                cacheInfo.MessagesCacheSize = chatListSize + contactListSize + groupListSize;
                cacheInfo.TotalMessages = chatList.Count;

                // 預留給圖片和頭像緩存的統計
                cacheInfo.ImagesCacheSize = 0; // 這將由現有的圖片緩存系統提供
                cacheInfo.AvatarsCacheSize = 0; // 這將由現有的圖片緩存系統提供

                cacheInfo.TotalSize =
                    cacheInfo.MessagesCacheSize + cacheInfo.ImagesCacheSize + cacheInfo.AvatarsCacheSize;

                return cacheInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"計算緩存大小失敗: {ex.Message}");
                return new CacheInfo();
            }
        }

        public async Task ClearCacheAsync(CacheType cacheType)
        {
            try
            {
                switch (cacheType)
                {
                    case CacheType.Chats:
                        await _platformService.RemoveValueAsync("CachedChatList");
                        await _platformService.RemoveValueAsync("CachedChatList_LastUpdated");
                        lock (_lockObject)
                        {
                            _chatListCache = null;
                        }

                        break;

                    case CacheType.Contacts:
                        await _platformService.RemoveValueAsync("CachedContactList");
                        await _platformService.RemoveValueAsync("CachedContactList_LastUpdated");
                        lock (_lockObject)
                        {
                            _contactListCache = null;
                        }

                        break;

                    case CacheType.Groups:
                        await _platformService.RemoveValueAsync("CachedGroupList");
                        await _platformService.RemoveValueAsync("CachedGroupList_LastUpdated");
                        lock (_lockObject)
                        {
                            _groupListCache = null;
                        }

                        break;

                    case CacheType.All:
                        await ClearAllCacheAsync();
                        break;
                }

                Debug.WriteLine($"清除緩存完成: {cacheType}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除緩存失敗: {cacheType}, {ex.Message}");
            }
        }

        public async Task ClearAllCacheAsync()
        {
            try
            {
                // 清除所有數據緩存
                await _platformService.RemoveValueAsync("CachedChatList");
                await _platformService.RemoveValueAsync("CachedChatList_LastUpdated");
                await _platformService.RemoveValueAsync("CachedContactList");
                await _platformService.RemoveValueAsync("CachedContactList_LastUpdated");
                await _platformService.RemoveValueAsync("CachedGroupList");
                await _platformService.RemoveValueAsync("CachedGroupList_LastUpdated");

                // 清除內存緩存
                lock (_lockObject)
                {
                    _chatListCache = null;
                    _contactListCache = null;
                    _groupListCache = null;
                }

                Debug.WriteLine("清除所有緩存完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除所有緩存失敗: {ex.Message}");
            }
        }

        public async Task ClearExpiredCacheAsync()
        {
            try
            {
                var expireTime = DateTime.Now.AddDays(-7); // 7天過期

                // 檢查並清理過期的聊天緩存
                await ClearExpiredCacheAsync("CachedChatList_LastUpdated", "CachedChatList", expireTime);
                await ClearExpiredCacheAsync("CachedContactList_LastUpdated", "CachedContactList", expireTime);
                await ClearExpiredCacheAsync("CachedGroupList_LastUpdated", "CachedGroupList", expireTime);

                Debug.WriteLine("清除過期緩存完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除過期緩存失敗: {ex.Message}");
            }
        }

        private async Task ClearExpiredCacheAsync(string timestampKey, string dataKey, DateTime expireTime)
        {
            try
            {
                var timestampStr = await _platformService.GetValueAsync(timestampKey);
                if (!string.IsNullOrEmpty(timestampStr) &&
                    DateTime.TryParse(timestampStr, out var lastUpdated) &&
                    lastUpdated < expireTime)
                {
                    await _platformService.RemoveValueAsync(timestampKey);
                    await _platformService.RemoveValueAsync(dataKey);
                    Debug.WriteLine($"清除過期緩存: {dataKey}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"檢查過期緩存失敗: {dataKey}, {ex.Message}");
            }
        }

        public async Task DeleteChatCacheAsync(long chatId, bool isGroup)
        {
            try
            {
                var cachedChats = await LoadCachedChatsAsync();
                var itemToRemove = cachedChats.FirstOrDefault(c => c.ChatId == chatId && c.IsGroup == isGroup);

                if (itemToRemove != null)
                {
                    cachedChats.Remove(itemToRemove);
                    await SaveChatListAsync(cachedChats);

                    // 更新內存緩存
                    lock (_lockObject)
                    {
                        _chatListCache = cachedChats;
                        _chatListCacheTime = DateTime.Now;
                    }

                    Debug.WriteLine($"刪除聊天緩存: {chatId}, isGroup: {isGroup}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除聊天緩存失敗: {ex.Message}");
            }
        }

        #endregion // 緩存管理操作

        #region Message Cache 新增方法實作
        public Task CacheMessageAsync(long chatId, bool isGroup, MessageItem message)
        {
            try
            {
                if (message == null) return Task.FromResult(0);
                var key = GetMessageCacheKey(chatId, isGroup);
                lock (_lockObject)
                {
                    List<MessageItem> list;
                    if (!_messageMemoryCache.TryGetValue(key, out list)) { list = new List<MessageItem>(); _messageMemoryCache[key] = list; }

                    if (message.MessageId != 0)
                    {
                        var existing = list.FirstOrDefault(x => x.MessageId == message.MessageId);
                        if (existing != null)
                        {
                            // 合併更新 (覆蓋最新資訊)
                            existing.Content = message.Content;
                            existing.Time = message.Time;
                            existing.IsFromSelf = message.IsFromSelf;
                            existing.SenderId = message.SenderId;
                            existing.SenderName = message.SenderName;
                            existing.MessageType = message.MessageType;
                            existing.ImageUrl = message.ImageUrl;
                            existing.SendStatus = message.SendStatus;
                            existing.SenderAvatar = message.SenderAvatar ?? existing.SenderAvatar;
                            existing.ShowSenderName = message.ShowSenderName;
                            existing.ShowTimeStamp = message.ShowTimeStamp;
                            existing.ReplySummary = message.ReplySummary;
                            existing.ReplyTargetId = message.ReplyTargetId;
                            existing.IsPreview = message.IsPreview;
                            existing.RawMessage = string.IsNullOrEmpty(message.RawMessage) ? existing.RawMessage : message.RawMessage; // 新增: 同步 RawMessage
                            existing.RichSegmentsSerialized = message.RichSegmentsSerialized ?? existing.RichSegmentsSerialized;
                            // 同步 RichSegments (若新消息有內容且舊的為空或較少)
                            try
                            {
                                if (message.RichSegments != null && message.RichSegments.Count > 0)
                                {
                                    if (existing.RichSegments == null || existing.RichSegments.Count != message.RichSegments.Count)
                                    {
                                        existing.RichSegments.Clear();
                                        foreach (var seg in message.RichSegments) existing.RichSegments.Add(seg);
                                    }
                                }
                            }
                            catch { }
                            return Task.FromResult(0); // 已更新
                        }
                    }
                    // 新增
                    list.Add(message);
                    if (list.Count > _messageMemoryLimitPerChat) list.RemoveRange(0, list.Count - _messageMemoryLimitPerChat);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"CacheMessageAsync 失敗: {ex.Message}"); }
            return Task.FromResult(0);
        }

        public Task CacheMessagesAsync(long chatId, bool isGroup, IEnumerable<MessageItem> messages)
        {
            try
            {
                if (messages == null) return Task.FromResult(0);
                var key = GetMessageCacheKey(chatId, isGroup);
                lock (_lockObject)
                {
                    List<MessageItem> list;
                    if (!_messageMemoryCache.TryGetValue(key, out list)) { list = new List<MessageItem>(); _messageMemoryCache[key] = list; }
                    foreach (var m in messages)
                    {
                        if (m == null) continue;
                        if (m.MessageId != 0)
                        {
                            var existing = list.FirstOrDefault(x => x.MessageId == m.MessageId);
                            if (existing != null)
                            {
                                existing.Content = m.Content;
                                existing.Time = m.Time;
                                existing.IsFromSelf = m.IsFromSelf;
                                existing.SenderId = m.SenderId;
                                existing.SenderName = m.SenderName;
                                existing.MessageType = m.MessageType;
                                existing.ImageUrl = m.ImageUrl;
                                existing.SendStatus = m.SendStatus;
                                existing.SenderAvatar = m.SenderAvatar ?? existing.SenderAvatar;
                                existing.ShowSenderName = m.ShowSenderName;
                                existing.ShowTimeStamp = m.ShowTimeStamp;
                                existing.ReplySummary = m.ReplySummary;
                                existing.ReplyTargetId = m.ReplyTargetId;
                                existing.IsPreview = m.IsPreview;
                                existing.RawMessage = string.IsNullOrEmpty(m.RawMessage) ? existing.RawMessage : m.RawMessage; // 新增: 同步 RawMessage
                                existing.RichSegmentsSerialized = m.RichSegmentsSerialized ?? existing.RichSegmentsSerialized;
                                try
                                {
                                    if (m.RichSegments != null && m.RichSegments.Count > 0)
                                    {
                                        if (existing.RichSegments == null || existing.RichSegments.Count != m.RichSegments.Count)
                                        {
                                            existing.RichSegments.Clear();
                                            foreach (var seg in m.RichSegments) existing.RichSegments.Add(seg);
                                        }
                                    }
                                }
                                catch { }
                                continue; // 已更新，不再新增
                            }
                        }
                        list.Add(m);
                        if (list.Count > _messageMemoryLimitPerChat) list.RemoveRange(0, list.Count - _messageMemoryLimitPerChat);
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"CacheMessagesAsync 失敗: {ex.Message}"); }
            return Task.FromResult(0);
        }

        public Task<List<MessageItem>> LoadCachedMessagesAsync(long chatId, bool isGroup, int take = 50)
        {
            try
            {
                var key = GetMessageCacheKey(chatId, isGroup);
                lock (_lockObject)
                {
                    if (_messageMemoryCache.TryGetValue(key, out var list))
                    {
                        var ordered = list.OrderBy(m => m.Time).ToList();
                        if (ordered.Count > take)
                            ordered = ordered.GetRange(ordered.Count - take, take);
                        return Task.FromResult(ordered);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadCachedMessagesAsync 失敗: {ex.Message}");
            }
            return Task.FromResult(new List<MessageItem>());
        }
        private static string GetMessageCacheKey(long chatId, bool isGroup) => $"{(isGroup ? "G" : "U")}_{chatId}";
        #endregion
    }
}