using System.ComponentModel;

namespace NapcatUWP.Models
{
    public class GroupInfo : INotifyPropertyChanged
    {
        private bool _groupAllShut;
        private long _groupId;
        private string _groupName;
        private string _groupRemark;
        private int _maxMemberCount;
        private int _memberCount;

        public long GroupId
        {
            get => _groupId;
            set
            {
                _groupId = value;
                OnPropertyChanged(nameof(GroupId));
            }
        }

        public string GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                OnPropertyChanged(nameof(GroupName));
            }
        }

        public string GroupRemark
        {
            get => _groupRemark;
            set
            {
                _groupRemark = value;
                OnPropertyChanged(nameof(GroupRemark));
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

        public int MaxMemberCount
        {
            get => _maxMemberCount;
            set
            {
                _maxMemberCount = value;
                OnPropertyChanged(nameof(MaxMemberCount));
            }
        }

        public bool GroupAllShut
        {
            get => _groupAllShut;
            set
            {
                _groupAllShut = value;
                OnPropertyChanged(nameof(GroupAllShut));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}