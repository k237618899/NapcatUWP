using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Platform;
using Newtonsoft.Json;
using System.IO; // 新增: 路徑組合
using System.ComponentModel; // 新增: 監聽屬性變更

namespace AnnaMessager.UWP.Services
{
    /// <summary>
    ///     UWP 平台基於 SQLite 的緩存管理服務實現
    /// </summary>
    public class UwpCacheManager : ICacheManager
    {
        private readonly IPlatformDatabaseService _databaseService;
        private bool _isInitialized;
        // 新增: 緩存資料庫路徑避免在背景執行緒重複存取 UWP API 導致 RPC_E_WRONG_THREAD
        private readonly string _dbPath;
        // 新增: 已訂閱頭像更新的聊天集合，避免重複訂閱
        private readonly HashSet<long> _subscribedChatItems = new HashSet<long>();

        public UwpCacheManager()
        {
            _databaseService = Mvx.Resolve<IPlatformDatabaseService>();
            try
            {
#if WINDOWS_UWP
                _dbPath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "AnnaMessager.db");
#else
                _dbPath = "AnnaMessager.db";
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine("初始化資料庫路徑失敗: " + ex.Message);
                _dbPath = "AnnaMessager.db"; // 後備
            }
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _databaseService.InitializeDatabaseAsync();
                _isInitialized = true;
                Debug.WriteLine("UwpCacheManager 初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UwpCacheManager 初始化失敗: {ex.Message}");
            }
        }

        #region 聊天緩存

        public async Task CacheChatItemAsync(ChatItem chatItem)
        {
            try
            {
                await EnsureInitializedAsync();

                var chatCacheEntity = ConvertToChatCacheEntity(chatItem);
                await _databaseService.SaveChatCacheAsync(chatCacheEntity);

                // 訂閱頭像後續載入（僅一次）
                TrySubscribeChatAvatarUpdates(chatItem);

                Debug.WriteLine($"聊天項目已緩存: {chatItem.Name} ({chatItem.ChatId})");
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
                await EnsureInitializedAsync();

                var chatList = chatItems?.ToList() ?? new List<ChatItem>();
                foreach (var chatItem in chatList)
                {
                    var chatCacheEntity = ConvertToChatCacheEntity(chatItem);
                    await _databaseService.SaveChatCacheAsync(chatCacheEntity);
                    TrySubscribeChatAvatarUpdates(chatItem);
                }

                Debug.WriteLine($"批量緩存聊天項目完成，數量: {chatList.Count}");
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
                await EnsureInitializedAsync();

                var cachedChats = await _databaseService.GetAllChatCacheAsync();
                var chatItems = new List<ChatItem>();

                foreach (var cached in cachedChats)
                {
                    var chatItem = ConvertToChatItem(cached);
                    if (chatItem != null)
                    {
                        chatItems.Add(chatItem);
                        // 重新訂閱（載入時若未有頭像，後續載入仍可更新資料庫）
                        TrySubscribeChatAvatarUpdates(chatItem);
                    }
                }

                Debug.WriteLine($"載入緩存聊天項目完成，數量: {chatItems.Count}");
                return chatItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存聊天項目失敗: {ex.Message}");
                return new List<ChatItem>();
            }
        }

        // 新增: 訂閱 ChatItem 屬性變更（主要監聽頭像載入完成）
        private void TrySubscribeChatAvatarUpdates(ChatItem chatItem)
        {
            try
            {
                if (chatItem == null || chatItem.ChatId <= 0) return;
                if (_subscribedChatItems.Contains(chatItem.ChatId)) return;

                chatItem.PropertyChanged += OnChatItemPropertyChanged;
                _subscribedChatItems.Add(chatItem.ChatId);
                Debug.WriteLine($"已訂閱聊天頭像更新: {chatItem.ChatId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"訂閱聊天頭像更新失敗: {ex.Message}");
            }
        }

        // 監聽屬性變更，當 Avatar / AvatarUrl 有值時更新資料庫緩存
        private async void OnChatItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (sender is ChatItem chatItem)
                {
                    if (e.PropertyName == nameof(ChatItem.Avatar) || e.PropertyName == nameof(ChatItem.AvatarUrl))
                    {
                        if (!string.IsNullOrEmpty(chatItem.Avatar))
                        {
                            Debug.WriteLine($"偵測到頭像更新，寫回 ChatCache: {chatItem.ChatId}");
                            var entity = ConvertToChatCacheEntity(chatItem);
                            _ = _databaseService.SaveChatCacheAsync(entity); // 忽略等待，避免阻塞 UI
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理聊天頭像更新事件失敗: {ex.Message}");
            }
        }

        #endregion

        #region 聯絡人緩存

        public async Task CacheContactItemAsync(ContactItem contactItem)
        {
            try
            {
                await EnsureInitializedAsync();

                var contactCacheEntity = ConvertToContactCacheEntity(contactItem);
                await _databaseService.SaveContactCacheAsync(contactCacheEntity);

                Debug.WriteLine($"聯絡人項目已緩存: {contactItem.Nickname} ({contactItem.UserId})");
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
                await EnsureInitializedAsync();

                var cachedContacts = await _databaseService.GetAllContactCacheAsync();
                var contactItems = new List<ContactItem>();

                foreach (var cached in cachedContacts)
                {
                    var contactItem = ConvertToContactItem(cached);
                    if (contactItem != null) contactItems.Add(contactItem);
                }

                Debug.WriteLine($"載入緩存聯絡人項目完成，數量: {contactItems.Count}");
                return contactItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存聯絡人項目失敗: {ex.Message}");
                return new List<ContactItem>();
            }
        }

        #endregion

        #region 群組緩存

        public async Task CacheGroupItemAsync(GroupItem groupItem)
        {
            try
            {
                await EnsureInitializedAsync();

                var groupCacheEntity = ConvertToGroupCacheEntity(groupItem);
                await _databaseService.SaveGroupCacheAsync(groupCacheEntity);

                Debug.WriteLine($"群組項目已緩存: {groupItem.GroupName} ({groupItem.GroupId})");
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
                await EnsureInitializedAsync();

                var cachedGroups = await _databaseService.GetAllGroupCacheAsync();
                var groupItems = new List<GroupItem>();

                foreach (var cached in cachedGroups)
                {
                    var groupItem = ConvertToGroupItem(cached);
                    if (groupItem != null) groupItems.Add(groupItem);
                }

                Debug.WriteLine($"載入緩存群組項目完成，數量: {groupItems.Count}");
                return groupItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存群組項目失敗: {ex.Message}");
                return new List<GroupItem>();
            }
        }

        #endregion

        #region 緩存管理

        public async Task DeleteChatCacheAsync(long chatId, bool isGroup)
        {
            try
            {
                await EnsureInitializedAsync();
                await _databaseService.DeleteChatCacheAsync(chatId, isGroup);
                Debug.WriteLine($"聊天緩存已刪除: {chatId} (群組: {isGroup})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除聊天緩存失敗: {ex.Message}");
            }
        }

        public async Task<CacheInfo> CalculateCacheSizeAsync()
        {
            try
            {
                await EnsureInitializedAsync();

                var databaseSize = await _databaseService.GetDatabaseSizeAsync();
                var chatCount = await _databaseService.GetCachedItemsCountAsync("ChatCache");
                var contactCount = await _databaseService.GetCachedItemsCountAsync("ContactCache");
                var groupCount = await _databaseService.GetCachedItemsCountAsync("GroupCache");

                return new CacheInfo
                {
                    TotalSize = databaseSize,
                    TotalMessages = chatCount + contactCount + groupCount,
                    ImagesCacheSize = 0, // 圖片緩存由其他服務管理
                    MessagesCacheSize = databaseSize
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"計算緩存大小失敗: {ex.Message}");
                return new CacheInfo
                {
                    TotalSize = 0,
                    TotalMessages = 0,
                    ImagesCacheSize = 0,
                    MessagesCacheSize = 0
                };
            }
        }

        public async Task ClearAllCacheAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                await _databaseService.ClearChatCacheAsync();
                await _databaseService.ClearContactCacheAsync();
                await _databaseService.ClearGroupCacheAsync();
                Debug.WriteLine("所有緩存已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除所有緩存失敗: {ex.Message}");
                throw;
            }
        }

        public async Task ClearExpiredCacheAsync()
        {
            try
            {
                await EnsureInitializedAsync();

                // 清除7天前的緩存
                var expireTime = DateTime.Now.AddDays(-7);
                await _databaseService.ClearExpiredCacheAsync(expireTime);
                Debug.WriteLine("過期緩存已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除過期緩存失敗: {ex.Message}");
                throw;
            }
        }

        public async Task ClearCacheAsync(CacheType cacheType)
        {
            try
            {
                await EnsureInitializedAsync();

                switch (cacheType)
                {
                    case CacheType.Chats:
                        await _databaseService.ClearChatCacheAsync();
                        break;
                    case CacheType.Contacts:
                        await _databaseService.ClearContactCacheAsync();
                        break;
                    case CacheType.Groups:
                        await _databaseService.ClearGroupCacheAsync();
                        break;
                    case CacheType.All:
                        await ClearAllCacheAsync();
                        break;
                    // 對於 Messages、Images、Avatars 類型，我們目前只清除相關的數據庫緩存
                    case CacheType.Messages:
                        await _databaseService.ClearChatCacheAsync();
                        break;
                    case CacheType.Images:
                    case CacheType.Avatars:
                        // 這些類型的緩存由其他服務管理
                        Debug.WriteLine($"緩存類型 {cacheType} 由其他服務管理");
                        break;
                }

                Debug.WriteLine($"緩存類型 {cacheType} 已清除");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除緩存類型 {cacheType} 失敗: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 新增: 消息緩存
        private const int DefaultMessageTake = 50;

        public async Task CacheMessageAsync(long chatId, bool isGroup, MessageItem message)
        {
            try
            {
                await EnsureInitializedAsync();
                await SaveMessageInternalAsync(chatId, isGroup, message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"緩存消息失敗: {ex.Message}");
            }
        }

        public async Task CacheMessagesAsync(long chatId, bool isGroup, IEnumerable<MessageItem> messages)
        {
            try
            {
                await EnsureInitializedAsync();
                foreach (var m in messages) await SaveMessageInternalAsync(chatId, isGroup, m);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"批量緩存消息失敗: {ex.Message}");
            }
        }

        public async Task<List<MessageItem>> LoadCachedMessagesAsync(long chatId, bool isGroup, int take = DefaultMessageTake)
        {
            var list = new List<MessageItem>();
            try
            {
                await EnsureInitializedAsync();
                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(@"SELECT ChatId,IsGroup,MessageId,MessageType,Content,SenderId,SenderName,Time,IsFromSelf,SendStatus,ImageUrl,FaceId,AudioFile,VideoFile,ForwardId,ExtraData FROM MessageCache WHERE ChatId=@cid AND IsGroup=@g ORDER BY Time ASC LIMIT @take", connection))
                    {
                        cmd.Parameters.AddWithValue("@cid", chatId);
                        cmd.Parameters.AddWithValue("@g", isGroup ? 1 : 0);
                        cmd.Parameters.AddWithValue("@take", take);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                try
                                {
                                    var item = new MessageItem
                                    {
                                        MessageId = reader.GetInt64(2),
                                        MessageType = ParseMessageType(reader.IsDBNull(3) ? "" : reader.GetString(3)),
                                        Content = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                        SenderId = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                                        SenderName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                        Time = DateTime.TryParse(reader.IsDBNull(7) ? null : reader.GetString(7), out var t) ? t : DateTime.Now,
                                        IsFromSelf = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
                                        SendStatus = ParseSendStatus(reader.IsDBNull(9) ? "Sent" : reader.GetString(9)),
                                        ImageUrl = reader.IsDBNull(10) ? null : reader.GetString(10),
                                        FaceId = reader.IsDBNull(11) ? null : reader.GetString(11),
                                        AudioFile = reader.IsDBNull(12) ? null : reader.GetString(12),
                                        VideoFile = reader.IsDBNull(13) ? null : reader.GetString(13),
                                        ForwardId = reader.IsDBNull(14) ? null : reader.GetString(14)
                                    };
                                    list.Add(item);
                                }
                                catch (Exception exRow)
                                {
                                    Debug.WriteLine($"解析消息緩存列失敗: {exRow.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入消息緩存失敗: {ex.Message}");
            }
            return list;
        }

        private static MessageType ParseMessageType(string t)
        {
            switch (t)
            {
                case "Image": return MessageType.Image;
                case "Voice": return MessageType.Voice;
                case "Video": return MessageType.Video;
                case "File": return MessageType.File;
                case "System": return MessageType.System;
                default: return MessageType.Text;
            }
        }
        private static MessageSendStatus ParseSendStatus(string s)
        {
            switch (s)
            {
                case "Sending": return MessageSendStatus.Sending;
                case "Failed": return MessageSendStatus.Failed;
                case "Read": return MessageSendStatus.Read;
                default: return MessageSendStatus.Sent;
            }
        }
        private async Task SaveMessageInternalAsync(long chatId, bool isGroup, MessageItem m)
        {
            try
            {
                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
                {
                    await connection.OpenAsync();
                    // 先檢查是否存在
                    using (var check = new Microsoft.Data.Sqlite.SqliteCommand("SELECT 1 FROM MessageCache WHERE ChatId=@cid AND IsGroup=@g AND MessageId=@mid LIMIT 1", connection))
                    {
                        check.Parameters.AddWithValue("@cid", chatId);
                        check.Parameters.AddWithValue("@g", isGroup ? 1 : 0);
                        check.Parameters.AddWithValue("@mid", m.MessageId);
                        var exists = await check.ExecuteScalarAsync();
                        if (exists != null) return; // 已存在不再插入
                    }
                    // 使用 UTC ISO8601，確保跨時區一致且可排序
                    var now = DateTime.UtcNow.ToString("o");
                    var msgTime = (m.Time.Kind == DateTimeKind.Utc ? m.Time : m.Time.ToUniversalTime()).ToString("o");
                    using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(@"INSERT OR IGNORE INTO MessageCache (ChatId,IsGroup,MessageId,MessageType,Content,SenderId,SenderName,Time,IsFromSelf,SendStatus,ImageUrl,FaceId,AudioFile,VideoFile,ForwardId,ExtraData,CreatedAt,UpdatedAt) VALUES (@cid,@g,@mid,@mt,@c,@sid,@sname,@time,@self,@status,@img,@face,@audio,@video,@fwd,@extra,@created,@updated)", connection))
                    {
                        cmd.Parameters.AddWithValue("@cid", chatId);
                        cmd.Parameters.AddWithValue("@g", isGroup ? 1 : 0);
                        cmd.Parameters.AddWithValue("@mid", m.MessageId);
                        cmd.Parameters.AddWithValue("@mt", m.MessageType.ToString());
                        cmd.Parameters.AddWithValue("@c", m.Content ?? "");
                        cmd.Parameters.AddWithValue("@sid", m.SenderId);
                        cmd.Parameters.AddWithValue("@sname", m.SenderName ?? "");
                        cmd.Parameters.AddWithValue("@time", msgTime);
                        cmd.Parameters.AddWithValue("@self", m.IsFromSelf ? 1 : 0);
                        cmd.Parameters.AddWithValue("@status", m.SendStatus.ToString());
                        cmd.Parameters.AddWithValue("@img", (object)m.ImageUrl ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@face", (object)m.FaceId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@audio", (object)m.AudioFile ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@video", (object)m.VideoFile ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fwd", (object)m.ForwardId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@extra", JsonConvert.SerializeObject(m));
                        cmd.Parameters.AddWithValue("@created", now);
                        cmd.Parameters.AddWithValue("@updated", now);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存消息記錄失敗: {ex.Message}");
            }
        }
        #endregion

        #region 輔助方法

        private async Task EnsureInitializedAsync()
        {
            var maxRetries = 50; // 最多等待5秒
            var retryCount = 0;

            while (!_isInitialized && retryCount < maxRetries)
            {
                await Task.Delay(100);
                retryCount++;
            }

            if (!_isInitialized) throw new InvalidOperationException("數據庫服務未完成初始化");
        }

        private static ChatCacheEntity ConvertToChatCacheEntity(ChatItem chatItem)
        {
            // 保存為 UTC ISO8601
            var lastMsgTime = (chatItem.LastMessageTime.Kind == DateTimeKind.Utc ? chatItem.LastMessageTime : chatItem.LastMessageTime.ToUniversalTime());
            return new ChatCacheEntity
            {
                ChatId = chatItem.ChatId,
                IsGroup = chatItem.IsGroup,
                Name = chatItem.Name,
                Avatar = chatItem.Avatar ?? "",
                LastMessage = chatItem.LastMessage,
                LastMessageTime = lastMsgTime,
                UnreadCount = chatItem.UnreadCount,
                LastMessageType = chatItem.LastMessageType ?? "",
                ChatData = JsonConvert.SerializeObject(chatItem),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static ChatItem ConvertToChatItem(ChatCacheEntity cached)
        {
            try
            {
                if (!string.IsNullOrEmpty(cached.ChatData))
                {
                    var chatItem = JsonConvert.DeserializeObject<ChatItem>(cached.ChatData);
                    if (chatItem != null) return chatItem;
                }

                // 如果JSON反序列化失敗，使用基本信息創建ChatItem
                return new ChatItem
                {
                    ChatId = cached.ChatId,
                    IsGroup = cached.IsGroup,
                    Name = cached.Name ?? "",
                    Avatar = cached.Avatar ?? "",
                    LastMessage = cached.LastMessage ?? "",
                    LastMessageTime = cached.LastMessageTime,
                    UnreadCount = cached.UnreadCount,
                    LastMessageType = cached.LastMessageType ?? ""
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"轉換ChatItem失敗: {ex.Message}");
                return null;
            }
        }

        private static ContactCacheEntity ConvertToContactCacheEntity(ContactItem contactItem)
        {
            return new ContactCacheEntity
            {
                UserId = contactItem.UserId,
                Nickname = contactItem.Nickname,
                Remark = contactItem.Remark,
                Avatar = contactItem.Avatar ?? "",
                Status = contactItem.Status.ToString(),
                CategoryId = contactItem.CategoryId,
                CategoryName = contactItem.CategoryName,
                ContactData = JsonConvert.SerializeObject(contactItem),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static ContactItem ConvertToContactItem(ContactCacheEntity cached)
        {
            try
            {
                if (!string.IsNullOrEmpty(cached.ContactData))
                {
                    var contactItem = JsonConvert.DeserializeObject<ContactItem>(cached.ContactData);
                    if (contactItem != null) return contactItem;
                }

                // 如果JSON反序列化失敗，使用基本信息創建ContactItem
                var status = UserStatus.Offline;
                if (Enum.TryParse<UserStatus>(cached.Status, out var parsedStatus)) status = parsedStatus;

                return new ContactItem
                {
                    UserId = cached.UserId,
                    Nickname = cached.Nickname ?? "",
                    Remark = cached.Remark ?? "",
                    Avatar = cached.Avatar ?? "",
                    Status = status,
                    CategoryId = cached.CategoryId,
                    CategoryName = cached.CategoryName
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"轉換ContactItem失敗: {ex.Message}");
                return null;
            }
        }

        private static GroupCacheEntity ConvertToGroupCacheEntity(GroupItem groupItem)
        {
            return new GroupCacheEntity
            {
                GroupId = groupItem.GroupId,
                GroupName = groupItem.GroupName,
                GroupAvatar = groupItem.GroupAvatar ?? "",
                MemberCount = groupItem.MemberCount,
                GroupData = JsonConvert.SerializeObject(groupItem),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static GroupItem ConvertToGroupItem(GroupCacheEntity cached)
        {
            try
            {
                if (!string.IsNullOrEmpty(cached.GroupData))
                {
                    var groupItem = JsonConvert.DeserializeObject<GroupItem>(cached.GroupData);
                    if (groupItem != null) return groupItem;
                }

                // 如果JSON反序列化失敗，使用基本信息創建GroupItem
                return new GroupItem
                {
                    GroupId = cached.GroupId,
                    GroupName = cached.GroupName ?? "",
                    GroupAvatar = cached.GroupAvatar ?? "",
                    MemberCount = cached.MemberCount
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"轉換GroupItem失敗: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}