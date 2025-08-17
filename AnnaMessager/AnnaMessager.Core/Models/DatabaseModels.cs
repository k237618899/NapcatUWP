using System;

namespace AnnaMessager.Core.Models
{
    /// <summary>
    ///     賬號信息實體
    /// </summary>
    public class AccountEntity
    {
        public int Id { get; set; }
        public string Account { get; set; }
        public string AccessToken { get; set; }
        public string Nickname { get; set; }
        public string Avatar { get; set; }
        public DateTime LastLoginTime { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    ///     伺服器設定實體
    /// </summary>
    public class ServerEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ServerUrl { get; set; }
        public int ConnectionTimeout { get; set; }
        public bool EnableSsl { get; set; }
        public bool AutoReconnect { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    ///     聊天緩存實體
    /// </summary>
    public class ChatCacheEntity
    {
        public int Id { get; set; }
        public long ChatId { get; set; }
        public bool IsGroup { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public string LastMessageType { get; set; }
        public string ChatData { get; set; } // JSON 格式儲存完整 ChatItem
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    ///     聯絡人緩存實體
    /// </summary>
    public partial class ContactCacheEntity
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public string Nickname { get; set; }
        public string Remark { get; set; }
        public string Avatar { get; set; }
        public string Status { get; set; }
        public int CategoryId { get; set; } // 新增分類ID
        public string CategoryName { get; set; } // 新增分類名稱
        public string ContactData { get; set; } // JSON 格式儲存完整 ContactItem
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    ///     群組緩存實體
    /// </summary>
    public class GroupCacheEntity
    {
        public int Id { get; set; }
        public long GroupId { get; set; }
        public string GroupName { get; set; }
        public string GroupAvatar { get; set; }
        public int MemberCount { get; set; }
        public string GroupData { get; set; } // JSON 格式儲存完整 GroupItem
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    ///     應用程式設定實體
    /// </summary>
    public class AppSettingsEntity
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    ///     分類緩存實體
    /// </summary>
    public class CategoryCacheEntity
    {
        public int Id { get; set; }
        public long CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int SortOrder { get; set; }
        public int TotalCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}