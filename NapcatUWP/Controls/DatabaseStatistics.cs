namespace NapcatUWP.Models
{
    public class DatabaseStatistics
    {
        public int TotalMessages { get; set; }
        public int TotalGroups { get; set; }
        public int TotalFriends { get; set; }
        public int TotalCategories { get; set; }
        public int TotalGroupMembers { get; set; }
        public int TotalChatListItems { get; set; }
        public int TotalSettings { get; set; }
        public long DatabaseSize { get; set; } // 实际占用的数据库大小

        // 头像缓存统计
        public int TotalAvatars { get; set; }
        public long AvatarCacheSize { get; set; }

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