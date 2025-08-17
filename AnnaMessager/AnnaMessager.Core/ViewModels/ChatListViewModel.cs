using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;
using MvvmCross.Platform.Core;

namespace AnnaMessager.Core.ViewModels
{
    public class ChatListViewModel : MvxViewModel
    {
        private readonly ICacheManager _cacheManager;
        private readonly IMvxMainThreadDispatcher _dispatcher;
        private readonly Dictionary<long, string> _friendNameCache = new Dictionary<long, string>();
        private readonly Dictionary<long, string> _groupNameCache = new Dictionary<long, string>();
        private readonly INotificationService _notificationService;
        private readonly IOneBotService _oneBotService;
        private readonly ISettingsService _settingsService;
        private AppSettings _appSettings;
        private string _cachedLoadTime;
        private ObservableCollection<ChatItem> _chatList;
        private ObservableCollection<ChatItem> _filteredChatList;
        private bool _isRefreshing;
        private List<ChatItem> _lastFilteredSnapshot = new List<ChatItem>();
        private string _searchText;
        private bool _showOnlyPinned;
        private bool _showOnlyUnread;
        private readonly object _chatListLock = new object();

        public ChatListViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;
            _cacheManager = Mvx.Resolve<ICacheManager>();
            _notificationService = Mvx.Resolve<INotificationService>(); // 修正拼寫
            _settingsService = Mvx.Resolve<ISettingsService>();
            try { _dispatcher = Mvx.Resolve<IMvxMainThreadDispatcher>(); } catch { }
            // 初始化集合與命令
            ChatList = new ObservableCollection<ChatItem>();
            FilteredChatList = new ObservableCollection<ChatItem>();
            OpenChatCommand = new MvxCommand<ChatItem>(OpenChat);
            RefreshCommand = new MvxCommand(async () => await RefreshAsync());
            SearchCommand = new MvxCommand<string>(async s => await OnSearchAsync(s));
            DeleteChatCommand = new MvxCommand<ChatItem>(async c => await DeleteChatAsync(c));
            PinChatCommand = new MvxCommand<ChatItem>(async c => await TogglePinChatAsync(c));
            MuteChatCommand = new MvxCommand<ChatItem>(async c => await ToggleMuteChatAsync(c));
            ToggleUnreadFilterCommand = new MvxCommand(async () => await ToggleUnreadFilterAsync());
            TogglePinnedFilterCommand = new MvxCommand(async () => await TogglePinnedFilterAsync());
            ContextDeleteCommand = DeleteChatCommand;
            ContextPinCommand = new MvxCommand<ChatItem>(async c => { if (c != null) await TogglePinChatAsync(c); });
            ContextMuteCommand = new MvxCommand<ChatItem>(async c => { if (c != null) await ToggleMuteChatAsync(c); });
            _oneBotService.MessageReceived += OnMessageReceived;
        }

        #region Properties / Commands
        public ObservableCollection<ChatItem> ChatList { get => _chatList; set => SetProperty(ref _chatList, value); }
        public ObservableCollection<ChatItem> FilteredChatList { get => _filteredChatList; set => SetProperty(ref _filteredChatList, value); }
        public bool IsRefreshing { get => _isRefreshing; set => SetProperty(ref _isRefreshing, value); }
        public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
        public bool ShowOnlyUnread { get => _showOnlyUnread; set => SetProperty(ref _showOnlyUnread, value); }
        public bool ShowOnlyPinned { get => _showOnlyPinned; set => SetProperty(ref _showOnlyPinned, value); }
        public ICommand OpenChatCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand DeleteChatCommand { get; }
        public ICommand PinChatCommand { get; }
        public ICommand MuteChatCommand { get; }
        public ICommand ToggleUnreadFilterCommand { get; }
        public ICommand TogglePinnedFilterCommand { get; }
        public ICommand ContextDeleteCommand { get; }
        public ICommand ContextPinCommand { get; }
        public ICommand ContextMuteCommand { get; }
        public string CachedLoadTime { get => _cachedLoadTime; set => SetProperty(ref _cachedLoadTime, value); }
        public event Action<ChatItem> ChatMovedToTop;
        #endregion

        public override async Task Initialize()
        {
            await base.Initialize();
            await LoadSettingsAsync();
            await LoadCachedDataAsync();
            _ = Task.Run(async () => await RefreshRecentChatsAsync("Init"));
            _ = Task.Run(async () => await PreloadContactsAndGroupsAsync());
            await ApplyFiltersAsync();
        }

