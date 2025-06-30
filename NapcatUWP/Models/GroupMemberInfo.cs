using System.ComponentModel;

namespace NapcatUWP.Models
{
    /// <summary>
    ///     群組成員信息
    /// </summary>
    public class GroupMemberInfo : INotifyPropertyChanged
    {
        private int _age;
        private string _area;
        private string _card;
        private bool _cardChangeable;
        private long _groupId;
        private long _joinTime;
        private long _lastSentTime;
        private string _level;
        private string _nickname;
        private string _role;
        private string _sex;
        private long _shutUpTimestamp;
        private string _title;
        private long _titleExpireTime;
        private bool _unfriendly;
        private long _userId;

        public long GroupId
        {
            get => _groupId;
            set
            {
                _groupId = value;
                OnPropertyChanged(nameof(GroupId));
            }
        }

        public long UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged(nameof(UserId));
            }
        }

        public string Nickname
        {
            get => _nickname;
            set
            {
                _nickname = value;
                OnPropertyChanged(nameof(Nickname));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string Card
        {
            get => _card;
            set
            {
                _card = value;
                OnPropertyChanged(nameof(Card));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string Sex
        {
            get => _sex;
            set
            {
                _sex = value;
                OnPropertyChanged(nameof(Sex));
            }
        }

        public int Age
        {
            get => _age;
            set
            {
                _age = value;
                OnPropertyChanged(nameof(Age));
            }
        }

        public string Area
        {
            get => _area;
            set
            {
                _area = value;
                OnPropertyChanged(nameof(Area));
            }
        }

        public long JoinTime
        {
            get => _joinTime;
            set
            {
                _joinTime = value;
                OnPropertyChanged(nameof(JoinTime));
            }
        }

        public long LastSentTime
        {
            get => _lastSentTime;
            set
            {
                _lastSentTime = value;
                OnPropertyChanged(nameof(LastSentTime));
            }
        }

        public string Level
        {
            get => _level;
            set
            {
                _level = value;
                OnPropertyChanged(nameof(Level));
            }
        }

        public string Role
        {
            get => _role;
            set
            {
                _role = value;
                OnPropertyChanged(nameof(Role));
            }
        }

        public bool Unfriendly
        {
            get => _unfriendly;
            set
            {
                _unfriendly = value;
                OnPropertyChanged(nameof(Unfriendly));
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        public long TitleExpireTime
        {
            get => _titleExpireTime;
            set
            {
                _titleExpireTime = value;
                OnPropertyChanged(nameof(TitleExpireTime));
            }
        }

        public bool CardChangeable
        {
            get => _cardChangeable;
            set
            {
                _cardChangeable = value;
                OnPropertyChanged(nameof(CardChangeable));
            }
        }

        public long ShutUpTimestamp
        {
            get => _shutUpTimestamp;
            set
            {
                _shutUpTimestamp = value;
                OnPropertyChanged(nameof(ShutUpTimestamp));
            }
        }

        /// <summary>
        ///     獲取顯示名稱（優先級：card > nickname > user_id）
        /// </summary>
        public string DisplayName => GetDisplayName();

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     獲取顯示名稱的方法
        /// </summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(Card))
                return Card;
            if (!string.IsNullOrEmpty(Nickname))
                return Nickname;
            return UserId.ToString();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}