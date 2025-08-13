using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Popups;
using AnnaMessager.Core.Services;

namespace AnnaMessager.UWP.Services
{
    /// <summary>
    ///     UWP 平台用戶交互服務實現
    /// </summary>
    public class UwpUserInteractionService : IUserInteractionService
    {
        public async Task ShowAlertAsync(string title, string message, string buttonText = "確定")
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    var dialog = new MessageDialog(message, title);
                    dialog.Commands.Add(new UICommand(buttonText));
                    await dialog.ShowAsync();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示警告對話框失敗: {ex.Message}");
            }
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "確定",
            string cancelText = "取消")
        {
            try
            {
                var result = false;

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    var dialog = new MessageDialog(message, title);

                    var confirmCommand = new UICommand(confirmText, cmd => { result = true; });
                    var cancelCommand = new UICommand(cancelText, cmd => { result = false; });

                    dialog.Commands.Add(confirmCommand);
                    dialog.Commands.Add(cancelCommand);
                    dialog.DefaultCommandIndex = 0;
                    dialog.CancelCommandIndex = 1;

                    await dialog.ShowAsync();
                });

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示確認對話框失敗: {ex.Message}");
                return false;
            }
        }

        public async Task<string> ShowInputAsync(string title, string message, string placeholder = "",
            string defaultValue = "")
        {
            try
            {
                // UWP 的 MessageDialog 不支持輸入框，這裡先返回默認值
                // 在實際應用中需要創建自定義對話框
                await ShowAlertAsync(title, message + "\n(輸入功能需要自定義對話框)");
                return defaultValue;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示輸入對話框失敗: {ex.Message}");
                return defaultValue;
            }
        }

        public async Task<int> ShowActionSheetAsync(string title, string message, params string[] options)
        {
            try
            {
                var selectedIndex = -1;

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    var dialog = new MessageDialog(message, title);

                    for (var i = 0; i < options.Length; i++)
                    {
                        var index = i; // 捕獲索引
                        dialog.Commands.Add(new UICommand(options[i], cmd => { selectedIndex = index; }));
                    }

                    await dialog.ShowAsync();
                });

                return selectedIndex;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示選擇對話框失敗: {ex.Message}");
                return -1;
            }
        }

        public async Task ShowLoadingAsync(string message = "載入中...")
        {
            try
            {
                // UWP 中需要自定義載入對話框，這裡僅做日誌記錄
                Debug.WriteLine($"顯示載入對話框: {message}");
                await Task.Delay(1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示載入對話框失敗: {ex.Message}");
            }
        }

        public async Task HideLoadingAsync()
        {
            try
            {
                Debug.WriteLine("隱藏載入對話框");
                await Task.Delay(1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"隱藏載入對話框失敗: {ex.Message}");
            }
        }
    }
}