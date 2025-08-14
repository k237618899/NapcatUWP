using System;
using System.Collections.Generic;
using MvvmCross.Core.ViewModels;

namespace AnnaMessager.Core.Models
{
    public class ChatItem : MvxNotifyPropertyChanged
    {
        private string _avatarUrl;
        private bool _isGroup;
        private bool _isMuted;
        private bool _isPinned;
        private string _lastMessage;
        private DateTime _lastTime;
        private string _name;
        private ChatStatus _status;
        private int _unreadCount;

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
            set => SetProperty(ref _lastTime, value);
        }

        public int UnreadCount
        {
            get => _unreadCount;
            set => SetProperty(ref _unreadCount, value);
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

        // UI 顯示屬性
        public bool HasUnreadMessages => UnreadCount > 0;
        public string UnreadCountDisplay => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

        // 顯示用的時間格式
        public string DisplayTime
        {
            get
            {
                var now = DateTime.Now;
                var diff = now - LastTime;

                if (diff.TotalMinutes < 1)
                    return "剛剛";
                if (diff.TotalHours < 1)
                    return $"{(int)diff.TotalMinutes}分鐘前";
                if (LastTime.Date == now.Date)
                    return LastTime.ToString("HH:mm");
                if (LastTime.Date == now.Date.AddDays(-1))
                    return "昨天";
                if (diff.TotalDays < 7)
                    return LastTime.ToString("dddd");
                return LastTime.ToString("MM/dd");
            }
        }
    }

    public class MessageItem : MvxNotifyPropertyChanged
    {
        private bool _isSelected;
        private MessageSendStatus _sendStatus;
        private bool _showSenderName;
        private bool _showTimeStamp;

        public MessageItem()
        {
            Segments = new List<MessageSegment>();
            SendStatus = MessageSendStatus.Sent;
            Time = DateTime.Now;
        }

        public long MessageId { get; set; }
        public string Content { get; set; }
        public DateTime Time { get; set; }
        public bool IsFromSelf { get; set; }
        public string SenderName { get; set; }
        public long SenderId { get; set; }
        public MessageType MessageType { get; set; }
        public string SenderAvatarUrl { get; set; }
        public List<MessageSegment> Segments { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public MessageSendStatus SendStatus
        {
            get => _sendStatus;
            set
            {
                SetProperty(ref _sendStatus, value);
                // 通知狀態相關屬性變更
                RaisePropertyChanged(() => IsSending);
                RaisePropertyChanged(() => IsSent);
                RaisePropertyChanged(() => IsFailed);
            }
        }

        public bool ShowTimeStamp
        {
            get => _showTimeStamp;
            set => SetProperty(ref _showTimeStamp, value);
        }

        public bool ShowSenderName
        {
            get => _showSenderName;
            set => SetProperty(ref _showSenderName, value);
        }

        // UI 顯示屬性
        public string DisplayTime => Time.ToString("HH:mm");
        public string DisplayDate => Time.ToString("yyyy年MM月dd日");

        // 發送狀態相關屬性
        public bool IsSending => SendStatus == MessageSendStatus.Sending;
        public bool IsSent => SendStatus == MessageSendStatus.Sent;
        public bool IsFailed => SendStatus == MessageSendStatus.Failed;
        public bool IsRead => SendStatus == MessageSendStatus.Read;
    }

    public class ContactItem : MvxNotifyPropertyChanged
    {
        private string _avatarUrl;
        private bool _isOnline;
        private string _nickname;
        private string _remark;
        private UserStatus _status;

        public long UserId { get; set; }

        public string Nickname
        {
            get => _nickname;
            set => SetProperty(ref _nickname, value);
        }

        public string Remark
        {
            get => _remark;
            set => SetProperty(ref _remark, value);
        }

        public string AvatarUrl
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

        // 顯示名稱優先顯示備註
        public string DisplayName => !string.IsNullOrEmpty(Remark) ? Remark : Nickname;

        // UI 需要的屬性
        public bool HasRemark => !string.IsNullOrEmpty(Remark);
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