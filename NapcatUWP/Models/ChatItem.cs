using System.ComponentModel;

namespace NapcatUWP.Models
{
    public class ChatItem : INotifyPropertyChanged
    {
        private string _avatarColor;
        private long _chatId; // 聊天對象的ID（好友UserId或群組GroupId）
        private bool _isGroup; // 是否為群組聊天
        private string _lastMessage;
        private string _lastTime;
        private int _memberCount; // 群組成員數（僅群組聊天有效）
        private string _name;
        private int _unreadCount;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string LastMessage
        {
            get => _lastMessage;
            set
            {
                _lastMessage = value;
                OnPropertyChanged(nameof(LastMessage));
            }
        }

        public string LastTime
        {
            get => _lastTime;
            set
            {
                _lastTime = value;
                OnPropertyChanged(nameof(LastTime));
            }
        }

        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                _unreadCount = value;
                OnPropertyChanged(nameof(UnreadCount));
            }
        }

        public string AvatarColor
        {
            get => _avatarColor;
            set
            {
                _avatarColor = value;
                OnPropertyChanged(nameof(AvatarColor));
            }
        }

        public long ChatId
        {
            get => _chatId;
            set
            {
                _chatId = value;
                OnPropertyChanged(nameof(ChatId));
            }
        }

        public bool IsGroup
        {
            get => _isGroup;
            set
            {
                _isGroup = value;
                OnPropertyChanged(nameof(IsGroup));
            }
        }

        public int MemberCount
        {
            get => _memberCount;
            set
            {
                _memberCount = value;
                OnPropertyChanged(nameof(MemberCount));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}