        private bool HasActiveFilters => ShowOnlyUnread || ShowOnlyPinned || !string.IsNullOrEmpty(SearchText);

        // 將 CQ 碼轉為列表可讀摘要
        private string SummarizeMessage(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            try
            {
                // 特殊 @all
                raw = raw.Replace("[CQ:at,qq=all]", "@所有人");
                // 映射常見 CQ 類型
                var map = new KeyValuePair<string,string>[]
                {
                    new KeyValuePair<string,string>("[CQ:image","[圖片]"),
                    new KeyValuePair<string,string>("[CQ:record","[語音]"),
                    new KeyValuePair<string,string>("[CQ:video","[視頻]"),
                    new KeyValuePair<string,string>("[CQ:file","[文件]"),
                    new KeyValuePair<string,string>("[CQ:face","[表情]"),
                    new KeyValuePair<string,string>("[CQ:reply","[回覆]"),
                    new KeyValuePair<string,string>("[CQ:poke","[戳一戳]"),
                    new KeyValuePair<string,string>("[CQ:gift","[禮物]"),
                    new KeyValuePair<string,string>("[CQ:forward","[轉發]"),
                    new KeyValuePair<string,string>("[CQ:xml","[XML卡片]"),
                    new KeyValuePair<string,string>("[CQ:json","[JSON卡片]")
                };
                foreach (var kv in map)
                {
                    var k = kv.Key; var t = kv.Value;
                    if (raw.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int idx; while((idx = raw.IndexOf(k, StringComparison.OrdinalIgnoreCase)) >= 0)
                        {
                            var end = raw.IndexOf(']', idx);
                            if (end < 0) break;
                            raw = raw.Remove(idx, end - idx + 1).Insert(idx, t);
                        }
                    }
                }
                // 移除殘餘未知 CQ 碼 -> 轉成 [CQ]
                int seek = 0;
                while ((seek = raw.IndexOf("[CQ:", seek, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var end = raw.IndexOf(']', seek);
                    if (end < 0) break; // 不完整，跳出
                    raw = raw.Remove(seek, end - seek + 1).Insert(seek, "[CQ]");
                    seek += 4;
                }
                return raw.Trim();
            }
            catch { return raw; }
        }

        private async Task RefreshRecentChatsAsync(string reason = "Manual")
        {
            try
            {
                Debug.WriteLine($"[ChatList] 刷新最近聊天開始 reason={reason}");
                var recent = await _oneBotService.GetRecentContactAsync();
                if (recent?.Status != "ok" || recent.Data == null) return;

                var addedOrUpdated = false;
                foreach (var rc in recent.Data)
                {
                    if (rc == null || rc.PeerId == 0) continue;
                    var baseTime = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(rc.LastTime).ToLocalTime();
                    string resolvedName = rc.IsGroup
                        ? await ResolveGroupNameAsync(rc.PeerId, "群聊")
                        : await ResolveFriendNameAsync(rc.PeerId, "用戶" + rc.PeerId);
                    var summarized = SummarizeMessage(rc.LastMessage ?? string.Empty);
                    RunOnUI(() =>
                    {
                        lock (_chatListLock)
                        {
                            var existing = ChatList.FirstOrDefault(c => c.ChatId == rc.PeerId && c.IsGroup == rc.IsGroup);
                            if (existing == null)
                            {
                                var chat = new ChatItem
                                {
                                    ChatId = rc.PeerId,
                                    IsGroup = rc.IsGroup,
                                    Name = resolvedName,
                                    LastMessage = summarized,
                                    LastMessageTime = baseTime,
                                    LastTime = baseTime,
                                    LastMessageType = DetectMessageType(rc.LastMessage ?? string.Empty).ToString(),
                                    AvatarUrl = rc.IsGroup ? $"https://p.qlogo.cn/gh/{rc.PeerId}/{rc.PeerId}/640/" : $"https://q1.qlogo.cn/g?b=qq&nk={rc.PeerId}&s=640"
                                };
                                ChatList.Add(chat);
                                if (!HasActiveFilters) IncrementalInsertIntoFilteredList(chat);
                                Task.Run(() => _cacheManager.CacheChatItemAsync(chat));
                                addedOrUpdated = true;
                            }
                            else if (baseTime > existing.LastMessageTime)
                            {
                                existing.LastMessage = summarized;
                                existing.LastMessageTime = baseTime;
                                existing.LastTime = baseTime;
                                existing.LastMessageType = DetectMessageType(rc.LastMessage ?? existing.LastMessage).ToString();
                                Task.Run(() => _cacheManager.CacheChatItemAsync(existing));
                                ReorderChat(existing); // 差量重排
                                addedOrUpdated = true;
                            }
                        }
                    });
                }
                if (addedOrUpdated && HasActiveFilters)
                {
                    await ApplyFiltersAsync();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[ChatList] 刷新最近聊天失敗: {ex.Message}"); }
        }

        private async void OnMessageReceived(object sender, MessageEventArgs e)
        {
            if (e?.Message == null) return; // 修正語法
            try
            {
                var isGroup = e.Message.GroupId.HasValue;
                var chatId = isGroup ? e.Message.GroupId.Value : e.Message.UserId;
                if (chatId == 0) return;
                var textSummaryRaw = e.Message.Message;
                var summarized = SummarizeMessage(textSummaryRaw);
                var serverTime = e.Message.DateTime == default ? DateTime.Now : e.Message.DateTime;
                var fromSelf = e.Message.Sender?.UserId == e.Message.SelfId;
                var msgType = DetectMessageType(textSummaryRaw);

                // 快取消息 (補充圖片 URL 抽取)
                try
                {
                    var imageUrl = ExtractFirst(textSummaryRaw, "image", "url") ?? ExtractFirst(textSummaryRaw, "image", "file");
                    var messageItem = new MessageItem
                    {
                        MessageId = e.Message.MessageId != 0 ? e.Message.MessageId : DateTime.Now.Ticks,
                        Content = summarized,
                        Time = serverTime,
                        IsFromSelf = fromSelf,
                        SenderId = e.Message.Sender?.UserId ?? (isGroup ? 0 : chatId),
                        SenderName = e.Message.Sender?.Nickname ?? e.Message.Sender?.Card ?? (isGroup ? "成員" : summarized),
                        MessageType = msgType,
                        ImageUrl = imageUrl,
                        SendStatus = MessageSendStatus.Sent
                    };
                    _ = _cacheManager.CacheMessageAsync(chatId, isGroup, messageItem);
                }
                catch (Exception cacheMsgEx) { Debug.WriteLine("[ChatList] 緩存消息失敗: " + cacheMsgEx.Message); }

                ChatItem target = null;
                var created = false;
                RunOnUI(() =>
                {
                    lock (_chatListLock)
                    {
                        target = ChatList.FirstOrDefault(c => c.ChatId == chatId && c.IsGroup == isGroup);
                        if (target == null)
                        {
                            target = new ChatItem
                            {
                                ChatId = chatId,
                                IsGroup = isGroup,
                                Name = isGroup ? "群聊" : (e.Message.Sender?.Nickname ?? ("用戶" + chatId)),
                                LastMessage = summarized,
                                LastMessageTime = serverTime,
                                LastTime = serverTime,
                                LastMessageType = msgType.ToString(),
                                UnreadCount = fromSelf ? 0 : 1,
                                AvatarUrl = isGroup ? $"https://p.qlogo.cn/gh/{chatId}/{chatId}/640/" : $"https://q1.qlogo.cn/g?b=qq&nk={chatId}&s=640",
                                IsRecentlyUpdated = true
                            };
                            ChatList.Add(target);
                            created = true;
                        }
                        else
                        {
                            target.LastMessage = summarized;
                            target.LastMessageTime = serverTime;
                            target.LastTime = serverTime;
                            target.LastMessageType = msgType.ToString();
                            if (!fromSelf) target.UnreadCount++;
                            target.IsRecentlyUpdated = true;
                        }
                        // 強制移動到第一位 (動畫由 ListView 自帶)
                        var idx = ChatList.IndexOf(target);
                        if (idx > 0)
                        {
                            ChatList.Move(idx, 0);
                            // 同步 Filtered (若無過濾)
                            if (!HasActiveFilters)
                            {
                                var fIdx = FilteredChatList.IndexOf(target);
                                if (fIdx > 0) FilteredChatList.Move(fIdx, 0);
                            }
                            ChatMovedToTop?.Invoke(target);
                        }
                    }
                });

                if (target != null)
                {
                    // 解析真實名稱 (群組 / 好友)
                    if (!target.IsGroup) target.Name = await ResolveFriendNameAsync(chatId, target.Name);
                    else target.Name = await ResolveGroupNameAsync(chatId, target.Name);
                    // 立即快取 (新建 / 名稱更新)
                    _ = _cacheManager.CacheChatItemAsync(target);
                    if (!fromSelf) await ShowMessageNotificationAsync(target, e.Message, target.Name);
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        RunOnUI(() => { if (target != null) target.IsRecentlyUpdated = false; });
                    });
                }
            }
            catch (Exception ex) { Debug.WriteLine("處理新消息失敗: " + ex.Message); }
        }

        private string ExtractFirst(string raw, string tag, string key)
        {
            try
            {
                if (string.IsNullOrEmpty(raw)) return null;
                var start = raw.IndexOf("[CQ:" + tag, StringComparison.OrdinalIgnoreCase);
                if (start < 0) return null;
                var end = raw.IndexOf(']', start);
                if (end < 0) return null;
                var inner = raw.Substring(start, end - start);
                var parts = inner.Split(',');
                foreach (var p in parts)
                {
                    if (p.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                        return p.Substring(key.Length + 1);
                }
            }
            catch { }
            return null;
        }

        #region 其餘原有方法 (未改動功能性，僅保留)
        private async Task LoadSettingsAsync()
        {
            try { _appSettings = await _settingsService.LoadSettingsAsync(); }
            catch { _appSettings = new AppSettings { EnableNotifications = true }; }
        }

        private async Task LoadCachedDataAsync()
        {
            try
            {
                var cachedChats = await _cacheManager.LoadCachedChatsAsync();
                if (cachedChats?.Count > 0)
                {
                    ChatList.Clear();
                    foreach (var chat in cachedChats.OrderByDescending(c => c.LastTime)) ChatList.Add(chat);
                    CachedLoadTime = DateTime.Now.ToString("HH:mm:ss");
                }
            }
            catch (Exception ex) { Debug.WriteLine("載入緩存聊天數據失敗: " + ex.Message); }
        }

        private async Task PreloadContactsAndGroupsAsync()
        {
            try
            {
                var friends = await _oneBotService.GetFriendListAsync();
                if (friends?.Status == "ok" && friends.Data != null)
                {
                    foreach (var f in friends.Data)
                    {
                        var resolved = !string.IsNullOrEmpty(f.Remark) ? f.Remark : f.Nickname;
                        if (!_friendNameCache.ContainsKey(f.UserId)) _friendNameCache[f.UserId] = resolved;
                        var chat = ChatList.FirstOrDefault(c => !c.IsGroup && c.ChatId == f.UserId);
                        if (chat != null && chat.Name != resolved)
                        {
                            chat.Name = resolved;
                            await _cacheManager.CacheChatItemAsync(chat);
                        }
                    }
                }
                var groups = await _oneBotService.GetGroupListAsync();
                if (groups?.Status == "ok" && groups.Data != null)
                {
                    foreach (var g in groups.Data)
                    {
                        if (!_groupNameCache.ContainsKey(g.GroupId)) _groupNameCache[g.GroupId] = g.GroupName;
                        var chat = ChatList.FirstOrDefault(c => c.IsGroup && c.ChatId == g.GroupId);
                        if (chat != null && chat.Name != g.GroupName)
                        {
                            chat.Name = g.GroupName;
                            await _cacheManager.CacheChatItemAsync(chat);
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("預載名稱失敗: " + ex.Message); }
        }

        private async Task OnSearchAsync(string searchText) { SearchText = searchText; await ApplyFiltersAsync(); }

        private async Task ApplyFiltersAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    List<ChatItem> list;
                    lock (_chatListLock) list = ChatList.ToList();
                    if (!string.IsNullOrEmpty(SearchText))
                        list = list.Where(c => (c.Name ?? "").Contains(SearchText) || (c.LastMessage ?? "").Contains(SearchText)).ToList();
                    if (ShowOnlyUnread) list = list.Where(c => c.UnreadCount > 0).ToList();
                    if (ShowOnlyPinned) list = list.Where(c => c.IsPinned).ToList();
                    list = list.OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.LastTime).ToList();
                    RunOnUI(() =>
                    {
                        if (SequenceEqual(_lastFilteredSnapshot, list)) return;
                        IncrementalUpdateFilteredChatList(list);
                        _lastFilteredSnapshot = list.ToList();
                    });
                });
            }
            catch (Exception ex) { Debug.WriteLine("應用過濾器失敗: " + ex.Message); }
        }

        private bool SequenceEqual(List<ChatItem> a, List<ChatItem> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!ReferenceEquals(a[i], b[i])) return false; return true;
        }

