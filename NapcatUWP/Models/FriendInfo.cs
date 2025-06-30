using System.ComponentModel;

namespace NapcatUWP.Models
{
    public class FriendInfo : INotifyPropertyChanged
    {
        private int _age;
        private int _birthdayDay;
        private int _birthdayMonth;
        private int _birthdayYear;
        private long _categoryId;
        private string _email;
        private int _level;
        private string _longNick;
        private string _nick;
        private string _nickname;
        private string _phoneNum;
        private string _qid;
        private string _remark;
        private long _richTime;
        private string _sex;
        private string _uid;
        private string _uin;
        private long _userId;

        public string Qid
        {
            get => _qid;
            set
            {
                _qid = value;
                OnPropertyChanged(nameof(Qid));
            }
        }

        public string LongNick
        {
            get => _longNick;
            set
            {
                _longNick = value;
                OnPropertyChanged(nameof(LongNick));
            }
        }

        public int BirthdayYear
        {
            get => _birthdayYear;
            set
            {
                _birthdayYear = value;
                OnPropertyChanged(nameof(BirthdayYear));
            }
        }

        public int BirthdayMonth
        {
            get => _birthdayMonth;
            set
            {
                _birthdayMonth = value;
                OnPropertyChanged(nameof(BirthdayMonth));
            }
        }

        public int BirthdayDay
        {
            get => _birthdayDay;
            set
            {
                _birthdayDay = value;
                OnPropertyChanged(nameof(BirthdayDay));
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

        public string Sex
        {
            get => _sex;
            set
            {
                _sex = value;
                OnPropertyChanged(nameof(Sex));
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged(nameof(Email));
            }
        }

        public string PhoneNum
        {
            get => _phoneNum;
            set
            {
                _phoneNum = value;
                OnPropertyChanged(nameof(PhoneNum));
            }
        }

        public long CategoryId
        {
            get => _categoryId;
            set
            {
                _categoryId = value;
                OnPropertyChanged(nameof(CategoryId));
            }
        }

        public long RichTime
        {
            get => _richTime;
            set
            {
                _richTime = value;
                OnPropertyChanged(nameof(RichTime));
            }
        }

        public string Uid
        {
            get => _uid;
            set
            {
                _uid = value;
                OnPropertyChanged(nameof(Uid));
            }
        }

        public string Uin
        {
            get => _uin;
            set
            {
                _uin = value;
                OnPropertyChanged(nameof(Uin));
            }
        }

        public string Nick
        {
            get => _nick;
            set
            {
                _nick = value;
                OnPropertyChanged(nameof(Nick));
            }
        }

        public string Remark
        {
            get => _remark;
            set
            {
                _remark = value;
                OnPropertyChanged(nameof(Remark));
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
            }
        }

        public int Level
        {
            get => _level;
            set
            {
                _level = value;
                OnPropertyChanged(nameof(Level));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}