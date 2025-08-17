using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;
using Newtonsoft.Json.Linq;

namespace AnnaMessager.Core.ViewModels
{
    public class MediaUploadPayload
    {
        public string FileName { get; set; }
        public string Base64Data { get; set; }
        public MessageType MediaType { get; set; }
    }

    /// <summary>
    ///     聊天界面 ViewModel - IM 核心功能 (重寫: 使用統一歷史 API, 加入自發消息處理, 修復命令狀態)
    /// </summary>
    public class ChatViewModel : MvxViewModel, IDisposable
    {
        private readonly ICacheManager _cacheManager;
        private readonly INotificationService _notificationService;
        private readonly IOneBotService _oneBotService;
        private readonly IAvatarCacheService _avatarCacheService; // 新增: 頭像快取

        private string _chatAvatar;
        private long _chatId;
        private string _chatName;
        private bool _disposed;
        private string _inputMessage;
        private bool _isGroup;
        private bool _isLoading;
        private bool _isOnline;
        private bool _isSending;
        private int _memberCount;
        private ObservableCollection<MessageItem> _messages;
        private long _selfUserId; // 自己的帳號
        private long _oldestMessageSeqLoaded; // 當前已載入的最舊 message_seq (翻頁用)
        private bool _hasMoreHistory = true; // 是否還有更早消息
        private bool _isUploadingMedia;
        private bool _initialLoadFinished; // 是否已作初始資料載入（避免重複）
        private bool _pendingConnectionLoad; // 標記等待連線後再載入
        public bool IsUploadingMedia { get => _isUploadingMedia; set { if (SetProperty(ref _isUploadingMedia, value)) { RaisePropertyChanged(() => CanSendMedia); UpdateMediaCommandsCanExecute(); } } }
        public bool CanSendMedia => !_isUploadingMedia && _oneBotService.IsConnected;

        private MvvmCross.Platform.Core.IMvxMainThreadDispatcher _uiDispatcher; // 新增主執行緒調度

        private HashSet<long> _fetchingMessageIds = new HashSet<long>();
        private readonly HashSet<long> _messageIdSet = new HashSet<long>(); // 全域去重集合

        private long _scrollToMessageRequestId;
        public long ScrollToMessageRequestId { get => _scrollToMessageRequestId; private set { if (SetProperty(ref _scrollToMessageRequestId, value)) { } } }
        public void RequestScrollToMessage(long id) { if (id > 0) ScrollToMessageRequestId = id; }

        public ChatViewModel()
        {
            _oneBotService = Mvx.Resolve<IOneBotService>();
            _cacheManager = Mvx.Resolve<ICacheManager>();
            _notificationService = Mvx.Resolve<INotificationService>();
            try { _avatarCacheService = Mvx.Resolve<IAvatarCacheService>(); } catch { }

            Messages = new ObservableCollection<MessageItem>();

            SendMessageCommand = new MvxCommand(async () => await SendMessageAsync(), () => CanSendMessage);
            LoadMoreMessagesCommand = new MvxCommand(async () => await LoadMoreMessagesAsync());
            SelectMessageCommand = new MvxCommand<MessageItem>(SelectMessage);
            DeleteMessageCommand = new MvxCommand<MessageItem>(async msg => await DeleteMessageAsync(msg));
            CopyMessageCommand = new MvxCommand<MessageItem>(CopyMessage);
            ResendMessageCommand = new MvxCommand<MessageItem>(async msg => await ResendMessageAsync(msg));
            UploadImageCommand = new MvxCommand<object>(async p => await SendImageAsync(p as MediaUploadPayload), p => CanSendMedia);
            UploadVoiceCommand = new MvxCommand<object>(async p => await SendVoiceAsync(p as MediaUploadPayload), p => CanSendMedia);
            UploadVideoCommand = new MvxCommand<object>(async p => await SendVideoAsync(p as MediaUploadPayload), p => CanSendMedia);

            _oneBotService.MessageReceived += OnMessageReceived;
            _oneBotService.ConnectionStatusChanged += OnConnectionStatusChanged;
            try { _uiDispatcher = Mvx.Resolve<MvvmCross.Platform.Core.IMvxMainThreadDispatcher>(); } catch { }
        }

        private void RunOnUI(Action action)
        {
            if (action == null) return;
            if (_uiDispatcher != null)
            {
                if (!_uiDispatcher.RequestMainThreadAction(action)) action();
            }
            else action();
        }
        private void AddMessageUI(MessageItem item)
        {
            if (item == null) return;
            RunOnUI(() => Messages.Add(item));
        }

        public void Init(long chatId, bool isGroup, string chatName)
        {
            _chatId = chatId;
            _isGroup = isGroup;
            _chatName = chatName;
            Debug.WriteLine($"[ChatViewModel] Init chatId={chatId} isGroup={isGroup} name={chatName} connected={_oneBotService.IsConnected}");
            RaisePropertyChanged(() => ChatTitle);
            RaisePropertyChanged(() => OnlineStatus);
            RaisePropertyChanged(() => GroupInfo);
            // 如果此時已經連線完成，且 Initialize 尚未或尚未載入資料，立即啟動初始載入 (避免歷史消息不載入)
            if (_oneBotService.IsConnected && !_initialLoadFinished)
            {
                Debug.WriteLine("[ChatViewModel] Init 遇到已連線狀態 -> 立即啟動初始載入");
                _ = Task.Run(() => PerformInitialDataLoadAsync("InitConnected"));
            }
            else if (!_oneBotService.IsConnected)
            {
                _pendingConnectionLoad = true; // 等待連線後載入
            }
        }

        private void OnConnectionStatusChanged(object sender, bool e)
        {
            Debug.WriteLine($"[ChatViewModel] ConnectionStatusChanged connected={e} ChatId={ChatId}");
            if (e && _pendingConnectionLoad && !_initialLoadFinished)
            {
                _ = Task.Run(async () => await PerformInitialDataLoadAsync("ConnectionEstablished"));
            }
        }

        #region Event Handlers

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            if (e?.Message == null) return;

