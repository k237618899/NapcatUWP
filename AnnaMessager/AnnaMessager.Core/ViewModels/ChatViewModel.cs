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
    /// 聊天 ViewModel
    /// </summary>
    public class ChatViewModel : MvxViewModel, IDisposable
    {
        private readonly ICacheManager _cacheManager;
        private readonly INotificationService _notificationService;
        private readonly IOneBotService _oneBotService;
        private readonly IAvatarCacheService _avatarCacheService;

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
        private long _selfUserId;
        private long _oldestMessageSeqLoaded;
        private bool _hasMoreHistory = true;
        private bool _isUploadingMedia;
        private bool _initialLoadFinished;
        private bool _pendingConnectionLoad;
        private readonly HashSet<long> _messageIdSet = new HashSet<long>();
        private readonly HashSet<long> _fetchingMessageIds = new HashSet<long>();
        private long _scrollToMessageRequestId;
        private MvvmCross.Platform.Core.IMvxMainThreadDispatcher _uiDispatcher;

        public ChatViewModel()
        {
            _oneBotService = Mvx.Resolve<IOneBotService>();
            _cacheManager = Mvx.Resolve<ICacheManager>();
            _notificationService = Mvx.Resolve<INotificationService>();
            try { _avatarCacheService = Mvx.Resolve<IAvatarCacheService>(); } catch { }
            try { _uiDispatcher = Mvx.Resolve<MvvmCross.Platform.Core.IMvxMainThreadDispatcher>(); } catch { }

            Messages = new ObservableCollection<MessageItem>();
            SendMessageCommand = new MvxCommand(async () => await SendMessageAsync(), () => CanSendMessage);
            LoadMoreMessagesCommand = new MvxCommand(async () => await LoadMoreMessagesAsync());
            SelectMessageCommand = new MvxCommand<MessageItem>(SelectMessage);
            DeleteMessageCommand = new MvxCommand<MessageItem>(async m => await DeleteMessageAsync(m));
            CopyMessageCommand = new MvxCommand<MessageItem>(CopyMessage);
            ResendMessageCommand = new MvxCommand<MessageItem>(async m => await ResendMessageAsync(m));
            UploadImageCommand = new MvxCommand<object>(async p => await SendImageAsync(p as MediaUploadPayload), p => CanSendMedia);
            UploadVoiceCommand = new MvxCommand<object>(async p => await SendVoiceAsync(p as MediaUploadPayload), p => CanSendMedia);
            UploadVideoCommand = new MvxCommand<object>(async p => await SendVideoAsync(p as MediaUploadPayload), p => CanSendMedia);

            _oneBotService.MessageReceived += OnMessageReceived;
            _oneBotService.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        #region Public Props
        public long ChatId { get => _chatId; set { if (SetProperty(ref _chatId, value)) RaisePropertyChanged(() => GroupInfo); } }
        public bool IsGroup { get => _isGroup; set { if (SetProperty(ref _isGroup, value)) { RaisePropertyChanged(() => ChatTitle); RaisePropertyChanged(() => GroupInfo); } } }
        public string ChatName { get => _chatName; set { if (SetProperty(ref _chatName, value)) RaisePropertyChanged(() => ChatTitle); } }
        public string ChatAvatar { get => _chatAvatar; set => SetProperty(ref _chatAvatar, value); }
        public string InputMessage { get => _inputMessage; set { if (SetProperty(ref _inputMessage, value)) { RaisePropertyChanged(() => CanSendMessage); ((MvxCommand)SendMessageCommand).RaiseCanExecuteChanged(); } } }
        public bool IsSending { get => _isSending; set { if (SetProperty(ref _isSending, value)) { RaisePropertyChanged(() => CanSendMessage); ((MvxCommand)SendMessageCommand).RaiseCanExecuteChanged(); } } }
        public bool IsUploadingMedia { get => _isUploadingMedia; set { if (SetProperty(ref _isUploadingMedia, value)) { RaisePropertyChanged(() => CanSendMedia); UpdateMediaCommandsCanExecute(); } } }
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool IsOnline { get => _isOnline; set { if (SetProperty(ref _isOnline, value)) RaisePropertyChanged(() => OnlineStatus); } }
        public int MemberCount { get => _memberCount; set { if (SetProperty(ref _memberCount, value)) { RaisePropertyChanged(() => ChatTitle); RaisePropertyChanged(() => OnlineStatus); } } }
        public ObservableCollection<MessageItem> Messages { get => _messages; set => SetProperty(ref _messages, value); }
        public bool CanSendMessage => !string.IsNullOrWhiteSpace(InputMessage) && !IsSending && _oneBotService.IsConnected;
        public bool CanSendMedia => !_isUploadingMedia && _oneBotService.IsConnected;
        public string ChatTitle => IsGroup ? $"{ChatName} ({MemberCount})" : ChatName;
        public string OnlineStatus => IsGroup ? $"{MemberCount} 位成員" : IsOnline ? "在線" : "離線";
        public string GroupInfo => IsGroup ? $"群號: {ChatId}" : string.Empty;
        public long ScrollToMessageRequestId { get => _scrollToMessageRequestId; private set => SetProperty(ref _scrollToMessageRequestId, value); }
        public void RequestScrollToMessage(long id) { if (id > 0) ScrollToMessageRequestId = id; }
        private void AddMessageUI(MessageItem item)
        {
            if (item == null) return;
            RunOnUI(() => Messages.Add(item));
        }
        private void RebuildMessageSegmentsIfNeeded(MessageItem msg)
        {
            try
            {
                if (msg == null || string.IsNullOrEmpty(msg.RawMessage)) return;
                
                // 检查是否需要重建段落
                bool needsRebuild = false;
                
                // 如果没有 RichSegments，需要重建
                if (!msg.HasRichSegments)
                {
                    needsRebuild = true;
                }
                // 如果有 CQ 码但段落数量为空或者内容不匹配，需要重建
                else if (msg.RawMessage.Contains("[CQ:"))
                {
                    // 检查段落是否正确反映了原始消息
                    var segmentTypes = msg.RichSegments.Select(s => s.Type?.ToLower()).ToList();
                    
                    // 检查图片消息
                    if (msg.RawMessage.Contains("[CQ:image") && !segmentTypes.Contains("image"))
                    {
                        needsRebuild = true;
                    }
                    // 检查回复消息
                    else if (msg.RawMessage.Contains("[CQ:reply") && !segmentTypes.Contains("reply"))
                    {
                        needsRebuild = true;
                    }
                    // 检查 @ 消息
                    else if (msg.RawMessage.Contains("[CQ:at") && !segmentTypes.Contains("at"))
                    {
                        needsRebuild = true;
                    }
                }
                
                if (!needsRebuild) return;
                
                Debug.WriteLine($"重建消息段: {msg.MessageId}, Raw: {msg.RawMessage}");
                
                // 清空现有的segments以确保重新解析
                msg.RichSegments.Clear();
                
                ParseCQMessage(msg.RawMessage, out var segs, out var mt, out var firstImg, out var display);
                if (segs != null && segs.Count > 0)
                {
                    foreach (var s in segs) msg.RichSegments.Add(s);
                    
                    // 更新消息类型（如果当前类型是Text但解析出了其他类型）
                    if (msg.MessageType == MessageType.Text && mt != MessageType.Text) 
                    {
                        msg.MessageType = mt;
                    }
                    
                    // 修复：确保图片URL同步更新到MessageItem的ImageUrl属性
                    if (!string.IsNullOrEmpty(firstImg)) 
                    {
                        msg.ImageUrl = firstImg;
                        Debug.WriteLine($"设置图片URL: {msg.MessageId} -> {firstImg}");
                    }
                    else if (msg.MessageType == MessageType.Image && segs.Count > 0)
                    {
                        // 如果解析时没有得到firstImg，但是消息类型是图片，尝试从segments中再次提取
                        foreach (var seg in segs)
                        {
                            if (string.Equals(seg.Type, "image", StringComparison.OrdinalIgnoreCase))
                            {
                                if (seg.Data.TryGetValue("url", out var urlObj) && urlObj != null && !string.IsNullOrEmpty(urlObj.ToString()))
                                {
                                    msg.ImageUrl = urlObj.ToString();
                                    Debug.WriteLine($"从segments提取图片URL: {msg.MessageId} -> {msg.ImageUrl}");
                                    break;
                                }
                                else if (seg.Data.TryGetValue("file", out var fileObj) && fileObj != null && !string.IsNullOrEmpty(fileObj.ToString()))
                                {
                                    msg.ImageUrl = fileObj.ToString();
                                    Debug.WriteLine($"从segments提取图片文件: {msg.MessageId} -> {msg.ImageUrl}");
                                    break;
                                }
                            }
                        }
                    }
                    
                    // 修复：更新显示内容，优先使用解析出的正确内容
                    if (!string.IsNullOrEmpty(display)) 
                    {
                        msg.Content = display;
                        Debug.WriteLine($"更新显示内容: {msg.MessageId} -> {display}");
                    }
                    
                    // 修复：处理回复消息
                    var replySeg = segs.FirstOrDefault(x => string.Equals(x.Type, "reply", StringComparison.OrdinalIgnoreCase));
                    if (replySeg != null && replySeg.Data.TryGetValue("id", out var rid))
                    {
                        msg.ReplySummary = $"回覆 #{rid}";
                        if (long.TryParse(rid?.ToString(), out var ridL)) 
                        { 
                            msg.ReplyTargetId = ridL; 
                            _ = EnsureReplyTargetLoadedAsync(ridL); 
                        }
                        Debug.WriteLine($"处理回复消息: {msg.MessageId} -> 回复目标: {rid}");
                    }
                    
                    // 修复：处理@消息，确保显示完整的@信息
                    var atSegments = segs.Where(x => string.Equals(x.Type, "at", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (atSegments.Count > 0)
                    {
                        foreach (var atSeg in atSegments)
                        {
                            if (atSeg.Data.TryGetValue("qq", out var qqObj))
                            {
                                var qq = qqObj?.ToString();
                                Debug.WriteLine($"处理@消息: {msg.MessageId} -> @{qq}");
                                // 这里可以添加更多@用户信息的处理逻辑
                            }
                        }
                    }
                    
                    Debug.WriteLine($"重建完成: {msg.MessageId}, Type: {mt}, ImageUrl: {msg.ImageUrl}, Segments: {segs.Count}, HasReply: {msg.HasReply}, Content: {msg.Content}");
                }
            }
            catch (Exception ex) { Debug.WriteLine("RebuildMessageSegmentsIfNeeded 失敗: " + ex.Message); }
        }
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

        #region Lifecycle
        public void Init(long chatId, bool isGroup, string chatName)
        {
            ChatId = chatId;
            IsGroup = isGroup;
            ChatName = chatName;
            if (_oneBotService.IsConnected && !_initialLoadFinished) _ = Task.Run(() => PerformInitialDataLoadAsync("InitConnected"));
            else if (!_oneBotService.IsConnected) _pendingConnectionLoad = true;
        }

        public override async Task Initialize()
        {
            await base.Initialize();
            if (!_oneBotService.IsConnected) { _pendingConnectionLoad = true; return; }
            await PerformInitialDataLoadAsync("Initialize");
        }

        private async Task PerformInitialDataLoadAsync(string reason)
        {
            if (_initialLoadFinished) return;
            _pendingConnectionLoad = false;
            try
            {
                Debug.WriteLine($"[ChatViewModel] 开始初始数据加载: {reason}");
                
                try
                {
                    var login = await _oneBotService.GetLoginInfoAsync();
                    if (login?.Status == "ok" && login.Data != null) _selfUserId = login.Data.UserId;
                }
                catch (Exception ex) { Debug.WriteLine("取得登入資訊失敗: " + ex.Message); }

                await LoadChatInfoAsync();
                EnsureChatAvatar();
                
                // 修复：优先加载缓存消息，然后异步获取最新消息
                await LoadCachedMessagesAsync();
                
                // 修复：延迟一点再加载网络消息，避免界面卡顿
                _ = Task.Run(async () =>
                {
                    await Task.Delay(200); // 让缓存消息先渲染
                    await LoadMessagesAsync();
                    
                    // 加载完成后滚动到底部
                    RunOnUI(() =>
                    {
                        Debug.WriteLine($"[ChatViewModel] 网络消息加载完成，当前消息总数: {Messages?.Count ?? 0}");
                    });
                });
                
                try { await _notificationService.ClearChatNotificationsAsync(ChatId, IsGroup); } catch { }
                _initialLoadFinished = true;
                Debug.WriteLine($"[ChatViewModel] 初始数据加载完成: {reason}");
            }
            catch (Exception ex) { Debug.WriteLine("初始載入失敗: " + ex.Message); }
        }
        #endregion

        #region Connection / Events
        private void OnConnectionStatusChanged(object s, bool connected)
        {
            if (connected && _pendingConnectionLoad && !_initialLoadFinished)
                _ = Task.Run(() => PerformInitialDataLoadAsync("ConnectedLater"));
        }

        private async void OnMessageReceived(object sender, MessageEventArgs e)
        {
            if (e?.Message == null) return; // 修正語法
            try
            {
                var belongs = IsGroup ? (e.Message.GroupId == ChatId) : (e.Message.UserId == ChatId || e.Message.Sender?.UserId == ChatId);
                if (!belongs) return;

                // 修复：使用 RawMessage 而不是 Message，确保获得原始 CQ 码
                var rawMessage = e.Message.RawMessage ?? e.Message.Message;
                
                // Parse segments / fallback
                var segments = e.Message.Segments; 
                MessageType parsedType = MessageType.Text; 
                string firstImageFromCQ = null; 
                string displayFromCQ = e.Message.Message;
                
                if (segments == null || segments.Count == 0)
                {
                    ParseCQMessage(rawMessage, out var parsedSegs, out var mt, out var firstImg, out var disp); 
                    segments = parsedSegs; 
                    parsedType = mt; 
                    firstImageFromCQ = firstImg; 
                    displayFromCQ = disp;
                }
                
                var content = displayFromCQ; 
                MessageType type = parsedType; 
                string firstImage;
                
                if (segments != null && segments.Count > 0) 
                {
                    content = BuildDisplayFromSegments(segments, out type, out firstImage); 
                }
                else 
                {
                    firstImage = firstImageFromCQ ?? ExtractFirst(rawMessage, "image", "url");
                }

                var serverMsgId = e.Message.MessageId;
                var isSelf = (e.Message.Sender?.UserId ?? 0) == _selfUserId;
                
                // 修复：检查消息是否已存在，避免重复添加
                if (serverMsgId != 0 && _messageIdSet.Contains(serverMsgId)) 
                {
                    Debug.WriteLine($"消息已存在，跳过重复添加: {serverMsgId}");
                    return;
                }

                var msg = new MessageItem
                {
                    MessageId = serverMsgId != 0 ? serverMsgId : DateTime.Now.Ticks,
                    RawMessage = rawMessage,  // 修复：使用原始 CQ 码
                    Content = content,
                    Time = e.Message.DateTime == default ? DateTime.Now : e.Message.DateTime,
                    IsFromSelf = isSelf,
                    SenderName = e.Message.Sender?.Nickname ?? e.Message.Sender?.Card ?? (IsGroup ? "未知成員" : ChatTitle),
                    SenderId = e.Message.Sender?.UserId ?? e.Message.UserId,
                    MessageType = type,
                    ImageUrl = firstImage,
                    SendStatus = MessageSendStatus.Sent,
                    SenderAvatar = BuildAvatarUrl(e.Message.Sender?.UserId ?? 0, IsGroup)
                };
                
                // 添加段落信息
                if (segments != null)
                {
                    foreach (var s in segments) msg.RichSegments.Add(s);
                    
                    // 处理回复段落
                    var replySeg = segments.FirstOrDefault(x => string.Equals(x.Type, "reply", StringComparison.OrdinalIgnoreCase));
                    if (replySeg != null && replySeg.Data.TryGetValue("id", out var rid))
                    {
                        msg.ReplySummary = $"回覆 #{rid}";
                        if (long.TryParse(rid?.ToString(), out var ridL)) 
                        { 
                            msg.ReplyTargetId = ridL; 
                            _ = EnsureReplyTargetLoadedAsync(ridL); 
                        }
                    }
                }
                
                var last = Messages.LastOrDefault();
                if (last == null || (msg.Time - last.Time).TotalMinutes > 5) msg.ShowTimeStamp = true;
                msg.ShowSenderName = IsGroup && !msg.IsFromSelf;

                if (msg.MessageId != 0) _messageIdSet.Add(msg.MessageId);
                AddMessageUI(msg);
                _ = _cacheManager.CacheMessageAsync(ChatId, IsGroup, msg);
                
                // 修复：预缓存头像
                _ = PrefetchAvatarAsync(msg);
                
                Debug.WriteLine($"接收新消息: {msg.MessageId}, Type: {msg.MessageType}, HasSegments: {msg.HasRichSegments}, HasReply: {msg.HasReply}, Raw: {rawMessage}");
            }
            catch (Exception ex) { Debug.WriteLine("處理接收消息失敗: " + ex.Message); }
        }
        #endregion

        #region Load Chat Info
        private async Task LoadChatInfoAsync()
        {
            try
            {
                if (IsGroup)
                {
                    var g = await _oneBotService.GetGroupInfoAsync(ChatId);
                    if (g?.Status == "ok" && g.Data != null)
                    {
                        ChatName = g.Data.GroupName;
                        MemberCount = g.Data.MemberCount;
                        IsOnline = true;
                    }
                }
                else
                {
                    try
                    {
                        var status = await _oneBotService.GetUserStatusAsync(ChatId);
                        var tok = status?.Data as JToken ?? (status?.Data != null ? JToken.FromObject(status.Data) : null);
                        var st = tok?["status"]?.ToObject<int?>();
                        if (st.HasValue) IsOnline = st == 10 || st == 20;
                    }
                    catch { }
                    var stranger = await _oneBotService.GetStrangerInfoAsync(ChatId);
                    if (stranger?.Status == "ok" && stranger.Data != null && string.IsNullOrEmpty(ChatName)) ChatName = stranger.Data.Nickname;
                }
                
                // 修复：预缓存聊天头像
                _ = PrefetchChatAvatarAsync();
            }
            catch (Exception ex) { Debug.WriteLine("載入聊天信息失敗: " + ex.Message); }
        }
        
        private async Task PrefetchChatAvatarAsync()
        {
            if (_avatarCacheService == null) return;
            
            try
            {
                var remoteUrl = IsGroup ? 
                    $"https://p.qlogo.cn/gh/{ChatId}/{ChatId}/640/" : 
                    $"https://q1.qlogo.cn/g?b=qq&nk={ChatId}&s=640";
                
                var category = IsGroup ? "group" : "user";
                var localPath = await _avatarCacheService.PrefetchAsync(remoteUrl, category, ChatId);
                
                if (!string.IsNullOrEmpty(localPath))
                {
                    RunOnUI(() =>
                    {
                        // 修复：直接使用返回的缓存路径
                        ChatAvatar = localPath;
                        Debug.WriteLine($"聊天头像缓存成功: {ChatId} -> {localPath}");
                    });
                }
                else
                {
                    Debug.WriteLine($"聊天头像缓存失败，保持原始URL: {ChatId} -> {remoteUrl}");
                }
            }
            catch (Exception ex) 
            { 
                Debug.WriteLine($"聊天头像预缓存失败: {ChatId} - {ex.Message}"); 
            }
        }

        private void EnsureChatAvatar()
        {
            if (!string.IsNullOrEmpty(ChatAvatar)) return;
            
            // 修复：先设置默认头像，同时启动缓存任务
            ChatAvatar = IsGroup ? $"https://p.qlogo.cn/gh/{ChatId}/{ChatId}/640/" : $"https://q1.qlogo.cn/g?b=qq&nk={ChatId}&s=640";
            
            // 异步尝试获取缓存头像
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_avatarCacheService != null)
                    {
                        var category = IsGroup ? "group" : "user";
                        var remoteUrl = ChatAvatar;
                        var localPath = await _avatarCacheService.PrefetchAsync(remoteUrl, category, ChatId);
                        
                        if (!string.IsNullOrEmpty(localPath))
                        {
                            RunOnUI(() =>
                            {
                                // 修复：直接使用返回的缓存路径
                                ChatAvatar = localPath;
                                Debug.WriteLine($"聊天头像更新为缓存版本: {ChatId} -> {localPath}");
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"聊天头像缓存失败: {ChatId} - {ex.Message}");
                }
            });
        }
        #endregion

        #region Cached Messages
        private async Task LoadCachedMessagesAsync()
        {
            try
            {
                var cached = await _cacheManager.LoadCachedMessagesAsync(ChatId, IsGroup, 80);
                if (cached == null || cached.Count == 0) 
                {
                    Debug.WriteLine("没有找到缓存消息");
                    return;
                }
                
                var ordered = cached.OrderBy(c => c.Time).ToList();
                
                Debug.WriteLine($"开始加载缓存消息: {ordered.Count} 条");
                
                RunOnUI(() =>
                {
                    foreach (var m in ordered)
                    {
                        // 修复：先检查并重建消息段落
                        if (!string.IsNullOrEmpty(m.RawMessage))
                        {
                            RebuildMessageSegmentsIfNeeded(m);
                        }
                        
                        // 修复：确保头像URL正确设置
                        if (string.IsNullOrEmpty(m.SenderAvatar) && m.SenderId > 0)
                        {
                            m.SenderAvatar = BuildAvatarUrl(m.SenderId, IsGroup);
                        }
                        
                        // 确保消息 ID 不重复
                        if (m.MessageId != 0 && !_messageIdSet.Contains(m.MessageId)) 
                            _messageIdSet.Add(m.MessageId);
                        
                        // 添加到消息列表
                        Messages.Add(m);
                        
                        // 修复：预缓存头像
                        _ = PrefetchAvatarAsync(m);
                        
                        Debug.WriteLine($"加载缓存消息: {m.MessageId}, Type: {m.MessageType}, Sender: {m.SenderName}, Content: {m.Content?.Substring(0, Math.Min(m.Content?.Length ?? 0, 50))}..., HasSegments: {m.HasRichSegments}, HasReply: {m.HasReply}, Avatar: {m.SenderAvatar}");
                    }
                    
                    // 重新计算时间戳显示
                    RecalculateTimestamps();
                    
                    Debug.WriteLine($"缓存消息UI更新完成: {ordered.Count} 条");
                });
                
                Debug.WriteLine($"缓存消息加载完成: {ordered.Count} 条");
            }
            catch (Exception ex) { Debug.WriteLine("載入消息快取失敗: " + ex.Message); }
        }
        #endregion

        #region Media completion
        private async Task EnsureMediaInfoAsync(MessageItem msg)
        {
            try
            {
                if (msg == null) return;
                if (!(msg.MessageType == MessageType.Image || msg.MessageType == MessageType.Video || msg.MessageType == MessageType.Voice)) return;
                
                Debug.WriteLine($"补全媒体信息开始: {msg.MessageId}, Type: {msg.MessageType}, HasRaw: {!string.IsNullOrEmpty(msg.RawMessage)}, HasImage: {!string.IsNullOrEmpty(msg.ImageUrl)}, HasSegments: {msg.HasRichSegments}");
                
                // 首先尝试从RawMessage重新解析
                if (!string.IsNullOrEmpty(msg.RawMessage))
                {
                    ParseCQMessage(msg.RawMessage, out var segs, out var mt, out var firstImg, out var display);
                    if (segs != null && segs.Count > 0)
                    {
                        // 修复：始终重建 RichSegments，确保最新的解析结果
                        msg.RichSegments.Clear();
                        foreach (var s in segs) msg.RichSegments.Add(s);
                        Debug.WriteLine($"从RawMessage重建段落: {msg.MessageId}, Segments: {segs.Count}");
                        
                        // 更新消息类型
                        if (msg.MessageType == MessageType.Text && mt != MessageType.Text) msg.MessageType = mt;
                        
                        // 更新图片URL
                        if (!string.IsNullOrEmpty(firstImg)) 
                        {
                            msg.ImageUrl = firstImg;
                            Debug.WriteLine($"从RawMessage提取图片URL: {msg.MessageId} -> {firstImg}");
                        }
                        else if (msg.MessageType == MessageType.Image)
                        {
                            // 如果是图片消息但没有提取到firstImg，再次从segments中寻找
                            foreach (var seg in segs)
                            {
                                if (string.Equals(seg.Type, "image", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (seg.Data.TryGetValue("url", out var urlObj) && urlObj != null && !string.IsNullOrEmpty(urlObj.ToString()))
                                    {
                                        msg.ImageUrl = urlObj.ToString();
                                        Debug.WriteLine($"从segments二次提取图片URL: {msg.MessageId} -> {msg.ImageUrl}");
                                        break;
                                    }
                                    else if (seg.Data.TryGetValue("file", out var fileObj) && fileObj != null && !string.IsNullOrEmpty(fileObj.ToString()))
                                    {
                                        msg.ImageUrl = fileObj.ToString();
                                        Debug.WriteLine($"从segments二次提取图片文件: {msg.MessageId} -> {msg.ImageUrl}");
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // 更新显示内容
                        if (!string.IsNullOrEmpty(display)) msg.Content = display;
                    }
                }
                
                // 如果仍然没有图片URL且消息ID有效，尝试从服务器获取
                if (string.IsNullOrEmpty(msg.ImageUrl) && msg.MessageId > 0)
                {
                    Debug.WriteLine($"从服务器获取消息详情: {msg.MessageId}");
                    try
                    {
                        var resp = await _oneBotService.GetMessageAsync(msg.MessageId);
                        if (resp?.Status == "ok" && resp.Data != null)
                        {
                            var tok = resp.Data as JToken ?? JToken.FromObject(resp.Data);
                            var raw = tok.Value<string>("message");
                            if (!string.IsNullOrEmpty(raw))
                            {
                                msg.RawMessage = raw;
                                ParseCQMessage(raw, out var segs2, out var mt2, out var firstImg2, out var disp2);
                                if (segs2 != null && segs2.Count > 0)
                                {
                                    msg.RichSegments.Clear();
                                    foreach (var s in segs2) msg.RichSegments.Add(s);
                                    msg.Content = disp2;
                                    Debug.WriteLine($"从服务器重建段落: {msg.MessageId}, Segments: {segs2.Count}");
                                }
                                if (string.IsNullOrEmpty(msg.ImageUrl) && !string.IsNullOrEmpty(firstImg2)) 
                                {
                                    msg.ImageUrl = firstImg2;
                                    Debug.WriteLine($"从服务器提取图片URL: {msg.MessageId} -> {firstImg2}");
                                }
                                if (msg.MessageType == MessageType.Text && mt2 != MessageType.Text) msg.MessageType = mt2;
                                
                                // 缓存更新后的消息
                                await _cacheManager.CacheMessageAsync(ChatId, IsGroup, msg);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"从服务器获取消息失败: {ex.Message}");
                    }
                }
                
                Debug.WriteLine($"补全媒体信息完成: {msg.MessageId}, FinalImageUrl: {msg.ImageUrl}, HasSegments: {msg.HasRichSegments}");
            }
            catch (Exception ex) { Debug.WriteLine("補全媒體消息失敗: " + ex.Message); }
        }
        #endregion

        #region History / More (existing simplified)
        private async Task LoadMessagesAsync()
        {
            try
            {
                IsLoading = true;
                var history = await _oneBotService.GetMessageHistoryAsync(ChatId, IsGroup);
                if (history?.Status == "ok" && history.Data?.Messages != null)
                {
                    foreach (var h in history.Data.Messages.OrderBy(m => m.Time))
                    {
                        var time = h.Time == 0 ? DateTime.Now : new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(h.Time).ToLocalTime();
                        
                        // 修复：现在 h.Message 中已经包含了原始 CQ 码
                        var rawMessage = h.Message;
                        ParseCQMessage(rawMessage, out var segs, out var mt, out var firstImg, out var content);
                        
                        if (Messages.Any(x => x.MessageId == h.MessageId && h.MessageId != 0)) continue;
                        
                        var item = new MessageItem
                        {
                            MessageId = h.MessageId != 0 ? h.MessageId : h.RealId != 0 ? h.RealId : DateTime.Now.Ticks,
                            RawMessage = rawMessage,  // 修复：使用原始 CQ 码
                            Content = content,
                            Time = time,
                            IsFromSelf = (h.Sender?.UserId ?? 0) == _selfUserId,
                            SenderId = h.Sender?.UserId ?? 0,
                            SenderName = h.Sender?.Nickname ?? h.Sender?.Card ?? (IsGroup ? "成員" : ChatTitle),
                            MessageType = mt,
                            ImageUrl = firstImg,
                            SendStatus = MessageSendStatus.Sent,
                            SenderAvatar = BuildAvatarUrl(h.Sender?.UserId ?? 0, IsGroup)
                        };
                        if (segs != null) foreach (var s in segs) item.RichSegments.Add(s);
                        var last = Messages.LastOrDefault();
                        if (last == null || (item.Time - last.Time).TotalMinutes > 5) item.ShowTimeStamp = true;
                        item.ShowSenderName = IsGroup && !item.IsFromSelf;
                        if (item.MessageId != 0) _messageIdSet.Add(item.MessageId);
                        AddMessageUI(item);
                        _ = _cacheManager.CacheMessageAsync(ChatId, IsGroup, item);
                        
                        // 修复：预缓存头像
                        _ = PrefetchAvatarAsync(item);
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("載入消息失敗: " + ex.Message); }
            finally { IsLoading = false; }
        }

        private Task LoadMoreMessagesAsync() { return Task.FromResult(0); } // 簡化
        #endregion

        #region Send / Media
        private async Task SendMessageAsync()
        {
            if (!CanSendMessage) return;
            try
            {
                IsSending = true;
                var raw = InputMessage?.Trim();
                if (string.IsNullOrEmpty(raw)) { IsSending = false; return; }
                InputMessage = string.Empty;
                
                var pending = BuildOutgoingMessage(raw);
                pending.RawMessage = raw;
                
                // 修复：为 pending 消息生成临时 ID，避免与服务器消息冲突
                var tempId = DateTime.Now.Ticks;
                pending.MessageId = tempId;
                _messageIdSet.Add(tempId);
                
                AddMessageUI(pending);
                await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
                
                // 真正發送
                var resp = IsGroup ? await _oneBotService.SendGroupMsgAsync(ChatId, raw) : await _oneBotService.SendPrivateMsgAsync(ChatId, raw);
                
                // 修复：无论发送成功与否，都移除临时消息
                // message_sent 事件会添加正确的服务器消息
                RunOnUI(() =>
                {
                    var toRemove = Messages.FirstOrDefault(m => m.MessageId == tempId);
                    if (toRemove != null)
                    {
                        Messages.Remove(toRemove);
                        Debug.WriteLine($"移除临时消息: {tempId}");
                    }
                });
                
                // 从集合中移除临时 ID
                _messageIdSet.Remove(tempId);
                
                // 如果发送失败，重新添加失败状态的消息
                if (resp?.Status != "ok")
                {
                    pending.SendStatus = MessageSendStatus.Failed;
                    pending.MessageId = tempId; // 保持临时ID
                    _messageIdSet.Add(tempId);
                    AddMessageUI(pending);
                    await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
                    Debug.WriteLine($"发送失败，重新添加失败消息: {tempId}");
                }
                else
                {
                    Debug.WriteLine($"发送成功，等待 message_sent 事件: 临时ID={tempId}");
                }
            }
            catch (Exception ex) { Debug.WriteLine("發送消息失敗: " + ex.Message); }
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

        private bool ShouldShowTimestampForOutgoing()
        {
            var last = Messages.LastOrDefault();
            if (last == null) return true;
            return (DateTime.Now - last.Time).TotalMinutes > 5;
        }

        private async Task SendImageAsync(MediaUploadPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Base64Data)) return;
            try
            {
                IsUploadingMedia = true;
                var cq = $"[CQ:image,file=base64://{payload.Base64Data}]";
                var pending = BuildOutgoingMediaMessage(MessageType.Image, "🖼圖片");
                pending.RawMessage = cq;
                _messageIdSet.Add(pending.MessageId);
                AddMessageUI(pending);
                await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
                await SendWithRetryAsync(pending, cq);
            }
            finally { IsUploadingMedia = false; }
        }
        private async Task SendVoiceAsync(MediaUploadPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Base64Data)) return;
            try
            {
                IsUploadingMedia = true; var cq = $"[CQ:record,file=base64://{payload.Base64Data}]";
                var pending = BuildOutgoingMediaMessage(MessageType.Voice, "🎵語音"); pending.RawMessage = cq; _messageIdSet.Add(pending.MessageId); AddMessageUI(pending); await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending); await SendWithRetryAsync(pending, cq);
            }
            finally { IsUploadingMedia = false; }
        }
        private async Task SendVideoAsync(MediaUploadPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Base64Data)) return;
            try
            {
                IsUploadingMedia = true; var cq = $"[CQ:video,file=base64://{payload.Base64Data}]";
                var pending = BuildOutgoingMediaMessage(MessageType.Video, "🎬影片"); pending.RawMessage = cq; _messageIdSet.Add(pending.MessageId); AddMessageUI(pending); await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending); await SendWithRetryAsync(pending, cq);
            }
            finally { IsUploadingMedia = false; }
        }

        private MessageItem BuildOutgoingMediaMessage(MessageType type, string display) => new MessageItem
        {
            MessageId = DateTime.Now.Ticks,
            Content = display,
            Time = DateTime.Now,
            IsFromSelf = true,
            MessageType = type,
            SendStatus = MessageSendStatus.Sending,
            ShowTimeStamp = ShouldShowTimestampForOutgoing()
        };

        private async Task SendWithRetryAsync(MessageItem pending, string cq)
        {
            int attempt = 0; const int MaxRetry = 2;
            while (attempt <= MaxRetry)
            {
                try
                {
                    attempt++; pending.SendStatus = MessageSendStatus.Sending;
                    var resp = IsGroup ? await _oneBotService.SendGroupMsgAsync(ChatId, cq) : await _oneBotService.SendPrivateMsgAsync(ChatId, cq);
                    if (resp?.Status == "ok") { pending.SendStatus = MessageSendStatus.Sent; break; }
                }
                catch { }
                if (attempt > MaxRetry) pending.SendStatus = MessageSendStatus.Failed; else await Task.Delay(600 * attempt);
            }
            await _cacheManager.CacheMessageAsync(ChatId, IsGroup, pending);
        }

        private void UpdateMediaCommandsCanExecute()
        {
            (UploadImageCommand as MvxCommand<object>)?.RaiseCanExecuteChanged();
            (UploadVoiceCommand as MvxCommand<object>)?.RaiseCanExecuteChanged();
            (UploadVideoCommand as MvxCommand<object>)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Message Ops
        private Task DeleteMessageAsync(MessageItem m) { if (m != null) Messages.Remove(m); return Task.FromResult(0); }
        private void CopyMessage(MessageItem m) { }
        private async Task ResendMessageAsync(MessageItem m)
        {
            if (m == null) return; m.SendStatus = MessageSendStatus.Sending;
            try
            {
                var resp = IsGroup ? await _oneBotService.SendGroupMsgAsync(ChatId, m.Content) : await _oneBotService.SendPrivateMsgAsync(ChatId, m.Content);
                m.SendStatus = resp?.Status == "ok" ? MessageSendStatus.Sent : MessageSendStatus.Failed;
            }
            catch { m.SendStatus = MessageSendStatus.Failed; }
        }
        private void SelectMessage(MessageItem m) { if (m != null) m.IsSelected = !m.IsSelected; }
        #endregion

        #region Utility
        private void RunOnUI(Action act)
        {
            if (act == null) return;
            if (_uiDispatcher != null && _uiDispatcher.RequestMainThreadAction(act)) return; act();
        }

        private void RecalculateTimestamps()
        {
            DateTime? last = null;
            foreach (var m in Messages.OrderBy(x => x.Time))
            {
                var show = !last.HasValue || (m.Time - last.Value).TotalMinutes > 5; m.ShowTimeStamp = show; if (show) last = m.Time; m.ShowSenderName = IsGroup && !m.IsFromSelf;
            }
        }

        private string ExtractFirst(string raw, string tag, string key)
        {
            try
            {
                var match = Regex.Match(raw ?? string.Empty, $"\\[CQ:{tag},([^]]+)\\]"); if (!match.Success) return null;
                foreach (var part in match.Groups[1].Value.Split(',')) if (part.StartsWith(key + "=")) return part.Substring(key.Length + 1);
            }
            catch { }
            return null;
        }

        private async Task EnsureReplyTargetLoadedAsync(long id) { await Task.FromResult(0); }
        private async Task PrefetchAvatarAsync(MessageItem m) 
        { 
            if (_avatarCacheService == null || m == null || m.SenderId <= 0) return;
            
            try
            {
                var remoteUrl = BuildAvatarUrl(m.SenderId, IsGroup);
                if (string.IsNullOrEmpty(remoteUrl)) return;
                
                var category = IsGroup ? "group_member" : "user";
                var localPath = await _avatarCacheService.PrefetchAsync(remoteUrl, category, m.SenderId);
                
                if (!string.IsNullOrEmpty(localPath))
                {
                    // 修复：在UI线程更新头像路径
                    RunOnUI(() =>
                    {
                        // 直接使用返回的缓存路径（应该是ms-appdata格式）
                        m.SenderAvatar = localPath;
                        Debug.WriteLine($"头像缓存成功: {m.SenderId} -> {localPath}");
                    });
                }
                else
                {
                    Debug.WriteLine($"头像缓存失败，保持原始URL: {m.SenderId} -> {remoteUrl}");
                    // 如果缓存失败，确保至少有原始URL
                    RunOnUI(() =>
                    {
                        if (string.IsNullOrEmpty(m.SenderAvatar))
                        {
                            m.SenderAvatar = remoteUrl;
                        }
                    });
                }
            }
            catch (Exception ex) 
            { 
                Debug.WriteLine($"头像预缓存失败: {m.SenderId} - {ex.Message}"); 
                // 确保至少有头像URL
                RunOnUI(() =>
                {
                    if (string.IsNullOrEmpty(m.SenderAvatar))
                    {
                        m.SenderAvatar = BuildAvatarUrl(m.SenderId, IsGroup);
                    }
                });
            }
        }
        private string BuildAvatarUrl(long senderId, bool group) => senderId <= 0 ? null : $"https://q1.qlogo.cn/g?b=qq&nk={senderId}&s=100";

        private void ParseCQMessage(string raw, out List<MessageSegment> segments, out MessageType overallType, out string firstImageUrl, out string displayText)
        {
            segments = new List<MessageSegment>(); overallType = MessageType.Text; firstImageUrl = null; displayText = string.Empty;
            if (string.IsNullOrEmpty(raw)) return;
            var pattern = new Regex("\\[CQ:([^,\\n\\r\\]]+)((?:,[^\\]]+)*)\\]"); int lastIndex = 0;
            foreach (Match m in pattern.Matches(raw))
            {
                if (m.Index > lastIndex) { var plain = raw.Substring(lastIndex, m.Index - lastIndex); if (!string.IsNullOrEmpty(plain)) { segments.Add(new MessageSegment("text") { Data = new Dictionary<string, object> { { "text", plain } } }); displayText += plain; } }
                var type = m.Groups[1].Value.Trim(); var dataStr = m.Groups[2].Value; var dict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(dataStr)) foreach (var kv in dataStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { var eq = kv.IndexOf('='); if (eq > 0 && eq < kv.Length - 1) dict[kv.Substring(0, eq)] = kv.Substring(eq + 1); }
                segments.Add(new MessageSegment(type) { Data = dict });
                switch (type)
                {
                    case "image": if (dict.ContainsKey("url") && firstImageUrl == null) firstImageUrl = dict["url"].ToString(); else if (dict.ContainsKey("file") && firstImageUrl == null) firstImageUrl = dict["file"].ToString(); if (overallType == MessageType.Text) overallType = MessageType.Image; displayText += "[圖片]"; break;
                    case "record": if (overallType == MessageType.Text) overallType = MessageType.Voice; displayText += "[語音]"; break;
                    case "video": if (overallType == MessageType.Text) overallType = MessageType.Video; displayText += "[視頻]"; break;
                    case "reply": displayText += "[回覆]"; break;
                    case "at": var qq = dict.ContainsKey("qq") ? dict["qq"].ToString() : ""; displayText += (qq == "all" ? "@所有人 " : ("@" + qq + " ")); break;
                    case "face": displayText += "[表情]"; break;
                    default: displayText += "[" + type + "]"; break;
                }
                lastIndex = m.Index + m.Length;
            }
            if (lastIndex < raw.Length) { var remain = raw.Substring(lastIndex); if (!string.IsNullOrEmpty(remain)) { segments.Add(new MessageSegment("text") { Data = new Dictionary<string, object> { { "text", remain } } }); displayText += remain; } }
            if (segments.Count == 0) { segments.Add(new MessageSegment("text") { Data = new Dictionary<string, object> { { "text", raw } } }); displayText = raw; }
        }

        private string BuildDisplayFromSegments(List<MessageSegment> segments, out MessageType type, out string firstImageUrl)
        {
            type = MessageType.Text; firstImageUrl = null; if (segments == null) return string.Empty; var sb = new System.Text.StringBuilder();
            foreach (var seg in segments)
            {
                var t = (seg.Type ?? string.Empty).ToLower();
                switch (t)
                {
                    case "text": sb.Append(seg.Data.ContainsKey("text") ? seg.Data["text"] : ""); break;
                    case "at": var qq = seg.Data.ContainsKey("qq") ? seg.Data["qq"].ToString() : ""; sb.Append(qq == "all" ? "@所有人 " : "@" + qq + " "); break;
                    case "image": if (firstImageUrl == null) { if (seg.Data.ContainsKey("url")) firstImageUrl = seg.Data["url"].ToString(); else if (seg.Data.ContainsKey("file")) firstImageUrl = seg.Data["file"].ToString(); } if (type == MessageType.Text) type = MessageType.Image; sb.Append("[圖片]"); break;
                    case "record": if (type == MessageType.Text) type = MessageType.Voice; sb.Append("[語音]"); break;
                    case "video": if (type == MessageType.Text) type = MessageType.Video; sb.Append("[視頻]"); break;
                    case "reply": sb.Append("[回覆]"); break;
                    default: sb.Append('[').Append(seg.Type).Append(']'); break;
                }
            }
            return sb.ToString();
        }
        #endregion

        #region IDisposable
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return; if (disposing) { try { _oneBotService.MessageReceived -= OnMessageReceived; } catch { } try { _oneBotService.ConnectionStatusChanged -= OnConnectionStatusChanged; } catch { } } _disposed = true;
        }
        #endregion
    }
}