        private async Task ToggleUnreadFilterAsync() { ShowOnlyUnread = !ShowOnlyUnread; await ApplyFiltersAsync(); }
        private async Task TogglePinnedFilterAsync() { ShowOnlyPinned = !ShowOnlyPinned; await ApplyFiltersAsync(); }

        public void AddOrUpdateChat(long chatId, bool isGroup, string name)
        {
            if (chatId == 0) return;
            ChatItem target = null;
            lock (_chatListLock)
            {
                target = ChatList.FirstOrDefault(c => c.ChatId == chatId && c.IsGroup == isGroup);
                if (target == null)
                {
                    target = new ChatItem
                    {
                        ChatId = chatId,
                        IsGroup = isGroup,
                        Name = name,
                        LastMessage = SummarizeMessage("點擊開始聊天"),
                        LastMessageTime = DateTime.Now,
                        LastTime = DateTime.Now,
                        AvatarUrl = isGroup ? $"https://p.qlogo.cn/gh/{chatId}/{chatId}/640/" : $"https://q1.qlogo.cn/g?b=qq&nk={chatId}&s=640"
                    };
                    ChatList.Insert(0, target);
                    if (!HasActiveFilters) IncrementalInsertIntoFilteredList(target);
                }
                else
                {
                    if (!string.IsNullOrEmpty(name) && target.Name != name) target.Name = name;
                }
                // 保證在頂部
                var idx = ChatList.IndexOf(target);
                if (idx > 0)
                {
                    ChatList.Move(idx, 0);
                    if (!HasActiveFilters)
                    {
                        var fIdx = FilteredChatList.IndexOf(target);
                        if (fIdx > 0) FilteredChatList.Move(fIdx, 0);
                    }
                    ChatMovedToTop?.Invoke(target);
                }
            }
            if (target != null) Task.Run(() => _cacheManager.CacheChatItemAsync(target));
        }