            Action addAction = () =>
            {
                try
                {
                    var belongs = IsGroup ? (e.Message.GroupId == ChatId) : (e.Message.UserId == ChatId || e.Message.Sender?.UserId == ChatId);
                    if (!belongs) return;

                    // segments 可能為 null，若為 null 則從原始 CQ 字串解析
                    List<MessageSegment> segments = e.Message.Segments;
                    MessageType parsedType = MessageType.Text; string firstImageFromCQ = null; string displayFromCQ = e.Message.Message; // fallback
                    if (segments == null || segments.Count == 0)
                    {
                        List<MessageSegment> parsedSegs; MessageType mt; string firstImg; string disp;
                        ParseCQMessage(e.Message.Message, out parsedSegs, out mt, out firstImg, out disp);
                        if (parsedSegs != null) segments = parsedSegs;
                        parsedType = mt; firstImageFromCQ = firstImg; displayFromCQ = disp;
                    }

                    var content = displayFromCQ;
                    var type = parsedType; string firstImage;
                    if (segments != null && segments.Count > 0)
                        content = BuildDisplayFromSegments(segments, out type, out firstImage);
                    else
                        firstImage = firstImageFromCQ ?? ExtractFirst(e.Message.Message, "image", "url");

                    var serverMsgId = e.Message.MessageId != 0 ? e.Message.MessageId : (long)0;
                    var isSelf = (_selfUserId != 0 && (e.Message.Sender?.UserId ?? 0) == _selfUserId);

                    // 若已存在則忽略 (避免重複)
                    if (serverMsgId != 0 && _messageIdSet.Contains(serverMsgId))
                    {
                        Debug.WriteLine($"[ChatViewModel] 收到重複消息 (忽略) id={serverMsgId}");
                        return;
                    }

                    var msg = new MessageItem
                    {
                        MessageId = serverMsgId != 0 ? serverMsgId : DateTime.Now.Ticks,
                        Content = content,
                        Time = e.Message.DateTime == default(DateTime) ? DateTime.Now : e.Message.DateTime,
                        IsFromSelf = isSelf,
                        SenderName = e.Message.Sender?.Nickname ?? e.Message.Sender?.Card ?? (IsGroup ? "未知成員" : ChatTitle),
                        SenderId = e.Message.Sender?.UserId ?? e.Message.UserId,
                        MessageType = type,
                        ImageUrl = firstImage,
                        SendStatus = MessageSendStatus.Sent
                    };

                    // NEW: 加入多媒體段到 RichSegments，否則 UI 無法判定 HasRichSegments
                    if (segments != null && segments.Count > 0)
                    {
                        foreach (var s in segments) msg.RichSegments.Add(s);
                        // Reply summary
                        var replySeg = segments.FirstOrDefault(x => string.Equals(x.Type, "reply", StringComparison.OrdinalIgnoreCase));
                        if (replySeg != null)
                        {
                            object idObj; if (replySeg.Data.TryGetValue("id", out idObj))
                            {
                                msg.ReplySummary = $"回覆 #{idObj}";
                                long idParsed; if (long.TryParse(idObj?.ToString(), out idParsed)) { msg.ReplyTargetId = idParsed; _ = EnsureReplyTargetLoadedAsync(idParsed); }
                            }
                            else msg.ReplySummary = "回覆";
                        }
                    }

                    // 自己剛發出的消息: 嘗試匹配最後一條本地 pending
                    if (msg.IsFromSelf)
                    {
                        var pending = Messages.LastOrDefault(m => m.IsFromSelf && (m.SendStatus == MessageSendStatus.Sending || m.SendStatus == MessageSendStatus.Failed || m.SendStatus == MessageSendStatus.Sent) && (DateTime.Now - m.Time).TotalSeconds < 90);
                        if (pending != null && !_messageIdSet.Contains(serverMsgId) && serverMsgId != 0)
                        {
                            // 更新原本的臨時ID為真實ID
                            pending.MessageId = serverMsgId;
                            pending.Content = msg.Content;
                            pending.ImageUrl = msg.ImageUrl ?? pending.ImageUrl;
                            pending.MessageType = msg.MessageType;
                            pending.SendStatus = MessageSendStatus.Sent;
                            if (segments != null && segments.Count > 0)
                            {
                                pending.RichSegments.Clear(); foreach (var s in segments) pending.RichSegments.Add(s);
                                var replySeg2 = segments.FirstOrDefault(x => string.Equals(x.Type, "reply", StringComparison.OrdinalIgnoreCase));
                                if (replySeg2 != null)
                                {
                                    object idObj2; if (replySeg2.Data.TryGetValue("id", out idObj2)) { pending.ReplySummary = $"回覆 #{idObj2}"; long idParsed2; if (long.TryParse(idObj2?.ToString(), out idParsed2)) { pending.ReplyTargetId = idParsed2; _ = EnsureReplyTargetLoadedAsync(idParsed2); } } else pending.ReplySummary = "回覆";
                                }
                            }
                            // 加入去重集合
                            _messageIdSet.Add(serverMsgId);
                            Debug.WriteLine($"[ChatViewModel] 自發消息回填 serverId={serverMsgId}");
                            return; // 不新增新行
                        }
                    }

                    // 設置頭像
                    msg.SenderAvatar = BuildAvatarUrl(msg.SenderId, IsGroup);
                    if (_avatarCacheService != null) _ = PrefetchAvatarAsync(msg);

                    var last = Messages.LastOrDefault();
                    if (last == null || (msg.Time - last.Time).TotalMinutes > 5) msg.ShowTimeStamp = true;
                    msg.ShowSenderName = IsGroup && !msg.IsFromSelf;

                    if (msg.MessageId != 0) _messageIdSet.Add(msg.MessageId);
                    AddMessageUI(msg);
                    _ = _cacheManager.CacheMessageAsync(ChatId, IsGroup, msg);
                    // 新增: 更新 ChatCache 最後消息類型
                    try
                    {
                        var chatSnapshot = new ChatItem
                        {
                            ChatId = ChatId,
                            IsGroup = IsGroup,
                            Name = ChatName,
                            LastMessage = msg.Content,
                            LastMessageTime = msg.Time,
                            LastTime = msg.Time,
                            LastMessageType = msg.MessageType.ToString(),
                            AvatarUrl = !string.IsNullOrEmpty(ChatAvatar) ? ChatAvatar : (IsGroup ? $"https://p.qlogo.cn/gh/{ChatId}/{ChatId}/640/" : $"https://q1.qlogo.cn/g?b=qq&nk={ChatId}&s=640")
                        };
                        _ = _cacheManager.CacheChatItemAsync(chatSnapshot);
                    }
                    catch (Exception cacheChatEx) { Debug.WriteLine("更新聊天快取 (接收) 失敗: " + cacheChatEx.Message); }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"處理接收消息失敗: {ex.Message}");
                }
            };

