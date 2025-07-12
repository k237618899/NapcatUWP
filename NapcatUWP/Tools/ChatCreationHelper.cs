using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using NapcatUWP.Controls;
using NapcatUWP.Models;
using NapcatUWP.Pages;

namespace NapcatUWP.Tools
{
    /// <summary>
    ///     聊天創建輔助類 - 用於從好友和群組創建聊天
    /// </summary>
    public static class ChatCreationHelper
    {
        /// <summary>
        ///     從好友創建聊天
        /// </summary>
        /// <param name="friendInfo">好友信息</param>
        /// <param name="mainView">主界面引用</param>
        /// <param name="webSocketClient">WebSocket客戶端</param>
        /// <returns>是否成功創建</returns>
        public static async Task<bool> CreateChatFromFriendAsync(FriendInfo friendInfo, MainView mainView,
            WebSocketClientStarter webSocketClient)
        {
            try
            {
                if (friendInfo == null)
                {
                    Debug.WriteLine("創建好友聊天: 好友信息為空");
                    return false;
                }

                var currentAccount = DataAccess.GetCurrentAccount();
                if (string.IsNullOrEmpty(currentAccount))
                {
                    Debug.WriteLine("創建好友聊天: 當前帳號為空");
                    return false;
                }

                // 檢查聊天是否已存在
                var chatExists = DataAccess.CheckChatExists(currentAccount, friendInfo.UserId, false);
                if (chatExists)
                {
                    Debug.WriteLine($"好友聊天已存在，直接導航: {friendInfo.Nickname} ({friendInfo.UserId})");

                    // 如果聊天已存在，直接導航到聊天界面
                    await NavigateToChat(friendInfo.UserId, false, mainView);
                    return true;
                }

                // 創建新的聊天項目
                var chatItem = DataAccess.CreateChatFromFriend(friendInfo);
                if (chatItem == null)
                {
                    Debug.WriteLine("創建好友聊天項目失敗");
                    return false;
                }

                // 添加到聊天列表緩存
                DataAccess.AddChatToCache(currentAccount, chatItem);

                // 如果是新聊天，請求歷史消息
                await RequestFriendHistoryMessages(friendInfo.UserId, webSocketClient, mainView);

                // 導航到聊天界面
                await NavigateToChat(friendInfo.UserId, false, mainView);

                Debug.WriteLine($"成功創建好友聊天: {friendInfo.Nickname} ({friendInfo.UserId})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"創建好友聊天錯誤: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     從群組創建聊天
        /// </summary>
        /// <param name="groupInfo">群組信息</param>
        /// <param name="mainView">主界面引用</param>
        /// <param name="webSocketClient">WebSocket客戶端</param>
        /// <returns>是否成功創建</returns>
        public static async Task<bool> CreateChatFromGroupAsync(GroupInfo groupInfo, MainView mainView,
            WebSocketClientStarter webSocketClient)
        {
            try
            {
                if (groupInfo == null)
                {
                    Debug.WriteLine("創建群組聊天: 群組信息為空");
                    return false;
                }

                var currentAccount = DataAccess.GetCurrentAccount();
                if (string.IsNullOrEmpty(currentAccount))
                {
                    Debug.WriteLine("創建群組聊天: 當前帳號為空");
                    return false;
                }

                // 檢查聊天是否已存在
                var chatExists = DataAccess.CheckChatExists(currentAccount, groupInfo.GroupId, true);
                if (chatExists)
                {
                    Debug.WriteLine($"群組聊天已存在，直接導航: {groupInfo.GroupName} ({groupInfo.GroupId})");

                    // 如果聊天已存在，直接導航到聊天界面
                    await NavigateToChat(groupInfo.GroupId, true, mainView);
                    return true;
                }

                // 創建新的聊天項目
                var chatItem = DataAccess.CreateChatFromGroup(groupInfo);
                if (chatItem == null)
                {
                    Debug.WriteLine("創建群組聊天項目失敗");
                    return false;
                }

                // 添加到聊天列表緩存
                DataAccess.AddChatToCache(currentAccount, chatItem);

                // 如果是新聊天，請求歷史消息
                await RequestGroupHistoryMessages(groupInfo.GroupId, webSocketClient, mainView);

                // 導航到聊天界面
                await NavigateToChat(groupInfo.GroupId, true, mainView);

                Debug.WriteLine($"成功創建群組聊天: {groupInfo.GroupName} ({groupInfo.GroupId})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"創建群組聊天錯誤: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     請求好友歷史消息
        /// </summary>
        /// <param name="userId">好友用戶ID</param>
        /// <param name="webSocketClient">WebSocket客戶端</param>
        /// <param name="mainView">主界面引用</param>
        private static async Task RequestFriendHistoryMessages(long userId, WebSocketClientStarter webSocketClient,
            MainView mainView)
        {
            try
            {
                if (webSocketClient == null || !webSocketClient.IsConnected)
                {
                    Debug.WriteLine("請求好友歷史消息: WebSocket未連接");
                    return;
                }

                var requestJson = DataAccess.CreateGetFriendMsgHistoryRequest(userId);
                if (string.IsNullOrEmpty(requestJson))
                {
                    Debug.WriteLine("請求好友歷史消息: 創建請求失敗");
                    return;
                }

                // 發送API請求
                await webSocketClient._socket.Send(requestJson);
                Debug.WriteLine($"已發送好友歷史消息請求: UserId={userId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求好友歷史消息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     請求群組歷史消息
        /// </summary>
        /// <param name="groupId">群組ID</param>
        /// <param name="webSocketClient">WebSocket客戶端</param>
        /// <param name="mainView">主界面引用</param>
        private static async Task RequestGroupHistoryMessages(long groupId, WebSocketClientStarter webSocketClient,
            MainView mainView)
        {
            try
            {
                if (webSocketClient == null || !webSocketClient.IsConnected)
                {
                    Debug.WriteLine("請求群組歷史消息: WebSocket未連接");
                    return;
                }

                var requestJson = DataAccess.CreateGetGroupMsgHistoryRequest(groupId);
                if (string.IsNullOrEmpty(requestJson))
                {
                    Debug.WriteLine("請求群組歷史消息: 創建請求失敗");
                    return;
                }

                // 發送API請求
                await webSocketClient._socket.Send(requestJson);
                Debug.WriteLine($"已發送群組歷史消息請求: GroupId={groupId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求群組歷史消息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     導航到聊天界面 - 修正版
        /// </summary>
        /// <param name="chatId">聊天ID</param>
        /// <param name="isGroup">是否為群組</param>
        /// <param name="mainView">主界面引用</param>
        private static async Task NavigateToChat(long chatId, bool isGroup, MainView mainView)
        {
            try
            {
                if (mainView == null)
                {
                    Debug.WriteLine("導航到聊天界面: MainView引用為空");
                    return;
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () =>
                    {
                        try
                        {
                            // 1. 首先切換到聊天頁面並刷新聊天列表
                            mainView.SwitchPagePublic("Chats");
                            mainView.RefreshChatList();

                            Debug.WriteLine($"已切換到聊天頁面，開始查找聊天項目: ChatId={chatId}, IsGroup={isGroup}");

                            // 2. 短暫延遲以確保頁面和數據加載完成
                            Task.Delay(100).ContinueWith(_ =>
                            {
                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                    () =>
                                    {
                                        try
                                        {
                                            // 3. 查找對應的聊天項目
                                            var targetChatItem = mainView.ChatItems.FirstOrDefault(c =>
                                                c.ChatId == chatId && c.IsGroup == isGroup);

                                            if (targetChatItem != null)
                                            {
                                                Debug.WriteLine($"找到目標聊天項目: {targetChatItem.Name}");

                                                // 4. 直接打開聊天界面
                                                mainView.OpenChatDirectly(targetChatItem);

                                                Debug.WriteLine($"成功導航到聊天界面: ChatId={chatId}, IsGroup={isGroup}");
                                            }
                                            else
                                            {
                                                Debug.WriteLine($"無法找到對應的聊天項目: ChatId={chatId}, IsGroup={isGroup}");
                                                Debug.WriteLine($"當前聊天列表項目數: {mainView.ChatItems.Count}");

                                                // 再次嘗試刷新並查找
                                                mainView.RefreshChatList();

                                                Task.Delay(200).ContinueWith(__ =>
                                                {
                                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                                        CoreDispatcherPriority.Normal, () =>
                                                        {
                                                            var retryItem = mainView.ChatItems.FirstOrDefault(c =>
                                                                c.ChatId == chatId && c.IsGroup == isGroup);

                                                            if (retryItem != null)
                                                            {
                                                                Debug.WriteLine($"重試找到聊天項目: {retryItem.Name}");
                                                                mainView.OpenChatDirectly(retryItem);
                                                            }
                                                            else
                                                            {
                                                                Debug.WriteLine("重試後仍無法找到聊天項目，保持在聊天列表");
                                                            }
                                                        });
                                                });
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"延遲導航時發生錯誤: {ex.Message}");
                                        }
                                    });
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"導航到聊天界面時發生錯誤: {ex.Message}");
                            // 發生錯誤時至少確保在聊天頁面
                            mainView.SwitchPagePublic("Chats");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"導航到聊天界面錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理歷史消息響應 - 增強版，支持錯誤處理
        /// </summary>
        /// <param name="responseJson">API響應JSON</param>
        /// <param name="echo">請求的echo標識</param>
        public static void ProcessHistoryMessageResponse(string responseJson, string echo)
        {
            try
            {
                if (string.IsNullOrEmpty(responseJson) || string.IsNullOrEmpty(echo))
                {
                    Debug.WriteLine("處理歷史消息響應: 參數無效");
                    return;
                }

                // 檢查響應是否包含錯誤信息
                if (responseJson.Contains("消息undefined不存在") || responseJson.Contains("\"status\":\"failed\""))
                {
                    Debug.WriteLine("歷史消息請求失敗，響應包含錯誤信息");
                    HandleHistoryMessageError(echo);
                    return;
                }

                // 解析echo以獲取聊天信息
                var echoParts = echo.Split('_');
                if (echoParts.Length < 3)
                {
                    Debug.WriteLine("處理歷史消息響應: echo格式無效");
                    return;
                }

                var chatType = echoParts[0]; // "friend" 或 "group"
                var chatIdStr = echoParts[2];

                if (!long.TryParse(chatIdStr, out var chatId))
                {
                    Debug.WriteLine("處理歷史消息響應: 無法解析chatId");
                    return;
                }

                var isGroup = chatType == "group";

                // 處理消息
                var messages = DataAccess.ProcessHistoryMessageResponse(responseJson, chatId, isGroup);
                if (messages.Count > 0)
                {
                    // 保存歷史消息到數據庫
                    DataAccess.SaveHistoryMessages(messages, chatId, isGroup);
                    Debug.WriteLine($"成功處理並保存 {messages.Count} 條歷史消息");
                }
                else
                {
                    Debug.WriteLine("歷史消息響應中沒有找到有效的消息");
                    // 即使沒有消息也要清除加載提示
                    HandleHistoryMessageError(echo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理歷史消息響應錯誤: {ex.Message}");
                // 發生錯誤時也要清除加載提示
                HandleHistoryMessageError(echo);
            }
        }

        /// <summary>
        ///     處理歷史消息錯誤情況
        /// </summary>
        /// <param name="echo">請求的echo標識</param>
        private static void HandleHistoryMessageError(string echo)
        {
            try
            {
                // 解析echo以獲取聊天信息
                var echoParts = echo.Split('_');
                if (echoParts.Length < 3)
                {
                    Debug.WriteLine("處理歷史消息錯誤: echo格式無效");
                    return;
                }

                var chatType = echoParts[0]; // "friend" 或 "group"
                var chatIdStr = echoParts[2];

                if (!long.TryParse(chatIdStr, out var chatId))
                {
                    Debug.WriteLine("處理歷史消息錯誤: 無法解析chatId");
                    return;
                }

                var isGroup = chatType == "group";

                Debug.WriteLine($"歷史消息請求失敗，清除加載提示: ChatId={chatId}, IsGroup={isGroup}");

                // 通知MainView清除加載提示並顯示空聊天窗
                Task.Run(async () =>
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                // 這裡需要MainView的引用，我們通過靜態方法或者其他方式來處理
                                // 由於我們無法直接訪問MainView實例，我們將在OneBotAPIHandler中處理
                                Debug.WriteLine($"需要清除聊天加載提示: ChatId={chatId}, IsGroup={isGroup}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"清除加載提示時發生錯誤: {ex.Message}");
                            }
                        });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理歷史消息錯誤時發生異常: {ex.Message}");
            }
        }
    }
}