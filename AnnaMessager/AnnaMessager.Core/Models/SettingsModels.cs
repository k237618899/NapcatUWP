using MvvmCross.Core.ViewModels;

namespace AnnaMessager.Core.Models
{
    public class AppSettings : MvxNotifyPropertyChanged
    {
        private string _accessToken;
        private bool _autoLogin;
        private string _downloadPath;
        private bool _enableNotifications;
        private bool _enableSounds;
        private bool _enableVibration;
        private int _maxAvatarCacheSize;
        private int _maxImageCacheSize;
        private int _messageCachedays;
        private string _serverUrl;

        public string ServerUrl
        {
            get => _serverUrl;
            set => SetProperty(ref _serverUrl, value);
        }

        public string AccessToken
        {
            get => _accessToken;
            set => SetProperty(ref _accessToken, value);
        }

        public bool AutoLogin
        {
            get => _autoLogin;
            set => SetProperty(ref _autoLogin, value);
        }

        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => SetProperty(ref _enableNotifications, value);
        }

        public bool EnableSounds
        {
            get => _enableSounds;
            set => SetProperty(ref _enableSounds, value);
        }

        public bool EnableVibration
        {
            get => _enableVibration;
            set => SetProperty(ref _enableVibration, value);
        }

        public string DownloadPath
        {
            get => _downloadPath;
            set => SetProperty(ref _downloadPath, value);
        }

        public int MaxImageCacheSize
        {
            get => _maxImageCacheSize;
            set => SetProperty(ref _maxImageCacheSize, value);
        }

        public int MaxAvatarCacheSize
        {
            get => _maxAvatarCacheSize;
            set => SetProperty(ref _maxAvatarCacheSize, value);
        }

        public int MessageCacheDays
        {
            get => _messageCachedays;
            set => SetProperty(ref _messageCachedays, value);
        }
    }

    /// <summary>
    ///     伺服器設定模型 - 從原有設定中分離出來
    /// </summary>
    public class ServerSettings : MvxNotifyPropertyChanged
    {
        private string _accessToken;
        private string _account;
        private bool _autoReconnect;
        private int _connectionTimeout;
        private bool _enableSsl;
        private string _serverUrl;

        public string ServerUrl
        {
            get => _serverUrl;
            set => SetProperty(ref _serverUrl, value);
        }

        public string Account
        {
            get => _account;
            set => SetProperty(ref _account, value);
        }

        public string AccessToken
        {
            get => _accessToken;
            set => SetProperty(ref _accessToken, value);
        }

        public int ConnectionTimeout
        {
            get => _connectionTimeout;
            set => SetProperty(ref _connectionTimeout, value);
        }

        public bool EnableSsl
        {
            get => _enableSsl;
            set => SetProperty(ref _enableSsl, value);
        }

        public bool AutoReconnect
        {
            get => _autoReconnect;
            set => SetProperty(ref _autoReconnect, value);
        }
    }

    public class CacheInfo : MvxNotifyPropertyChanged
    {
        private long _avatarsCacheSize;
        private long _imagesCacheSize;
        private long _messagesCacheSize;
        private int _totalAvatars;
        private int _totalImages;
        private int _totalMessages;
        private long _totalSize;

        public long TotalSize
        {
            get => _totalSize;
            set => SetProperty(ref _totalSize, value);
        }

        public long ImagesCacheSize
        {
            get => _imagesCacheSize;
            set => SetProperty(ref _imagesCacheSize, value);
        }

        public long AvatarsCacheSize
        {
            get => _avatarsCacheSize;
            set => SetProperty(ref _avatarsCacheSize, value);
        }

        public long MessagesCacheSize
        {
            get => _messagesCacheSize;
            set => SetProperty(ref _messagesCacheSize, value);
        }

        public int TotalMessages
        {
            get => _totalMessages;
            set => SetProperty(ref _totalMessages, value);
        }

        public int TotalImages
        {
            get => _totalImages;
            set => SetProperty(ref _totalImages, value);
        }

        public int TotalAvatars
        {
            get => _totalAvatars;
            set => SetProperty(ref _totalAvatars, value);
        }

        // 格式化大小顯示
        public string TotalSizeDisplay => FormatBytes(TotalSize);
        public string ImagesCacheSizeDisplay => FormatBytes(ImagesCacheSize);
        public string AvatarsCacheSizeDisplay => FormatBytes(AvatarsCacheSize);
        public string MessagesCacheSizeDisplay => FormatBytes(MessagesCacheSize);

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }

    public class LoginCredentials
    {
        public string ServerUrl { get; set; }
        public string AccessToken { get; set; }
        public bool RememberCredentials { get; set; }
    }
}