        private void OpenChat(ChatItem chatItem)
        {
            if (chatItem == null) return;
            Task.Run(async () => { try { await _notificationService.ClearChatNotificationsAsync(chatItem.ChatId, chatItem.IsGroup); } catch { } });
            chatItem.UnreadCount = 0;
            // 統一使用 ChatViewModel (舊 ChatRoomViewModel 已廢棄)
            ShowViewModel<ChatViewModel>(new { chatId = chatItem.ChatId, isGroup = chatItem.IsGroup, chatName = chatItem.Name });
        }

        private async Task RefreshAsync() { await RefreshRecentChatsAsync("RefreshCommand"); }
        private async Task DeleteChatAsync(ChatItem chatItem)
        {
            if (chatItem == null) return;
            ChatList.Remove(chatItem); FilteredChatList.Remove(chatItem); await _cacheManager.DeleteChatCacheAsync(chatItem.ChatId, chatItem.IsGroup);
        }
        private async Task TogglePinChatAsync(ChatItem chatItem)
        { if (chatItem == null) return; chatItem.IsPinned = !chatItem.IsPinned; chatItem.LastTime = DateTime.Now; await _cacheManager.CacheChatItemAsync(chatItem); ReorderChat(chatItem); }
        private async Task ToggleMuteChatAsync(ChatItem chatItem) { if (chatItem == null) return; chatItem.IsMuted = !chatItem.IsMuted; await _cacheManager.CacheChatItemAsync(chatItem); }

