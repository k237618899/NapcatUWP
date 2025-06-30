using System.Collections.Generic;
using System.ComponentModel;

namespace NapcatUWP.Models
{
    public class FriendCategory : INotifyPropertyChanged
    {
        private List<FriendInfo> _buddyList;
        private long _categoryId;
        private int _categoryMbCount;
        private string _categoryName;

        private long _categorySortId;

        // 在 FriendCategory 類中添加展開狀態屬性
        private bool _isExpanded = true; // 默認展開
        private int _onlineCount;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
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

        public long CategorySortId
        {
            get => _categorySortId;
            set
            {
                _categorySortId = value;
                OnPropertyChanged(nameof(CategorySortId));
            }
        }

        public string CategoryName
        {
            get => _categoryName;
            set
            {
                _categoryName = value;
                OnPropertyChanged(nameof(CategoryName));
            }
        }

        public int CategoryMbCount
        {
            get => _categoryMbCount;
            set
            {
                _categoryMbCount = value;
                OnPropertyChanged(nameof(CategoryMbCount));
            }
        }

        public int OnlineCount
        {
            get => _onlineCount;
            set
            {
                _onlineCount = value;
                OnPropertyChanged(nameof(OnlineCount));
            }
        }

        public List<FriendInfo> BuddyList
        {
            get => _buddyList;
            set
            {
                _buddyList = value;
                OnPropertyChanged(nameof(BuddyList));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}