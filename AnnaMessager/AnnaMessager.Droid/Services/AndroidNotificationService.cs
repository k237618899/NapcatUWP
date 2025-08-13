using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using AnnaMessager.Core.Services;
using Java.Lang;
using Debug = System.Diagnostics.Debug;
using Exception = System.Exception;

namespace AnnaMessager.Droid.Services
{
    /// <summary>
    ///     Android 平台通知服務實現
    /// </summary>
    public class AndroidNotificationService : INotificationService
    {
        private const string CHANNEL_ID = "anna_messager_notifications";
        private const string CHANNEL_NAME = "Anna Messager";
        private readonly Context _context;
        private readonly NotificationManager _notificationManager;

        public AndroidNotificationService()
        {
            try
            {
                _context = Application.Context;
                _notificationManager = _context.GetSystemService(Context.NotificationService) as NotificationManager;
                CreateNotificationChannel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化 Android 通知服務失敗: {ex.Message}");
            }
        }

        public async Task ShowMessageNotificationAsync(NotificationInfo notificationInfo)
        {
            try
            {
                if (_notificationManager == null || _context == null) return;

                await Task.Run(() =>
                {
#if __ANDROID__
                    var intent = new Intent(_context, typeof(MainActivity));
                    intent.PutExtra("chat_id", notificationInfo.ChatId);
                    intent.PutExtra("is_group", notificationInfo.IsGroup);

                    var pendingIntent =
                        PendingIntent.GetActivity(_context, 0, intent, PendingIntentFlags.UpdateCurrent);

                    var builder = new Notification.Builder(_context, CHANNEL_ID)
                        .SetContentTitle(notificationInfo.Title ?? "新消息")
                        .SetContentText(notificationInfo.Message)
                        .SetSubText(notificationInfo.SenderName)
                        .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
                        .SetContentIntent(pendingIntent)
                        .SetAutoCancel(true)
                        .SetWhen(JavaSystem.CurrentTimeMillis());

                    var notification = builder.Build();
                    _notificationManager.Notify((int)notificationInfo.ChatId, notification);
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
                if (_notificationManager == null || _context == null) return;

                await Task.Run(() =>
                {
#if __ANDROID__
                    var builder = new Notification.Builder(_context, CHANNEL_ID)
                        .SetContentTitle(title)
                        .SetContentText(message)
                        .SetSmallIcon(GetNotificationIcon(type))
                        .SetAutoCancel(true)
                        .SetWhen(JavaSystem.CurrentTimeMillis());

                    var notification = builder.Build();
                    _notificationManager.Notify(DateTime.Now.GetHashCode(), notification);
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
                await Task.Run(() =>
                {
#if __ANDROID__
                    Toast.MakeText(_context, message, ToastLength.Short).Show();
#endif
                });
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
                await Task.Run(() => { _notificationManager?.CancelAll(); });

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
                await Task.Run(() => { _notificationManager?.Cancel((int)chatId); });

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
                return await Task.Run(() =>
                {
#if __ANDROID__
                    return _notificationManager?.AreNotificationsEnabled() ?? false;
#else
                    return false;
#endif
                });
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
                // Android 通知權限通常在安裝時授予，這裡返回當前狀態
                return await CheckNotificationPermissionAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求通知權限失敗: {ex.Message}");
                return false;
            }
        }

        private void CreateNotificationChannel()
        {
#if __ANDROID__
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    var channel = new NotificationChannel(CHANNEL_ID, CHANNEL_NAME, NotificationImportance.High)
                    {
                        Description = "Anna Messager 應用通知"
                    };

                    _notificationManager?.CreateNotificationChannel(channel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"創建通知頻道失敗: {ex.Message}");
            }
#endif
        }

        private int GetNotificationIcon(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Success:
                    return Android.Resource.Drawable.IcDialogInfo;
                case NotificationType.Warning:
                    return Android.Resource.Drawable.IcDialogAlert;
                case NotificationType.Error:
                    return Android.Resource.Drawable.IcDialogAlert;
                default:
                    return Android.Resource.Drawable.IcDialogInfo;
            }
        }
    }
}