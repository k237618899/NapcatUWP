using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MvvmCross.Core.ViewModels;
using Newtonsoft.Json; // 新增: JSON 映射屬性

namespace AnnaMessager.Core.Models
{
    public class ChatItem : MvxNotifyPropertyChanged
    {
        private string _avatarUrl;
        private bool _isGroup;
        private bool _isMuted;
        private bool _isPinned;
        private string _lastMessage;
        private DateTime _lastMessageTime;
        private string _lastMessageType;
        private DateTime _lastTime;
        private string _name;
        private ChatStatus _status;
        private int _unreadCount;
        private bool _showPinnedSeparator;
        private bool _isRecentlyUpdated;

        public long ChatId { get; set; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string LastMessage
        {
            get => _lastMessage;
            set => SetProperty(ref _lastMessage, value);
        }

        public DateTime LastTime
        {
            get => _lastTime;
            set
            {
                if (SetProperty(ref _lastTime, value))
                {
                    RaisePropertyChanged(() => DisplayTime);
                    RaisePropertyChanged(() => LastActivityTime);
                }
            }
        }

        public DateTime LastMessageTime
        {
            get => _lastMessageTime;
            set
            {
                if (SetProperty(ref _lastMessageTime, value))
                {
                    RaisePropertyChanged(() => DisplayTime);
                    RaisePropertyChanged(() => LastActivityTime);
                }
            }
        }

        public string LastMessageType
        {
            get => _lastMessageType;
            set => SetProperty(ref _lastMessageType, value);
        }

        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                if (SetProperty(ref _unreadCount, value))
                {
                    RaisePropertyChanged(() => HasUnreadMessages);
                    RaisePropertyChanged(() => UnreadCountDisplay);
                }
            }
        }

        public bool IsGroup
        {
            get => _isGroup;
            set => SetProperty(ref _isGroup, value);
        }

        public string AvatarUrl
        {
            get => _avatarUrl;
            set => SetProperty(ref _avatarUrl, value);
        }

