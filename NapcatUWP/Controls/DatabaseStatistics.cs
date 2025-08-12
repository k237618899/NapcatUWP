namespace NapcatUWP.Models
{
    /// <summary>
    ///     数据库统计信息类 - UWP 15063 兼容版本
    /// </summary>
    public class DatabaseStatistics
    {
        public int TotalMessages { get; set; }
        public int TotalGroups { get; set; }
        public int TotalFriends { get; set; }
        public int TotalCategories { get; set; }
        public int TotalGroupMembers { get; set; }
        public int TotalChatListItems { get; set; }
        public int TotalSettings { get; set; }
        public long DatabaseSize { get; set; }

        // 头像缓存统计
        public int TotalAvatars { get; set; }
        public long AvatarCacheSize { get; set; }

        // 格式化的大小字符串属性
        public string DatabaseSizeFormatted => FormatBytes(DatabaseSize);
        public string AvatarCacheSizeFormatted => FormatBytes(AvatarCacheSize);

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            return $"{bytes / (1024 * 1024):F1} MB";
        }
    }
}