using System;
using System.Threading.Tasks;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     通知服務接口 - 統一管理跨平台通知顯示
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        ///     顯示新消息通知
        /// </summary>
        Task ShowMessageNotificationAsync(NotificationInfo notificationInfo);

        /// <summary>
        ///     顯示系統通知
        /// </summary>
        Task ShowSystemNotificationAsync(string title, string message, NotificationType type = NotificationType.Info);

        /// <summary>
        ///     顯示 Toast 通知
        /// </summary>
        Task ShowToastAsync(string message, ToastType type = ToastType.Info, int durationMs = 3000);

        /// <summary>
        ///     清除所有通知
        /// </summary>
        Task ClearAllNotificationsAsync();

        /// <summary>
        ///     清除指定聊天的通知
        /// </summary>
        Task ClearChatNotificationsAsync(long chatId, bool isGroup);

        /// <summary>
        ///     檢查通知權限
        /// </summary>
        Task<bool> CheckNotificationPermissionAsync();

        /// <summary>
        ///     請求通知權限
        /// </summary>
        Task<bool> RequestNotificationPermissionAsync();
    }

    /// <summary>
    ///     通知信息模型
    /// </summary>
    public class NotificationInfo
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string SenderName { get; set; }
        public string AvatarUrl { get; set; }
        public long ChatId { get; set; }
        public bool IsGroup { get; set; }
        public DateTime Time { get; set; }
        public string ChatName { get; set; }
    }

    /// <summary>
    ///     通知類型
    /// </summary>
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    ///     Toast 類型
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
}