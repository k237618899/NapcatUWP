using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NapcatUWP.Models
{
    public class DatabaseStatistics : INotifyPropertyChanged
    {
        private int _totalMessages;
        private int _totalGroups;
        private int _totalFriends;
        private int _totalCategories;
        private int _totalGroupMembers;
        private int _totalChatListItems;
        private int _totalSettings;
        private long _databaseSize;

        public int TotalMessages
        {
            get => _totalMessages;
            set
            {
                _totalMessages = value;
                OnPropertyChanged();
            }
        }

        public int TotalGroups
        {
            get => _totalGroups;
            set
            {
                _totalGroups = value;
                OnPropertyChanged();
            }
        }

        public int TotalFriends
        {
            get => _totalFriends;
            set
            {
                _totalFriends = value;
                OnPropertyChanged();
            }
        }

        public int TotalCategories
        {
            get => _totalCategories;
            set
            {
                _totalCategories = value;
                OnPropertyChanged();
            }
        }

        public int TotalGroupMembers
        {
            get => _totalGroupMembers;
            set
            {
                _totalGroupMembers = value;
                OnPropertyChanged();
            }
        }

        public int TotalChatListItems
        {
            get => _totalChatListItems;
            set
            {
                _totalChatListItems = value;
                OnPropertyChanged();
            }
        }

        public int TotalSettings
        {
            get => _totalSettings;
            set
            {
                _totalSettings = value;
                OnPropertyChanged();
            }
        }

        public long DatabaseSize
        {
            get => _databaseSize;
            set
            {
                _databaseSize = value;
                OnPropertyChanged();
            }
        }

        public string DatabaseSizeFormatted => FormatFileSize(DatabaseSize);

        private string FormatFileSize(long bytes)
        {
            const int scale = 1024;
            string[] orders = { "B", "KB", "MB", "GB", "TB" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);
                max /= scale;
            }

            return "0 B";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}