            if (_uiDispatcher != null && !_uiDispatcher.RequestMainThreadAction(addAction))
            {
                addAction();
            }
            else if (_uiDispatcher == null)
            {
                addAction();
            }
        }

        private string BuildAvatarUrl(long senderId, bool group)
        {
            if (senderId <= 0) return null;
            if (group)
                return $"https://q1.qlogo.cn/g?b=qq&nk={senderId}&s=100"; // 個人頭像 URL (群消息內成員)
            return $"https://q1.qlogo.cn/g?b=qq&nk={senderId}&s=100"; // 私聊頭像
        }

        private string BuildDisplayFromSegments(System.Collections.Generic.List<MessageSegment> segments, out MessageType type, out string firstImageUrl)
        {
            type = MessageType.Text;
            firstImageUrl = null;
            if (segments == null || segments.Count == 0) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (var seg in segments)
            {
                var t = (seg.Type ?? string.Empty).ToLower();
                switch (t)
                {
                    case "text":
                        var txt = seg.Data.ContainsKey("text") ? seg.Data["text"]?.ToString() : "";
                        sb.Append(txt);
                        break;
                    case "at":
                        var qq = seg.Data.ContainsKey("qq") ? seg.Data["qq"].ToString() : "";
                        sb.Append(string.IsNullOrEmpty(qq) ? "@?" : (qq == "all" ? "@所有人 " : "@" + qq + " "));
                        break;
                    case "image":
                        if (firstImageUrl == null)
                        {
                            if (seg.Data.ContainsKey("url")) firstImageUrl = seg.Data["url"].ToString();
                            else if (seg.Data.ContainsKey("file")) firstImageUrl = seg.Data["file"].ToString();
                        }
                        if (type == MessageType.Text) type = MessageType.Image;
                        sb.Append("[圖片]");
                        break;
                    case "face":
                        if (type == MessageType.Text) type = MessageType.System; // 保持顯示為普通文本
                        sb.Append("[表情]");
                        break;
                    case "record":
                        if (type == MessageType.Text) type = MessageType.Voice;
                        sb.Append("[語音]");
                        break;
                    case "video":
                        if (type == MessageType.Text) type = MessageType.Video;
                        sb.Append("[視頻]");
                        break;
                    case "reply":
                        sb.Append("[回覆]");
                        break;
                    case "forward":
                        sb.Append("[轉發]");
                        break;
                    default:
                        sb.Append('[').Append(seg.Type).Append(']');
                        break;
                }
            }
            return sb.ToString();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                try { if (_oneBotService != null) _oneBotService.MessageReceived -= OnMessageReceived; } catch (Exception ex) { Debug.WriteLine($"清理消息事件失敗: {ex.Message}"); }
                try { if (_oneBotService != null) _oneBotService.ConnectionStatusChanged -= OnConnectionStatusChanged; } catch { }
            }
            _disposed = true;
        }

        #endregion

        #region Properties

        public long ChatId { get => _chatId; set { if (SetProperty(ref _chatId, value)) { RaisePropertyChanged(() => GroupInfo); } } }
        public bool IsGroup { get => _isGroup; set { if (SetProperty(ref _isGroup, value)) { RaisePropertyChanged(() => ChatTitle); RaisePropertyChanged(() => GroupInfo); } } }
        public string ChatName { get => _chatName; set { if (SetProperty(ref _chatName, value)) { RaisePropertyChanged(() => ChatTitle); } } }
        public string InputMessage { get => _inputMessage; set { if (SetProperty(ref _inputMessage, value)) { RaisePropertyChanged(() => CanSendMessage); ((MvxCommand)SendMessageCommand).RaiseCanExecuteChanged(); } } }
        public bool CanSendMessage => !string.IsNullOrWhiteSpace(InputMessage) && !IsSending && _oneBotService.IsConnected;
        public bool IsSending { get => _isSending; set { if (SetProperty(ref _isSending, value)) { RaisePropertyChanged(() => CanSendMessage); ((MvxCommand)SendMessageCommand).RaiseCanExecuteChanged(); } } }
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public ObservableCollection<MessageItem> Messages { get => _messages; set => SetProperty(ref _messages, value); }
        public string ChatAvatar { get => _chatAvatar; set => SetProperty(ref _chatAvatar, value); }
        public int MemberCount { get => _memberCount; set { if (SetProperty(ref _memberCount, value)) { RaisePropertyChanged(() => ChatTitle); RaisePropertyChanged(() => OnlineStatus); } } }
        public bool IsOnline { get => _isOnline; set { if (SetProperty(ref _isOnline, value)) { RaisePropertyChanged(() => OnlineStatus); } } }
        public string ChatTitle => IsGroup ? $"{ChatName} ({MemberCount})" : ChatName;
        public string OnlineStatus => IsGroup ? $"{MemberCount} 位成員" : IsOnline ? "在線" : "離線";
        public string GroupInfo => IsGroup ? $"群號: {ChatId}" : string.Empty;

        #endregion

        #region Commands

        public ICommand SendMessageCommand { get; }
        public ICommand LoadMoreMessagesCommand { get; }
        public ICommand SelectMessageCommand { get; }
        public ICommand DeleteMessageCommand { get; }
        public ICommand CopyMessageCommand { get; }
        public ICommand ResendMessageCommand { get; }
        public ICommand UploadImageCommand { get; }
        public ICommand UploadVoiceCommand { get; }
        public ICommand UploadVideoCommand { get; }

        #endregion

        #region Initialization

        public override async Task Initialize()
        {
            await base.Initialize();
            Debug.WriteLine($"[ChatViewModel] Initialize start ChatId={ChatId} IsGroup={IsGroup} connected={_oneBotService.IsConnected}");

            if (!_oneBotService.IsConnected)
            {
                Debug.WriteLine("[ChatViewModel] 尚未連線，等待 ConnectionStatusChanged 事件再載入資料");
                _pendingConnectionLoad = true;
                return; // 等待事件後再載入
            }

            await PerformInitialDataLoadAsync("Initialize");
        }

        private async Task PerformInitialDataLoadAsync(string reason)
        {
            if (_initialLoadFinished) { Debug.WriteLine($"[ChatViewModel] 初始載入已完成(忽略) reason={reason}"); return; }
            _pendingConnectionLoad = false;
            try
            {
                Debug.WriteLine($"[ChatViewModel] 初始資料載入開始 reason={reason}");
                // 取得自身帳號 (用於判斷 IsFromSelf)
                try
                {
                    var login = await _oneBotService.GetLoginInfoAsync();
                    if (login?.Status == "ok" && login.Data != null) _selfUserId = login.Data.UserId;
                    Debug.WriteLine($"[ChatViewModel] 自身帳號={_selfUserId}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"取得登入資訊失敗: {ex.Message}");
                }

                await LoadChatInfoAsync();
                EnsureChatAvatar();
                await LoadCachedMessagesAsync();
                await LoadMessagesAsync();

                try { await _notificationService.ClearChatNotificationsAsync(ChatId, IsGroup); } catch (Exception ex) { Debug.WriteLine($"清除聊天通知失敗: {ex.Message}"); }
                _initialLoadFinished = true;
                Debug.WriteLine("[ChatViewModel] 初始資料載入完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatViewModel] 初始載入發生錯誤: {ex.Message}");
            }
        }

        private async Task LoadCachedMessagesAsync()
        {
            try
            {
                var cached = await _cacheManager.LoadCachedMessagesAsync(ChatId, IsGroup, 80);
                if (cached?.Count > 0)
                {
                    // 先排序與準備列表，集中一次性加入 UI 執行緒，避免 race
                    var ordered = cached.OrderBy(c => c.Time).ToList();
                    RunOnUI(() =>
                    {
                        foreach (var m in ordered)
                        {
                            if (m.MessageId != 0 && !_messageIdSet.Contains(m.MessageId)) _messageIdSet.Add(m.MessageId);
                            // 先加入集合
                            Messages.Add(m);
                        }
                        // 在 UI 執行緒重新計算標記
                        RecalculateTimestamps();
                    });
                    Debug.WriteLine($"載入本地消息快取: {cached.Count}");
                }
                else
                {
                    Debug.WriteLine("[ChatViewModel] 無本地消息快取或為空");
                }
            }
            catch (Exception ex) { Debug.WriteLine($"載入消息快取失敗: {ex.Message}"); }
        }

        private async Task LoadChatInfoAsync()
        {
            try
            {
                Debug.WriteLine($"[ChatViewModel] LoadChatInfoAsync ChatId={ChatId} IsGroup={IsGroup}");
                if (IsGroup)
                {
                    var groupInfo = await _oneBotService.GetGroupInfoAsync(ChatId);
                    Debug.WriteLine($"[ChatViewModel] 呼叫 get_group_info 結果 status={groupInfo?.Status} member?={groupInfo?.Data?.MemberCount}");
                    if (groupInfo?.Status == "ok" && groupInfo.Data != null)
                    {
                        ChatName = groupInfo.Data.GroupName;
                        MemberCount = groupInfo.Data.MemberCount;
                        IsOnline = true;
                    }
                    if (MemberCount == 0)
                    {
                        try
                        {
                            var memberListResp = await _oneBotService.GetGroupMemberListAsync(ChatId);
                            Debug.WriteLine($"[ChatViewModel] 呼叫 get_group_member_list 結果 status={memberListResp?.Status} count={memberListResp?.Data?.Count}");
                            if (memberListResp?.Status == "ok" && memberListResp.Data != null)
                                MemberCount = memberListResp.Data.Count;
                        }
                        catch (Exception ex2) { Debug.WriteLine($"取得群組成員列表失敗(後備): {ex2.Message}"); }
                    }
                }
                else
                {
                    // 私聊: 先嘗試 nc_get_user_status 再補 StrangerInfo
                    try
                    {
                        var status = await _oneBotService.GetUserStatusAsync(ChatId);
                        Debug.WriteLine($"[ChatViewModel] 呼叫 nc_get_user_status status={status?.Status}");
                        if (status?.Status == "ok" && status.Data != null)
                        {
                            var dataTok = status.Data as JToken ?? JToken.FromObject(status.Data);
                            var st = dataTok["status"]?.ToObject<int?>();
                            if (st.HasValue) IsOnline = st.Value == 10 || st.Value == 20; // 10/20 視為在線
                        }
                    }
                    catch (Exception statusEx) { Debug.WriteLine("取得使用者狀態失敗: " + statusEx.Message); }
                    var strangerInfo = await _oneBotService.GetStrangerInfoAsync(ChatId);
                    Debug.WriteLine($"[ChatViewModel] 呼叫 get_stranger_info 結果 status={strangerInfo?.Status}");
                    if (strangerInfo?.Status == "ok" && strangerInfo.Data != null)
                    {
                        if (string.IsNullOrEmpty(ChatName)) ChatName = strangerInfo.Data.Nickname;
                        if (!IsOnline) IsOnline = true; // 有資料視為在線
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"載入聊天信息失敗: {ex.Message}"); }
        }

        private void EnsureChatAvatar()
        {
            try
            {
                if (string.IsNullOrEmpty(ChatAvatar))
                {
                    ChatAvatar = IsGroup ? $"https://p.qlogo.cn/gh/{ChatId}/{ChatId}/640/" : $"https://q1.qlogo.cn/g?b=qq&nk={ChatId}&s=640";
                }
            }
            catch { }
        }

        #endregion

        #region Message Operations

        private async Task LoadMessagesAsync()
        {
            try
            {
                IsLoading = true;
                Debug.WriteLine($"[ChatViewModel] 請求歷史消息 GetMessageHistoryAsync ChatId={ChatId} IsGroup={IsGroup}");
                var historyResp = await _oneBotService.GetMessageHistoryAsync(ChatId, IsGroup);
                Debug.WriteLine($"[ChatViewModel] 歷史消息結果 status={historyResp?.Status} count={historyResp?.Data?.Messages?.Count}");
                if (historyResp?.Status == "ok" && historyResp.Data?.Messages != null)
                {
                    var ordered = historyResp.Data.Messages.OrderBy(m => m.Time).ToList();
                    DateTime? lastTime = Messages.LastOrDefault()?.Time;
                    foreach (var h in ordered)
                    {
                        if (h.MessageId != 0 && _messageIdSet.Contains(h.MessageId)) continue;
                        if (Messages.Any(x => x.MessageId == h.MessageId && h.MessageId != 0)) continue;
                        var time = h.Time == 0 ? DateTime.Now : new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(h.Time).ToLocalTime();
                        List<MessageSegment> segments = null; string firstImg = null; MessageType mt = MessageType.Text; string content = h.Message;
                        ParseCQMessage(h.Message, out segments, out mt, out firstImg, out content);
                        var item = new MessageItem
                        {
                            MessageId = h.MessageId != 0 ? h.MessageId : h.RealId != 0 ? h.RealId : DateTime.Now.Ticks,
                            Content = content,
                            Time = time,
                            IsFromSelf = (h.Sender?.UserId ?? 0) == _selfUserId,
                            SenderId = h.Sender?.UserId ?? 0,
                            SenderName = h.Sender?.Nickname ?? h.Sender?.Card ?? (IsGroup ? "成員" : ChatTitle),
                            MessageType = mt,
                            SendStatus = MessageSendStatus.Sent,
                            ImageUrl = firstImg,
                            SenderAvatar = BuildAvatarUrl(h.Sender?.UserId ?? 0, IsGroup)
                        };
                        if (_avatarCacheService != null) _ = PrefetchAvatarAsync(item);
                        if (segments != null) foreach (var s in segments) item.RichSegments.Add(s);
                        var replySeg = segments?.FirstOrDefault(x => string.Equals(x.Type, "reply", StringComparison.OrdinalIgnoreCase));
                        if (replySeg != null)
                        {
                            object idObj; if (replySeg.Data.TryGetValue("id", out idObj)) { item.ReplySummary = $"回覆 #{idObj}"; long idParsed; if (long.TryParse(idObj?.ToString(), out idParsed)) item.ReplyTargetId = idParsed; } else item.ReplySummary = "回覆";
                        }
                        if (!lastTime.HasValue || (item.Time - lastTime.Value).TotalMinutes > 5)
                        {
                            item.ShowTimeStamp = true;
                            lastTime = item.Time;
                        }
                        item.ShowSenderName = IsGroup && !item.IsFromSelf;
                        if (item.MessageId != 0) _messageIdSet.Add(item.MessageId);
                        AddMessageUI(item); // UI thread
                        await _cacheManager.CacheMessageAsync(ChatId, IsGroup, item);
                        try
                        {
                            var chatItem = new ChatItem { ChatId = ChatId, IsGroup = IsGroup, Name = ChatName, LastMessage = item.Content, LastMessageTime = item.Time, LastTime = item.Time, LastMessageType = item.MessageType.ToString(), AvatarUrl = !string.IsNullOrEmpty(ChatAvatar) ? ChatAvatar : (IsGroup ? $"https://p.qlogo.cn/gh/{ChatId}/{ChatId}/640/" : $"https://q1.qlogo.cn/g?b=qq&nk={ChatId}&s=640") };
                            _ = _cacheManager.CacheChatItemAsync(chatItem);
                        }
                        catch (Exception cacheChatEx) { Debug.WriteLine("更新聊天快取 (歷史消息載入) 失敗: " + cacheChatEx.Message); }
                    }
                    if (ordered.Count == 0) _hasMoreHistory = false; // 沒拿到代表到底
                }
                else
                {
                    _hasMoreHistory = false;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"載入消息失敗: {ex.Message}"); }
            finally { IsLoading = false; }
        }

        private async Task LoadMoreMessagesAsync()
        {
            try
            {
                if (!_hasMoreHistory || _oldestMessageSeqLoaded <= 0)
                {
                    Debug.WriteLine("沒有更多歷史或尚未初始化序列");
                    return;
                }
                IsLoading = true;
                var prevSeq = (int)_oldestMessageSeqLoaded - 1;
                if (prevSeq < 0) { _hasMoreHistory = false; IsLoading = false; return; }

                OneBotResponse<MessageHistoryResponse> resp = IsGroup
                    ? await _oneBotService.GetGroupMsgHistoryAsync(ChatId, prevSeq, 20)
                    : await _oneBotService.GetFriendMsgHistoryAsync(ChatId, prevSeq, 20);

                Debug.WriteLine($"[ChatViewModel] LoadMoreMessages 呼叫 {(IsGroup ? "get_group_msg_history" : "get_friend_msg_history")} status={resp?.Status} newCount={resp?.Data?.Messages?.Count}");

                if (resp?.Status == "ok" && resp.Data?.Messages != null && resp.Data.Messages.Count > 0)
                {
                    var ordered = resp.Data.Messages.OrderBy(m => m.Time).ToList();
                    var needInsert = new System.Collections.Generic.List<MessageItem>();
                    foreach (var h in ordered)
                    {
                        if (h.MessageId != 0 && _messageIdSet.Contains(h.MessageId)) continue;
                        if (Messages.Any(x => x.MessageId == h.MessageId && h.MessageId != 0)) continue;
                        var time = h.Time == 0 ? DateTime.Now : new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(h.Time).ToLocalTime();
                        List<MessageSegment> segments; MessageType mt; string firstImg; string display;
                        ParseCQMessage(h.Message, out segments, out mt, out firstImg, out display);
                        var item = new MessageItem
                        {
                            MessageId = h.MessageId != 0 ? h.MessageId : h.RealId != 0 ? h.RealId : DateTime.Now.Ticks,
                            Content = display,
                            Time = time,
                            IsFromSelf = (h.Sender?.UserId ?? 0) == _selfUserId,
                            SenderId = h.Sender?.UserId ?? 0,
                            SenderName = h.Sender?.Nickname ?? h.Sender?.Card ?? (IsGroup ? "成員" : ChatTitle),
                            MessageType = mt,
                            SendStatus = MessageSendStatus.Sent,
                            ImageUrl = firstImg,
                            SenderAvatar = BuildAvatarUrl(h.Sender?.UserId ?? 0, IsGroup)
                        };
                        if (_avatarCacheService != null) _ = PrefetchAvatarAsync(item);
                        if (segments != null) foreach (var s in segments) item.RichSegments.Add(s);
                        var replySeg = segments?.FirstOrDefault(x => string.Equals(x.Type, "reply", StringComparison.OrdinalIgnoreCase));
                        if (replySeg != null)
                        {
                            object idObj; if (replySeg.Data.TryGetValue("id", out idObj)) { item.ReplySummary = $"回覆 #{idObj}"; long idParsed; if (long.TryParse(idObj?.ToString(), out idParsed)) item.ReplyTargetId = idParsed; } else item.ReplySummary = "回覆";
                        }
                        item.ShowSenderName = IsGroup && !item.IsFromSelf;
                        if (item.MessageId != 0) _messageIdSet.Add(item.MessageId);
                        needInsert.Add(item);
                        if (_oldestMessageSeqLoaded == 0 || h.MessageSeq < _oldestMessageSeqLoaded) _oldestMessageSeqLoaded = h.MessageSeq;
                    }
                    // 插入需在 UI 執行緒
                    RunOnUI(() =>
                    {
                        for (int i = needInsert.Count - 1; i >= 0; i--)
                        {
                            Messages.Insert(0, needInsert[i]);
                        }
                        RecalculateTimestamps();
                    });
                    foreach (var msg in needInsert) await _cacheManager.CacheMessageAsync(ChatId, IsGroup, msg);
                }
                else
                {
                    _hasMoreHistory = false;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"載入更多消息失敗: {ex.Message}"); }
            finally { IsLoading = false; }
        }

        private void RecalculateTimestamps()
        {
            DateTime? last = null;
            foreach (var m in Messages.OrderBy(x => x.Time))
            {
                var show = !last.HasValue || (m.Time - last.Value).TotalMinutes > 5;
                m.ShowTimeStamp = show;
                if (show) last = m.Time;
                // 新增: 重新計算快取載入後的顯示名稱標記 (群聊且非自己)
                m.ShowSenderName = IsGroup && !m.IsFromSelf;
            }
        }

        private MessageType DetectMessageType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return MessageType.Text;
            if (raw.Contains("[CQ:image")) return MessageType.Image;
            if (raw.Contains("[CQ:record")) return MessageType.Voice;
            if (raw.Contains("[CQ:video")) return MessageType.Video;
            return MessageType.Text;
        }

        private string ExtractFirst(string raw, string tag, string key)
        {
            try
            {
                var match = Regex.Match(raw ?? string.Empty, $"\\[CQ:{tag},([^]]+)\\]");
                if (!match.Success) return null;
                foreach (var part in match.Groups[1].Value.Split(','))
                {
                    if (part.StartsWith(key + "=")) return part.Substring(key.Length + 1);
                }
            }
            catch { }
            return null;
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage) return;
            try
            {
                IsSending = true;
                var raw = InputMessage?.Trim();
                if (string.IsNullOrEmpty(raw)) { IsSending = false; return; }
                InputMessage = string.Empty; // 清空輸入框
                var pending = BuildOutgoingMessage(raw);
                if (pending.MessageId != 0) _messageIdSet.Add(pending.MessageId);
                AddMessageUI(pending);
                await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
                // 新增: 更新 ChatCache 最後消息類型
                try
                {
                    var chatItem = new ChatItem
                    {
                        ChatId = ChatId,
                        IsGroup = IsGroup,
                        Name = ChatName,
                        LastMessage = pending.Content,
                        LastMessageTime = pending.Time,
                        LastTime = pending.Time,
                        LastMessageType = pending.MessageType.ToString(),
                        AvatarUrl = !string.IsNullOrEmpty(ChatAvatar) ? ChatAvatar : (IsGroup ? $"https://p.qlogo.cn/gh/{ChatId}/{ChatId}/640/" : $"https://q1.qlogo.cn/g?b=qq&nk={ChatId}&s=640")
                    };
                    await _cacheManager.CacheChatItemAsync(chatItem);
                }
                catch (Exception cacheChatEx) { Debug.WriteLine("更新聊天快取 (發送) 失敗: " + cacheChatEx.Message); }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"發送消息失敗: {ex.Message}");
                var last = Messages.LastOrDefault(m => m.IsFromSelf && m.SendStatus == MessageSendStatus.Sending);
                if (last != null) last.SendStatus = MessageSendStatus.Failed;
            }
            finally { IsSending = false; }
        }

        private MessageItem BuildOutgoingMessage(string raw) => new MessageItem
        {
            MessageId = DateTime.Now.Ticks,
            Content = raw,
            Time = DateTime.Now,
            IsFromSelf = true,
            MessageType = MessageType.Text,
            SendStatus = MessageSendStatus.Sending,
            ShowTimeStamp = ShouldShowTimestampForOutgoing()
        };

        private MessageItem BuildOutgoingMediaMessage(MessageType type, string displayContent)
        {
            return new MessageItem
            {
                MessageId = DateTime.Now.Ticks,
                Content = displayContent,
                Time = DateTime.Now,
                IsFromSelf = true,
                MessageType = type,
                SendStatus = MessageSendStatus.Sending,
                ShowTimeStamp = ShouldShowTimestampForOutgoing()
            };
        }

        private void UpdateMediaCommandsCanExecute()
        {
            (UploadImageCommand as MvxCommand<object>)?.RaiseCanExecuteChanged();
            (UploadVoiceCommand as MvxCommand<object>)?.RaiseCanExecuteChanged();
            (UploadVideoCommand as MvxCommand<object>)?.RaiseCanExecuteChanged();
        }

        private const int MaxRetryCount = 2; // 簡單重試次數

        private async Task SendImageAsync(MediaUploadPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Base64Data)) return;
            try
            {
                IsUploadingMedia = true;
                var cq = $"[CQ:image,file=base64://{payload.Base64Data}]";
                var pending = BuildOutgoingMediaMessage(MessageType.Image, "🖼圖片");
                var seg = new MessageSegment("image") { Data = new Dictionary<string, object>() }; seg.Data["url"] = "data:image/auto;base64," + payload.Base64Data; pending.RichSegments.Add(seg);
                if (pending.MessageId != 0) _messageIdSet.Add(pending.MessageId);
                AddMessageUI(pending); await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
                await SendWithRetryAsync(pending, cq, MessageType.Image);
            }
            catch (Exception ex) { Debug.WriteLine($"發送圖片失敗: {ex.Message}"); }
            finally { IsUploadingMedia = false; }
        }

        private async Task SendVoiceAsync(MediaUploadPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Base64Data)) return;
            try
            {
                IsUploadingMedia = true;
                var cq = $"[CQ:record,file=base64://{payload.Base64Data}]";
                var pending = BuildOutgoingMediaMessage(MessageType.Voice, "🎵語音");
                var seg = new MessageSegment("record") { Data = new Dictionary<string, object>() }; pending.RichSegments.Add(seg);
                if (pending.MessageId != 0) _messageIdSet.Add(pending.MessageId);
                AddMessageUI(pending); await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
                await SendWithRetryAsync(pending, cq, MessageType.Voice);
            }
            catch (Exception ex) { Debug.WriteLine($"發送語音失敗: {ex.Message}"); }
            finally { IsUploadingMedia = false; }
        }

        private async Task SendVideoAsync(MediaUploadPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Base64Data)) return;
            try
            {
                IsUploadingMedia = true;
                var cq = $"[CQ:video,file=base64://{payload.Base64Data}]";
                var pending = BuildOutgoingMediaMessage(MessageType.Video, "🎬影片");
                var seg = new MessageSegment("video") { Data = new Dictionary<string, object>() }; pending.RichSegments.Add(seg);
                if (pending.MessageId != 0) _messageIdSet.Add(pending.MessageId);
                AddMessageUI(pending); await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
                await SendWithRetryAsync(pending, cq, MessageType.Video);
            }
            catch (Exception ex) { Debug.WriteLine($"發送影片失敗: {ex.Message}"); }
            finally { IsUploadingMedia = false; }
        }

        private async Task SendWithRetryAsync(MessageItem pending, string cqMessage, MessageType type)
        {
            int attempt = 0; OneBotResponse<object> resp = null; Exception lastEx = null;
            while (attempt <= MaxRetryCount)
            {
                try
                {
                    attempt++;
                    pending.SendStatus = MessageSendStatus.Sending;
                    resp = IsGroup ? await _oneBotService.SendGroupMsgAsync(ChatId, cqMessage) : await _oneBotService.SendPrivateMsgAsync(ChatId, cqMessage);
                    if (resp?.Status == "ok") { pending.SendStatus = MessageSendStatus.Sent; break; }
                    if (attempt > MaxRetryCount) pending.SendStatus = MessageSendStatus.Failed; else await Task.Delay(800 * attempt);
                }
                catch (Exception ex)
                {
                    lastEx = ex; if (attempt > MaxRetryCount) { pending.SendStatus = MessageSendStatus.Failed; break; } await Task.Delay(800 * attempt);
                }
            }
            if (pending.SendStatus == MessageSendStatus.Failed && lastEx != null) Debug.WriteLine($"媒體發送最終失敗: {lastEx.Message}");
            await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
        }

        private Task DeleteMessageAsync(MessageItem message)
        {
            if (message == null) return Task.FromResult(true);
            Messages.Remove(message);
            return Task.FromResult(true);
        }
        private void CopyMessage(MessageItem message) { if (message?.Content != null) Debug.WriteLine($"已複製消息: {message.Content}"); }
        private async Task ResendMessageAsync(MessageItem message)
        {
            if (message == null) return;
            try
            {
                message.SendStatus = MessageSendStatus.Sending;
                OneBotResponse<object> result = IsGroup ? await _oneBotService.SendGroupMsgAsync(ChatId, message.Content) : await _oneBotService.SendPrivateMsgAsync(ChatId, message.Content);
                message.SendStatus = result?.Status == "ok" ? MessageSendStatus.Sent : MessageSendStatus.Failed;
            }
            catch (Exception ex) { Debug.WriteLine($"重發消息失敗: {ex.Message}"); message.SendStatus = MessageSendStatus.Failed; }
        }
        private void SelectMessage(MessageItem message) { if (message != null) message.IsSelected = !message.IsSelected; }

        private bool ShouldShowTimestampForOutgoing()
        {
            var last = Messages.LastOrDefault();
            if (last == null) return true;
            return (DateTime.Now - last.Time).TotalMinutes > 5;
        }

        private async Task PrefetchAvatarAsync(MessageItem msg)
        {
            try
            {
                if (msg == null || string.IsNullOrEmpty(msg.SenderAvatar)) return;
                var category = "friend"; // 成員與好友皆視為 friend 類別頭像
                if (IsGroup && msg.SenderId == ChatId) category = "group"; // 群本身頭像 (很少用到)
                var path = await _avatarCacheService.PrefetchAsync(msg.SenderAvatar, category, msg.SenderId);
                if (!string.IsNullOrEmpty(path))
                {
#if WINDOWS_UWP
                    try
                    {
                        var tempPath = global::Windows.Storage.ApplicationData.Current.TemporaryFolder.Path;
                        if (path.StartsWith(tempPath))
                        {
                            var fileName = System.IO.Path.GetFileName(path);
                            msg.SenderAvatar = $"ms-appdata:///temp/{fileName}";
                        }
                        else msg.SenderAvatar = path;
                    }
                    catch { msg.SenderAvatar = path; }
#else
                    msg.SenderAvatar = path;
#endif
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("預取頭像失敗: " + ex.Message);
            }
        }

        private async Task EnsureReplyTargetLoadedAsync(long targetId)
        {
            try
            {
                if (targetId <= 0) return;
                if (Messages.Any(m => m.MessageId == targetId)) return;
                lock (_fetchingMessageIds)
                {
                    if (_fetchingMessageIds.Contains(targetId)) return;
                    _fetchingMessageIds.Add(targetId);
                }
                var resp = await _oneBotService.GetMessageAsync(targetId);
                if (resp?.Status == "ok" && resp.Data != null)
                {
                    JToken dataToken = resp.Data as JToken ?? JToken.FromObject(resp.Data);
                    if (dataToken != null)
                    {
                        var messageId = dataToken.Value<long?>("message_id") ?? targetId;
                        if (Messages.Any(m => m.MessageId == messageId)) return;
                        var rawMessage = dataToken.Value<string>("message") ?? string.Empty;
                        var userId = dataToken.Value<long?>("user_id") ?? dataToken.SelectToken("sender.user_id")?.Value<long?>();
                        var senderNick = dataToken.SelectToken("sender.nickname")?.ToString() ?? dataToken.SelectToken("sender.card")?.ToString();
                        var timeSec = dataToken.Value<long?>("time") ?? dataToken.Value<long?>("send_time");
                        DateTime msgTime = timeSec.HasValue ? new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(timeSec.Value).ToLocalTime() : DateTime.Now;
                        List<MessageSegment> segments; MessageType mt; string firstImg; string display;
                        ParseCQMessage(rawMessage, out segments, out mt, out firstImg, out display);
                        var previewItem = new MessageItem
                        {
                            MessageId = messageId,
                            Content = display,
                            Time = msgTime,
                            IsFromSelf = userId.HasValue && userId.Value == _selfUserId,
                            SenderId = userId ?? 0,
                            SenderName = senderNick ?? (IsGroup ? "成員" : ChatTitle),
                            MessageType = mt,
                            SendStatus = MessageSendStatus.Sent,
                            ImageUrl = firstImg,
                            SenderAvatar = BuildAvatarUrl(userId ?? 0, IsGroup),
                            IsPreview = true
                        };
                        if (segments != null) foreach (var s in segments) previewItem.RichSegments.Add(s);
                        Action addAction = () =>
                        {
                            if (Messages.Any(m => m.MessageId == previewItem.MessageId)) return;
                            var index = 0;
                            while (index < Messages.Count && Messages[index].Time <= previewItem.Time) index++;
                            Messages.Insert(index, previewItem);
                            RecalculateTimestamps();
                            ScrollToMessageRequestId = previewItem.MessageId;
                        };
                        if (_uiDispatcher != null && !_uiDispatcher.RequestMainThreadAction(addAction)) addAction();
                        else if (_uiDispatcher == null) addAction();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"補取 Reply 原始消息失敗: {ex.Message}");
            }
            finally
            {
                lock (_fetchingMessageIds) _fetchingMessageIds.Remove(targetId);
                if (!Messages.Any(m => m.MessageId == targetId)) ScrollToMessageRequestId = targetId;
            }
        }

        #region ParseCQ

        // 解析 CQ 字串為 segments + 顯示文本
        private void ParseCQMessage(string raw, out List<MessageSegment> segments, out MessageType overallType, out string firstImageUrl, out string displayText)
        {
            segments = new List<MessageSegment>(); overallType = MessageType.Text; firstImageUrl = null; displayText = string.Empty;
            if (string.IsNullOrEmpty(raw)) return;
            var pattern = new Regex("\\[CQ:([^,\\n\\r\\]]+)((?:,[^\\]]+)*)\\]");
            int lastIndex = 0;
            foreach (Match m in pattern.Matches(raw))
            {
                if (m.Index > lastIndex)
                {
                    var plain = raw.Substring(lastIndex, m.Index - lastIndex);
                    if (!string.IsNullOrEmpty(plain))
                    {
                        segments.Add(new MessageSegment("text") { Data = new Dictionary<string, object> { { "text", plain } } });
                        displayText += plain;
                    }
                }
                var type = m.Groups[1].Value.Trim();
                var dataStr = m.Groups[2].Value; // ,k=v,k2=v2
                var dict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(dataStr))
                {
                    foreach (var kv in dataStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var eq = kv.IndexOf('=');
                        if (eq > 0 && eq < kv.Length - 1)
                        {
                            var k = kv.Substring(0, eq); var v = kv.Substring(eq + 1);
                            dict[k] = v;
                        }
                    }
                }
                segments.Add(new MessageSegment(type) { Data = dict });
                // 更新顯示 & type 判斷
                switch (type)
                {
                    case "image":
                        if (dict.ContainsKey("url") && firstImageUrl == null) firstImageUrl = dict["url"].ToString();
                        else if (dict.ContainsKey("file") && firstImageUrl == null) firstImageUrl = dict["file"].ToString();
                        if (overallType == MessageType.Text) overallType = MessageType.Image; displayText += "[圖片]"; break;
                    case "record": if (overallType == MessageType.Text) overallType = MessageType.Voice; displayText += "[語音]"; break;
                    case "video": if (overallType == MessageType.Text) overallType = MessageType.Video; displayText += "[視頻]"; break;
                    case "reply": displayText += "[回覆]"; break;
                    case "at":
                        var qq = dict.ContainsKey("qq") ? dict["qq"].ToString() : ""; displayText += (qq == "all" ? "@所有人 " : ("@" + qq + " ")); break;
                    case "face": displayText += "[表情]"; break;
                    default: displayText += "[" + type + "]"; break;
                }
                lastIndex = m.Index + m.Length;
            }
            if (lastIndex < raw.Length)
            {
                var remain = raw.Substring(lastIndex);
                if (!string.IsNullOrEmpty(remain))
                {
                    segments.Add(new MessageSegment("text") { Data = new Dictionary<string, object> { { "text", remain } } });
                    displayText += remain;
                }
            }
            if (segments.Count == 0)
            {
                // 純文字
                segments.Add(new MessageSegment("text") { Data = new Dictionary<string, object> { { "text", raw } } });
                displayText = raw;
            }
        }
        #endregion // ParseCQ
        #endregion // Message Operations
    }
}