        private void ReorderChat(ChatItem chat)
        {
            if (chat == null) return;
            // 使用時間 + 置頂重新排序
            var ordered = ChatList
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.LastTime)
                .ToList();
            int targetIndex = ordered.IndexOf(chat);
            int currentIndex = ChatList.IndexOf(chat);
            if (targetIndex >= 0 && currentIndex >= 0 && targetIndex != currentIndex)
                ChatList.Move(currentIndex, targetIndex);
            if (!HasActiveFilters)
            {
                // 同步 FilteredChatList
                if (!FilteredChatList.Contains(chat)) IncrementalInsertIntoFilteredList(chat);
                else
                {
                    var fCurrent = FilteredChatList.IndexOf(chat);
                    var fordered = FilteredChatList.OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.LastTime).ToList();
                    var fTarget = fordered.IndexOf(chat);
                    if (fCurrent >= 0 && fTarget >= 0 && fCurrent != fTarget)
                        FilteredChatList.Move(fCurrent, fTarget);
                }
            }
        }

        private void IncrementalUpdateFilteredChatList(List<ChatItem> newOrder)
        {
            for (int i = FilteredChatList.Count - 1; i >= 0; i--)
                if (!newOrder.Contains(FilteredChatList[i])) FilteredChatList.RemoveAt(i);
            for (int t = 0; t < newOrder.Count; t++)
            {
                var item = newOrder[t];
                var cur = FilteredChatList.IndexOf(item);
                if (cur == -1) FilteredChatList.Insert(t, item);
                else if (cur != t) FilteredChatList.Move(cur, t);
            }
        }

        private void IncrementalInsertIntoFilteredList(ChatItem chat)
        {
            if (chat == null) return;
            if (FilteredChatList.Contains(chat)) return;
            var ordered = ChatList.OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.LastTime).ToList();
            var idx = ordered.IndexOf(chat);
            if (idx < 0 || idx >= FilteredChatList.Count) FilteredChatList.Add(chat); else FilteredChatList.Insert(idx, chat);
        }

        private void RunOnUI(Action action)
        {
            if (action == null) return;
            if (_dispatcher != null && _dispatcher.RequestMainThreadAction(action)) return;
            action();
        }

        private MessageType DetectMessageType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return MessageType.Text;
            if (raw.Contains("[CQ:image")) return MessageType.Image;
            if (raw.Contains("[CQ:record")) return MessageType.Voice;
            if (raw.Contains("[CQ:video")) return MessageType.Video;
            if (raw.Contains("[CQ:file")) return MessageType.File;
            return MessageType.Text;
        }

        private async Task ShowMessageNotificationAsync(ChatItem chatItem, MessageEvent message, string senderName)
        {
            try
            {
                if (_appSettings?.EnableNotifications != true) return;
                if (chatItem.IsMuted) return;
                var info = new NotificationInfo
                {
                    Title = chatItem.IsGroup ? $"群聊: {chatItem.Name}" : chatItem.Name,
                    Message = message.Message,
                    SenderName = senderName,
                    ChatId = chatItem.ChatId,
                    IsGroup = chatItem.IsGroup,
                    Time = message.DateTime,
                    ChatName = chatItem.Name
                };
                await _notificationService.ShowMessageNotificationAsync(info);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("顯示消息通知失敗: " + ex.Message);
            }
        }
        #endregion // 補上區域結束

        // 重新補上名稱解析（被截斷後遺失）
        private async Task<string> ResolveFriendNameAsync(long userId, string currentName)
        {
            try
            {
                string cached;
                if (_friendNameCache.TryGetValue(userId, out cached)) return cached;
                if (!string.IsNullOrEmpty(currentName) && currentName != "群聊" && !currentName.StartsWith("用戶")) return currentName;
                var cats = await _oneBotService.GetFriendsWithCategoryAsync();
                if (cats != null && cats.Status == "ok" && cats.Data != null)
                {
                    foreach (var cat in cats.Data)
                    {
                        if (cat.Friends == null) continue;
                        var f = cat.Friends.FirstOrDefault(x => x.UserId == userId);
                        if (f != null)
                        {
                            var resolved = !string.IsNullOrEmpty(f.Remark) ? f.Remark : f.Nickname;
                            _friendNameCache[userId] = resolved; return resolved;
                        }
                    }
                }
                var stranger = await _oneBotService.GetStrangerInfoAsync(userId, true);
                if (stranger != null && stranger.Status == "ok" && stranger.Data != null)
                { _friendNameCache[userId] = stranger.Data.Nickname; return stranger.Data.Nickname; }
            }
            catch { }
            return currentName;
        }

        private async Task<string> ResolveGroupNameAsync(long groupId, string currentName)
        {
            try
            {
                string cached;
                if (_groupNameCache.TryGetValue(groupId, out cached)) return cached;
                if (!string.IsNullOrEmpty(currentName) && currentName != "群聊") return currentName;
                var info = await _oneBotService.GetGroupInfoAsync(groupId, true);
                if (info != null && info.Status == "ok" && info.Data != null && !string.IsNullOrEmpty(info.Data.GroupName))
                { _groupNameCache[groupId] = info.Data.GroupName; return info.Data.GroupName; }
            }
            catch { }
            return currentName;
        }
    }
}