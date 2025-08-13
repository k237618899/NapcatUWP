using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AnnaMessager.Core.Services;
using Foundation;
using UserNotifications;

namespace AnnaMessager.iOS.Services
{
    /// <summary>
    ///     iOS 平台通知服務實現
    /// </summary>
    public class iOSNotificationService : INotificationService
    {
        public async Task ShowMessageNotificationAsync(NotificationInfo notificationInfo)
        {
            try
            {
                await Task.Run(() =>
                {
#if __IOS__
                    var content = new UNMutableNotificationContent();
                    content.Title = notificationInfo.Title ?? "新消息";
                    content.Subtitle = notificationInfo.SenderName ?? "";
                    content.Body = notificationInfo.Message ?? "";
                    content.Sound = UNNotificationSound.Default;

                    // 設置用戶信息
                    content.UserInfo = new NSDictionary(
                        "chat_id", notificationInfo.ChatId.ToString(),
                        "is_group", notificationInfo.IsGroup.ToString()
                    );

                    var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);
                    var request = UNNotificationRequest.FromIdentifier(
                        $"message_{notificationInfo.ChatId}_{notificationInfo.IsGroup}",
                        content,
                        trigger
                    );

                    UNUserNotificationCenter.Current.AddNotificationRequest(request, error =>
                    {
                        if (error != null) Debug.WriteLine($"iOS 通知請求失敗: {error.LocalizedDescription}");
                    });
#endif
                });

                Debug.WriteLine($"顯示消息通知: {notificationInfo.Title} - {notificationInfo.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示消息通知失敗: {ex.Message}");
            }
        }

        public async Task ShowSystemNotificationAsync(string title, string message,
            NotificationType type = NotificationType.Info)
        {
            try
            {
                await Task.Run(() =>
                {
#if __IOS__
                    var content = new UNMutableNotificationContent();
                    content.Title = title;
                    content.Body = message;
                    content.Sound = UNNotificationSound.Default;

                    var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);
                    var request = UNNotificationRequest.FromIdentifier(
                        $"system_{DateTime.Now.Ticks}",
                        content,
                        trigger
                    );

                    UNUserNotificationCenter.Current.AddNotificationRequest(request, error =>
                    {
                        if (error != null) Debug.WriteLine($"iOS 系統通知請求失敗: {error.LocalizedDescription}");
                    });
#endif
                });

                Debug.WriteLine($"顯示系統通知: {title} - {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示系統通知失敗: {ex.Message}");
            }
        }

        public async Task ShowToastAsync(string message, ToastType type = ToastType.Info, int durationMs = 3000)
        {
            try
            {
                // iOS 沒有內建的 Toast，使用 Alert 或自定義 HUD
                var title = GetToastTitle(type);
                await ShowSystemNotificationAsync(title, message, GetNotificationType(type));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示 Toast 失敗: {ex.Message}");
            }
        }

        public async Task ClearAllNotificationsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
#if __IOS__
                    UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
                    UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
#endif
                });

                Debug.WriteLine("清除所有通知");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除所有通知失敗: {ex.Message}");
            }
        }

        public async Task ClearChatNotificationsAsync(long chatId, bool isGroup)
        {
            try
            {
                await Task.Run(() =>
                {
#if __IOS__
                    var identifier = $"message_{chatId}_{isGroup}";
                    UNUserNotificationCenter.Current.RemoveDeliveredNotifications(new[] { identifier });
                    UNUserNotificationCenter.Current.RemovePendingNotificationRequests(new[] { identifier });
#endif
                });

                Debug.WriteLine($"清除聊天通知: ChatId={chatId}, IsGroup={isGroup}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除聊天通知失敗: {ex.Message}");
            }
        }

        public async Task<bool> CheckNotificationPermissionAsync()
        {
            try
            {
                var result = false;

                await Task.Run(() =>
                {
#if __IOS__
                    var semaphore = new SemaphoreSlim(0, 1);

                    UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
                    {
                        result = settings.AuthorizationStatus == UNAuthorizationStatus.Authorized;
                        semaphore.Release();
                    });

                    semaphore.Wait();
#endif
                });

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"檢查通知權限失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RequestNotificationPermissionAsync()
        {
            try
            {
                var result = false;

                await Task.Run(() =>
                {
#if __IOS__
                    var semaphore = new SemaphoreSlim(0, 1);

                    UNUserNotificationCenter.Current.RequestAuthorization(
                        UNAuthorizationOptions.Alert |
                        UNAuthorizationOptions.Badge |
                        UNAuthorizationOptions.Sound,
                        (granted, error) =>
                        {
                            result = granted && error == null;
                            if (error != null) Debug.WriteLine($"請求通知權限失敗: {error.LocalizedDescription}");
                            semaphore.Release();
                        }
                    );

                    semaphore.Wait();
#endif
                });

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求通知權限失敗: {ex.Message}");
                return false;
            }
        }

        private string GetToastTitle(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return "成功";
                case ToastType.Warning:
                    return "警告";
                case ToastType.Error:
                    return "錯誤";
                default:
                    return "通知";
            }
        }

        private NotificationType GetNotificationType(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return NotificationType.Success;
                case ToastType.Warning:
                    return NotificationType.Warning;
                case ToastType.Error:
                    return NotificationType.Error;
                default:
                    return NotificationType.Info;
            }
        }
    }
}