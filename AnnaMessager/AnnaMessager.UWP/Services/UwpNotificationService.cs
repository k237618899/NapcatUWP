using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Xml.Dom;
using Windows.UI.Core;
using Windows.UI.Notifications;
using AnnaMessager.Core.Services;

namespace AnnaMessager.UWP.Services
{
    /// <summary>
    ///     UWP 平台通知服務實現
    /// </summary>
    public class UwpNotificationService : INotificationService
    {
        private readonly ToastNotifier _toastNotifier;

        public UwpNotificationService()
        {
            try
            {
                _toastNotifier = ToastNotificationManager.CreateToastNotifier();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化 UWP 通知服務失敗: {ex.Message}");
            }
        }

        public async Task ShowMessageNotificationAsync(NotificationInfo notificationInfo)
        {
            try
            {
                if (_toastNotifier == null) return;

                // 創建 Toast XML 模板
                var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText04);

                // 設置文本內容
                var textElements = toastXml.GetElementsByTagName("text");
                if (textElements.Length >= 1)
                    textElements[0].AppendChild(toastXml.CreateTextNode(notificationInfo.Title ?? "新消息"));
                if (textElements.Length >= 2)
                    textElements[1].AppendChild(toastXml.CreateTextNode(notificationInfo.SenderName ?? ""));
                if (textElements.Length >= 3)
                    textElements[2].AppendChild(toastXml.CreateTextNode(notificationInfo.Message ?? ""));

                // 設置啟動參數
                var toastElement = toastXml.SelectSingleNode("/toast") as XmlElement;
                toastElement?.SetAttribute("launch", $"chat_{notificationInfo.ChatId}_{notificationInfo.IsGroup}");

                // 創建並顯示通知
                var toast = new ToastNotification(toastXml);

                // 設置過期時間
                toast.ExpirationTime = DateTime.Now.AddHours(1);

                // 顯示通知
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { _toastNotifier.Show(toast); });

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
                if (_toastNotifier == null) return;

                // 根據類型選擇模板和圖標
                var templateType = ToastTemplateType.ToastText02;
                var toastXml = ToastNotificationManager.GetTemplateContent(templateType);

                // 設置文本內容
                var textElements = toastXml.GetElementsByTagName("text");
                if (textElements.Length >= 1) textElements[0].AppendChild(toastXml.CreateTextNode(title));
                if (textElements.Length >= 2) textElements[1].AppendChild(toastXml.CreateTextNode(message));

                // 創建並顯示通知
                var toast = new ToastNotification(toastXml);
                toast.ExpirationTime = DateTime.Now.AddMinutes(5);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { _toastNotifier.Show(toast); });

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
                // UWP 中 Toast 通知就是系統通知，所以這裡可以複用
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
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { ToastNotificationManager.History.Clear(); });

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
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var group = $"chat_{chatId}_{isGroup}";
                    ToastNotificationManager.History.RemoveGroup(group);
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
                return await Task.Run(() =>
                {
                    // UWP 應用默認有通知權限，除非用戶明確禁用
                    return true;
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
                // UWP 不需要額外請求權限
                return await Task.FromResult(true);
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