        // 別名屬性，用於緩存兼容性
        public string Avatar
        {
            get => _avatarUrl;
            set => SetProperty(ref _avatarUrl, value);
        }

        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }

        public ChatStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool ShowPinnedSeparator
        {
            get => _showPinnedSeparator;
            set => SetProperty(ref _showPinnedSeparator, value);
        }

        public bool IsRecentlyUpdated
        {
            get => _isRecentlyUpdated;
            set => SetProperty(ref _isRecentlyUpdated, value);
        }

        // UI 顯示屬性
        public bool HasUnreadMessages => UnreadCount > 0;
        public string UnreadCountDisplay => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

        // 顯示用的時間格式
        public string DisplayTime
        {
            get
            {
                var baseTime = LastMessageTime != default(DateTime) ? LastMessageTime : LastTime;
                if (baseTime == default(DateTime)) return "";

                // 轉為本地時間（如果是 UTC ）
                if (baseTime.Kind == DateTimeKind.Utc)
                    baseTime = baseTime.ToLocalTime();

                var now = DateTime.Now;
                var diff = now - baseTime;

                if (diff.TotalMinutes < 1)
                    return "剛剛";
                if (diff.TotalHours < 1)
                    return $"{(int)diff.TotalMinutes}分鐘前";
                if (baseTime.Date == now.Date)
                    return baseTime.ToString("HH:mm");
                if (baseTime.Date == now.Date.AddDays(-1))
                    return "昨天";
                if (diff.TotalDays < 7)
                    return baseTime.ToString("dddd");
                return baseTime.ToString("MM/dd");
            }
        }

        // 最近活動時間（供轉換器使用）
        public DateTime LastActivityTime
        {
            get
            {
                var baseTime = LastMessageTime != default(DateTime) ? LastMessageTime : LastTime;
                if (baseTime == default(DateTime)) return DateTime.MinValue;
                if (baseTime.Kind == DateTimeKind.Utc) return baseTime.ToLocalTime();
                return baseTime;
            }
        }
    }

    // 新增: NapCat get_recent_contact 回傳模型對應
    public class RecentContact
    {
        // 原始欄位: peerUin
        [JsonProperty("peerUin")] public long PeerUin { get; set; }
        // 原始欄位: chatType (2=群聊, 1=好友)
        [JsonProperty("chatType")] public int ChatType { get; set; }
        // 原始欄位: msgTime (字串秒級時間戳)
        [JsonProperty("msgTime")] public string MsgTimeRaw { get; set; }
        // 原始欄位: lastestMsg (包含 raw_message/time 等)
        [JsonProperty("lastestMsg")] public RecentContactLastMessage LastestMsg { get; set; }
        // 備註(可能為空)
        [JsonProperty("remark")] public string Remark { get; set; }

        // 兼容 ChatListViewModel 既有命名
        [JsonIgnore] public long PeerId => PeerUin;
        [JsonIgnore] public bool IsGroup => ChatType == 2;
        [JsonIgnore] public long LastTime
        {
            get
            {
                if (long.TryParse(MsgTimeRaw, out var s)) return s;
                if (LastestMsg != null) return LastestMsg.Time;
                return 0;
            }
        }
        [JsonIgnore] public string LastMessage => LastestMsg?.RawMessage;
    }

    public class RecentContactLastMessage
    {
        [JsonProperty("raw_message")] public string RawMessage { get; set; }
        [JsonProperty("time")] public long Time { get; set; }
        // 其餘欄位(如 sender/message 等) 暫不需要
    }

    public class MessageItem : MvxNotifyPropertyChanged
    {
        private long _messageId;
        private string _content;
        private DateTime _time;
        private bool _isFromSelf;
        private string _senderName;
        private long _senderId;
        private MessageType _messageType;
        private MessageSendStatus _sendStatus;
        private bool _isSelected;
        private string _imageUrl;
        private string _faceId;
        private string _audioFile;
        private string _videoFile;
        private string _forwardId;
        private bool _isLoadingExtra;
        private bool _isPlaying;
        private ObservableCollection<MessageItem> _forwardNodes;
        private bool _showTimeStamp;
        private bool _showSenderName;
        private ObservableCollection<MessageSegment> _richSegments; // 新增: 多媒體段
        private string _senderAvatar;
        private string _replySummary;
        private long _replyTargetId;
        private bool _isPreview;
        private string _rawMessage;              // 新增: 原始 CQ 字串
        private string _richSegmentsSerialized;  // 新增: 序列化 JSON (可選)

        public MessageItem()
        {
            SendStatus = MessageSendStatus.Sent;
            Time = DateTime.Now;
            _richSegments = new ObservableCollection<MessageSegment>();
            _richSegments.CollectionChanged += (s, e) => { RaisePropertyChanged(() => HasRichSegments); };
        }

        public long MessageId { get => _messageId; set => SetProperty(ref _messageId, value); }
        public string Content { get => _content; set => SetProperty(ref _content, value); }
        public DateTime Time { get => _time; set { if (SetProperty(ref _time, value)) { RaisePropertyChanged(() => DisplayTime); RaisePropertyChanged(() => DisplayDate); } } }
        public bool IsFromSelf { get => _isFromSelf; set => SetProperty(ref _isFromSelf, value); }
        public string SenderName { get => _senderName; set => SetProperty(ref _senderName, value); }
        public long SenderId { get => _senderId; set => SetProperty(ref _senderId, value); }
        public MessageType MessageType { get => _messageType; set => SetProperty(ref _messageType, value); }
        public MessageSendStatus SendStatus { get => _sendStatus; set { if (SetProperty(ref _sendStatus, value)) { RaisePropertyChanged(() => IsSending); RaisePropertyChanged(() => IsSent); RaisePropertyChanged(() => IsFailed); } } }
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
        // 確保包含 UWP 使用的媒體屬性
        public string ImageUrl { get => _imageUrl; set => SetProperty(ref _imageUrl, value); }
        public string FaceId { get => _faceId; set => SetProperty(ref _faceId, value); }
        public string AudioFile { get => _audioFile; set => SetProperty(ref _audioFile, value); }
        public string VideoFile { get => _videoFile; set => SetProperty(ref _videoFile, value); }
        public string ForwardId { get => _forwardId; set => SetProperty(ref _forwardId, value); }
        public bool IsLoadingExtra { get => _isLoadingExtra; set => SetProperty(ref _isLoadingExtra, value); }
        public bool IsPlaying { get => _isPlaying; set => SetProperty(ref _isPlaying, value); }
        public ObservableCollection<MessageItem> ForwardNodes { get => _forwardNodes ?? (_forwardNodes = new ObservableCollection<MessageItem>()); set => SetProperty(ref _forwardNodes, value); }
        public bool ShowTimeStamp { get => _showTimeStamp; set => SetProperty(ref _showTimeStamp, value); }
        public bool ShowSenderName { get => _showSenderName; set => SetProperty(ref _showSenderName, value); }
        public ObservableCollection<MessageSegment> RichSegments { get => _richSegments ?? (_richSegments = new ObservableCollection<MessageSegment>()); set { if (_richSegments != value) { if (_richSegments != null) { try { _richSegments.CollectionChanged -= _richSegments_CollectionChanged; } catch { } } _richSegments = value ?? new ObservableCollection<MessageSegment>(); try { _richSegments.CollectionChanged += _richSegments_CollectionChanged; } catch { } SetProperty(ref _richSegments, _richSegments); } RaisePropertyChanged(() => HasRichSegments); } }
        private void _richSegments_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) { RaisePropertyChanged(() => HasRichSegments); }
        public bool HasRichSegments => _richSegments != null && _richSegments.Count > 0;
        public string SenderAvatar { get => _senderAvatar; set => SetProperty(ref _senderAvatar, value); }
        public string SenderInitial => string.IsNullOrEmpty(SenderName) ? "?" : SenderName.Substring(0,1);
        public string ReplySummary { get => _replySummary; set { if (SetProperty(ref _replySummary, value)) { RaisePropertyChanged(() => HasReply); } } }
        public bool HasReply => !string.IsNullOrEmpty(ReplySummary);
        public long ReplyTargetId { get => _replyTargetId; set => SetProperty(ref _replyTargetId, value); }
        public bool IsPreview { get => _isPreview; set => SetProperty(ref _isPreview, value); }

        // 新增: 原始 CQ 字串屬性 (供重新解析 RichSegments)
        public string RawMessage { get => _rawMessage; set => SetProperty(ref _rawMessage, value); }
        // 新增: 序列化後的 segments (目前僅預留，尚未在載入時使用)
        public string RichSegmentsSerialized { get => _richSegmentsSerialized; set => SetProperty(ref _richSegmentsSerialized, value); }

        // 衍生屬性
        public string DisplayTime => Time.ToString("HH:mm");
        public string DisplayDate => Time.ToString("yyyy-MM-dd HH:mm");
        public bool IsSending => SendStatus == MessageSendStatus.Sending;
        public bool IsSent => SendStatus == MessageSendStatus.Sent || SendStatus == MessageSendStatus.Read;
        public bool IsFailed => SendStatus == MessageSendStatus.Failed;
        public bool HasMedia => !string.IsNullOrEmpty(ImageUrl) || !string.IsNullOrEmpty(AudioFile) || !string.IsNullOrEmpty(VideoFile);
        public bool IsForwardSummary => !string.IsNullOrEmpty(ForwardId) && (ForwardNodes == null || ForwardNodes.Count == 0);
    }

    public class ContactItem : MvxNotifyPropertyChanged
    {
        private string _avatarUrl;
        private bool _isOnline;
        private string _nickname;
        private string _remark;
        private UserStatus _status;
        private string _categoryName;
        private int _categoryId; // 分類ID
        private string _namePrefix;
        private string _nameMatch;
        private string _nameSuffix;

        public long UserId { get; set; }

        public string Nickname
        {
            get => _nickname;
            set
            {
                if (SetProperty(ref _nickname, value))
                {
                    // 顯示名稱依賴 Nickname
                    RaisePropertyChanged(() => DisplayName);
                }
            }
        }

        public string Remark
        {
            get => _remark;
            set
            {
                if (SetProperty(ref _remark, value))
                {
                    // 顯示名稱依賴 Remark
                    RaisePropertyChanged(() => DisplayName);
                }
            }
        }

        public string AvatarUrl
        {
            get => _avatarUrl;
            set => SetProperty(ref _avatarUrl, value);
        }

        // 別名屬性，用於緩存兼容性
        public string Avatar
        {
            get => _avatarUrl;
            set => SetProperty(ref _avatarUrl, value);
        }

        public UserStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsOnline
        {
            get => _isOnline;
            set => SetProperty(ref _isOnline, value);
        }

        public string CategoryName
        {
            get => _categoryName;
            set => SetProperty(ref _categoryName, value);
        }

        public int CategoryId
        {
            get => _categoryId;
            set => SetProperty(ref _categoryId, value);
        }

        // 顯示名稱優先顯示備註，其次暱稱，再次使用ID
        public string DisplayName => !string.IsNullOrEmpty(Remark) ? Remark : (!string.IsNullOrEmpty(Nickname) ? Nickname : UserId.ToString());

        // 搜尋高亮分段
        public string NamePrefix { get => _namePrefix; set => SetProperty(ref _namePrefix, value); }
        public string NameMatch { get => _nameMatch; set { if (SetProperty(ref _nameMatch, value)) { RaisePropertyChanged(() => HasHighlight); } } }
        public string NameSuffix { get => _nameSuffix; set => SetProperty(ref _nameSuffix, value); }
        public bool HasHighlight => !string.IsNullOrEmpty(NameMatch);

        // UI 需要的屬性
        public bool HasRemark => !string.IsNullOrEmpty(Remark);

        private string _onlineStatusText;
        public string OnlineStatusText
        {
            get => _onlineStatusText;
            set { if (SetProperty(ref _onlineStatusText, value)) { RaisePropertyChanged(() => HasOnlineStatus); } }
        }
        public bool HasOnlineStatus => !string.IsNullOrEmpty(OnlineStatusText);
    }

    public class GroupItem : MvxNotifyPropertyChanged
    {
        private string _avatarUrl;
        private string _groupName;
        private bool _isAdmin;
        private bool _isOwner;
        private int _memberCount;

        public long GroupId { get; set; }

        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public string AvatarUrl
        {
            get => _avatarUrl;
            set => SetProperty(ref _avatarUrl, value);
        }

        // 別名屬性，用於緩存兼容性
        public string GroupAvatar
        {
            get => _avatarUrl;
            set => SetProperty(ref _avatarUrl, value);
        }

        public int MemberCount
        {
            get => _memberCount;
            set => SetProperty(ref _memberCount, value);
        }

        public bool IsOwner
        {
            get => _isOwner;
            set => SetProperty(ref _isOwner, value);
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set => SetProperty(ref _isAdmin, value);
        }

        public string MemberCountDisplay => $"({MemberCount})";
    }

    public enum ChatStatus
    {
        Normal,
        Muted,
        Blocked
    }

    public enum MessageType
    {
        Text,
        Image,
        Voice,
        Video,
        File,
        Location,
        System
    }

    public enum MessageSendStatus
    {
        Sending,
        Sent,
        Failed,
        Read
    }

    public enum UserStatus
    {
        Online,
        Away,
        Busy,
        Offline
    }

    // 聊天過濾器
    public class ChatFilter
    {
        public bool? HasUnreadMessages { get; set; }
        public bool? IsPinned { get; set; }
        public bool? IsMuted { get; set; }
        public string SearchKeyword { get; set; }
    }
}