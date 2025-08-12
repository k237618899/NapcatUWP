using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.UI.Core;
using NapcatUWP.Models;
using NapcatUWP.Pages;
using NapcatUWP.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NapcatUWP.Controls.APIHandler
{
    internal class OneBotAPIHandler
    {
        // 用於追蹤正在請求的消息ID，避免重複請求
        private static readonly HashSet<long> _requestingMessages = new HashSet<long>();
        private long _currentUserId; // 當前用戶ID
        private bool _hasLoadedChatList; // 追蹤是否已載入聊天列表
        private bool _hasLoadedContacts; // 追蹤是否已載入聯絡人
        private bool _hasProcessedInitialLogin; // 追蹤是否已處理初始登入
        private MainView _mainView;

        public void SetMainView(MainView mainView)
        {
            _mainView = mainView;

            // 設置 ReplySegment 的靜態委託
            ReplySegment.RequestMessageContentDelegate = messageId =>
            {
                RequestMessageContent(messageId, $"get_msg_reply_{messageId}");
            };
        }

        /// <summary>
        ///     優化的初始化流程 - 按優先級順序載入
        /// </summary>
        private void OptimizedInitializationFlow()
        {
            try
            {
                if (_hasProcessedInitialLogin) return;

                _hasProcessedInitialLogin = true;

                // 第1階段：獲取登入信息（最高優先級）
                RequestLoginInfo();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化流程錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     請求登入信息
        /// </summary>
        private void RequestLoginInfo()
        {
            try
            {
                var loginAction = JSONTools.ActionToJSON("get_login_info", new JsonObject(), "login_info");
                MainPage.SocketClientStarter._socket.Send(loginAction);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求登入信息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     階段2：載入聊天列表和用戶頭像
        /// </summary>
        private async void LoadChatListAndUserAvatar()
        {
            try
            {
                if (_hasLoadedChatList || _currentUserId == 0) return;

                _hasLoadedChatList = true;

                // 並行載入用戶頭像和最近聯繫人
                var userAvatarTask = LoadCurrentUserAvatarAsync();
                var recentContactsTask = Task.Run(() => RequestRecentContacts());

                await Task.WhenAll(userAvatarTask, recentContactsTask);

                // 延遲500ms後載入聯絡人列表（避免同時大量請求）
                await Task.Delay(500);
                LoadContactsAndGroups();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聊天列表和用戶頭像錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     修復版智能合併聊天列表 - 確保頭像狀態正確保持
        /// </summary>
        public void SmartMergeChatList(List<ChatItem> newChatItems)
        {
            try
            {
                if (newChatItems == null || newChatItems.Count == 0) return;

                Debug.WriteLine($"開始智能合併聊天列表: {newChatItems.Count} 個新項目");

                // 通知 MainView 進行合併，並確保頭像狀態保持
                if (_mainView != null)
                    _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                // 使用 MainView 改進的合併方法
                                _mainView.SmartMergeRecentContacts(newChatItems);

                                // 確保立即觸發頭像預載入
                                _ = Task.Run(async () =>
                                {
                                    await Task.Delay(500); // 短暫延遲確保合併完成
                                    await PreloadMergedChatAvatarsAsync(newChatItems);
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"通知 MainView 合併聊天列表錯誤: {ex.Message}");
                            }
                        });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SmartMergeChatList 錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     修復版預載入合併後的聊天頭像 - 確保類型正確
        /// </summary>
        private async Task PreloadMergedChatAvatarsAsync(List<ChatItem> chatItems)
        {
            try
            {
                const int batchSize = 5;
                for (var i = 0; i < chatItems.Count; i += batchSize)
                {
                    var batch = chatItems.Skip(i).Take(batchSize);
                    var tasks = batch.Select(async item =>
                    {
                        try
                        {
                            // 重要修復：使用正確的頭像類型
                            var avatarType = item.IsGroup ? "group" : "friend";
                            var expectedCacheKey = $"{avatarType}_{item.ChatId}";

                            Debug.WriteLine(
                                $"預載入頭像: {item.Name} (ID: {item.ChatId}, IsGroup: {item.IsGroup}), CacheKey: {expectedCacheKey}");

                            var avatar = await AvatarManager.GetAvatarAsync(avatarType, item.ChatId, 2, true);

                            if (avatar != null)
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Low,
                                    () => { _mainView?.UpdateChatItemAvatar(item.ChatId, item.IsGroup, avatar); });

                                Debug.WriteLine($"✓ 預載入頭像成功: {expectedCacheKey}");
                            }
                            else
                            {
                                Debug.WriteLine($"✗ 預載入頭像失敗: {expectedCacheKey}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"預載入頭像錯誤: {item.ChatId}, {ex.Message}");
                        }
                    });

                    await Task.WhenAll(tasks);
                    await Task.Delay(100); // 批次間延遲
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"預載入合併頭像錯誤: {ex.Message}");
            }
        }


        /// <summary>
        ///     改進版聊天列表和頭像載入 - 階段2
        /// </summary>
        private async void LoadChatListAndUserAvatarOptimized()
        {
            try
            {
                Debug.WriteLine("OneBotAPIHandler: 階段2 - 開始載入聊天列表和用戶頭像（優化版）");

                // 首先嘗試從緩存載入
                var currentAccount = GetCurrentAccount();
                if (!string.IsNullOrEmpty(currentAccount))
                {
                    var cachedItems = DataAccess.LoadChatListCacheWithAvatars(currentAccount);
                    if (cachedItems.Count > 0)
                    {
                        Debug.WriteLine($"從緩存載入聊天列表: {cachedItems.Count} 個項目");

                        // 立即顯示緩存的聊天列表
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.High,
                            () =>
                            {
                                _mainView?.LoadRecentContactsToUI(ConvertChatItemsToRecentContacts(cachedItems));
                            });

                        // 後台繼續載入新數據並合併
                        RequestRecentContactsForMerging();
                    }
                    else
                    {
                        // 沒有緩存，直接請求新數據
                        RequestRecentContacts();
                    }
                }
                else
                {
                    RequestRecentContacts();
                }

                // 載入用戶頭像
                await RequestUserAvatarAsync();

                Debug.WriteLine("OneBotAPIHandler: 階段2 - 聊天列表和頭像載入完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadChatListAndUserAvatarOptimized 發生錯誤: {ex.Message}");
            }
        }


        // 修復 GetCurrentAccount 方法
        private string GetCurrentAccount()
        {
            try
            {
                var settings = DataAccess.GetAllDatas();
                return settings.Get("Account") ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取當前帳號錯誤: {ex.Message}");
                return "";
            }
        }

        // 修復 RequestUserAvatarAsync 方法
        private async Task RequestUserAvatarAsync()
        {
            try
            {
                if (_currentUserId == 0) return;

                // 載入當前用戶頭像
                var userAvatar = await AvatarManager.GetAvatarAsync("current", _currentUserId, 0, true);

                if (userAvatar != null)
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.High, () =>
                        {
                            try
                            {
                                // 更新側邊欄用戶頭像
                                _mainView?.UpdateUserAvatar(userAvatar);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"更新用戶頭像UI錯誤: {ex.Message}");
                            }
                        });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求用戶頭像錯誤: {ex.Message}");
            }
        }

        // 修復 _webSocket 引用問題 - 使用正確的 WebSocket 引用
        private void RequestRecentContactsForMerging()
        {
            try
            {
                var recentContactAction =
                    JSONTools.ActionToJSON("get_recent_contact", new JsonObject(), "recent_contact_merge");
                MainPage.SocketClientStarter._socket.Send(recentContactAction);
                Debug.WriteLine("已請求最近聯繫人數據用於合併");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求最近聯繫人合併數據時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     將 ChatItem 列表轉換為 RecentContactMessage 列表
        /// </summary>
        private List<RecentContactMessage> ConvertChatItemsToRecentContacts(List<ChatItem> chatItems)
        {
            var recentMessages = new List<RecentContactMessage>();

            try
            {
                foreach (var chatItem in chatItems)
                {
                    var recentMessage = new RecentContactMessage
                    {
                        UserId = chatItem.IsGroup ? 0 : chatItem.ChatId,
                        GroupId = chatItem.IsGroup ? chatItem.ChatId : 0,
                        ChatType = chatItem.IsGroup ? 2 : 1,
                        Message = chatItem.LastMessage,
                        ParsedMessage = chatItem.LastMessage,
                        Time = DateTimeOffset.Now.ToUnixTimeSeconds(), // 使用當前時間作為佔位符
                        PeerName = chatItem.Name,
                        Remark = chatItem.Name
                    };

                    recentMessages.Add(recentMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"轉換聊天項目為最近聯繫人時發生錯誤: {ex.Message}");
            }

            return recentMessages;
        }

        /// <summary>
        ///     載入當前用戶頭像
        /// </summary>
        private async Task LoadCurrentUserAvatarAsync()
        {
            try
            {
                if (_currentUserId == 0) return;

                // 載入當前用戶頭像 - 修復：添加缺失的第四個參數
                var userAvatar = await AvatarManager.GetAvatarAsync("current", _currentUserId, 0);

                if (userAvatar != null)
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.High, () =>
                        {
                            try
                            {
                                // 更新側邊欄用戶頭像
                                _mainView?.UpdateUserAvatar(userAvatar);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"更新用戶頭像UI錯誤: {ex.Message}");
                            }
                        });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入當前用戶頭像錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     階段3：載入聯絡人和群組（低優先級）
        /// </summary>
        private void LoadContactsAndGroups()
        {
            try
            {
                if (_hasLoadedContacts) return;

                _hasLoadedContacts = true;

                // 請求群組列表
                var groupListAction = JSONTools.ActionToJSON("get_group_list", new JsonObject(), "get_group_list");
                MainPage.SocketClientStarter._socket.Send(groupListAction);

                // 延遲請求好友列表，避免同時發送太多請求
                Task.Delay(200).ContinueWith(_ =>
                {
                    try
                    {
                        var friendListAction = JSONTools.ActionToJSON("get_friends_with_category", new JsonObject(),
                            "get_friends_with_category");
                        MainPage.SocketClientStarter._socket.Send(friendListAction);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"請求好友列表錯誤: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聯絡人和群組錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     請求最近聯繫人消息 - 減少日誌輸出
        /// </summary>
        private async void RequestRecentContacts()
        {
            try
            {
                var requestData = new
                {
                    action = "get_recent_contact",
                    @params = new
                    {
                        count = 50
                    },
                    echo = "get_recent_contact"
                };

                var jsonString = JsonConvert.SerializeObject(requestData);
                await MainPage.SocketClientStarter._socket.Send(jsonString);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求最近聯繫人錯誤: {ex.Message}");
            }
        }

        public void IncomingTask(string messages)
        {
            var json = JsonObject.Parse(messages);
            var echo = json.GetNamedString("echo", "null");
            if ("null" != echo)
                ActionResponseHandler(messages, echo);
            else
                // 處理接收到的消息
                HandleIncomingMessage(json);
        }

        private void ActionResponseHandler(string json, string echo)
        {
            var response = JsonConvert.DeserializeObject<ResponseEntity>(json);
            switch (echo)
            {
                case "login_info":
                    LoginInfoHandler(response);
                    break;
                case "get_group_list":
                    GroupListHandler(response);
                    break;
                case "get_friends_with_category":
                    FriendsWithCategoryHandler(response);
                    break;
                case "get_recent_contact":
                    RecentContactHandler(response);
                    break;
                default:
                    // 處理聊天歷史消息響應
                    if (echo.StartsWith("get_group_msg_history_"))
                    {
                        var groupId = long.Parse(echo.Substring("get_group_msg_history_".Length));
                        HandleChatHistoryResponse(response, groupId, true);
                    }
                    else if (echo.StartsWith("get_friend_msg_history_"))
                    {
                        var userId = long.Parse(echo.Substring("get_friend_msg_history_".Length));
                        HandleChatHistoryResponse(response, userId, false);
                    }
                    else if (echo.StartsWith("get_group_member_info_"))
                    {
                        HandleGroupMemberInfoResponse(response, echo);
                    }
                    else if (echo.StartsWith("get_msg_"))
                    {
                        HandleGetMessageResponse(response, echo);
                    }
                    // 處理 get_image API 響應
                    else if (echo.StartsWith("get_image_"))
                    {
                        HandleGetImageResponse(response, echo);
                    }
                    // 處理來自 ChatCreationHelper 的歷史消息請求
                    else if (echo.StartsWith("friend_history_"))
                    {
                        var parts = echo.Split('_');
                        if (parts.Length >= 3 && long.TryParse(parts[2], out var friendUserId))
                            HandleChatCreationHistoryResponse(response, friendUserId, false, echo);
                    }
                    else if (echo.StartsWith("group_history_"))
                    {
                        var parts = echo.Split('_');
                        if (parts.Length >= 3 && long.TryParse(parts[2], out var groupId))
                            HandleChatCreationHistoryResponse(response, groupId, true, echo);
                    }

                    break;
            }
        }

        /// <summary>
        ///     處理 get_image API 響應
        /// </summary>
        private void HandleGetImageResponse(ResponseEntity response, string echo)
        {
            try
            {
                if (response.Status == "ok" && response.Data != null)
                {
                    var imageUrl = response.Data.Value<string>("url");

                    if (!string.IsNullOrEmpty(imageUrl))
                        Task.Run(async () =>
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                CoreDispatcherPriority.Normal, () =>
                                {
                                    try
                                    {
                                        _mainView?.HandleGetImageResponse(imageUrl, echo);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"通知 ImageViewer 處理響應錯誤: {ex.Message}");
                                    }
                                });
                        });
                }
                else
                {
                    Task.Run(async () =>
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                try
                                {
                                    _mainView?.HandleGetImageError(echo, $"API 請求失敗: {response.Status}");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"通知 ImageViewer 錯誤時異常: {ex.Message}");
                                }
                            });
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理 get_image API 響應錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理聊天創建時的歷史消息響應
        /// </summary>
        private void HandleChatCreationHistoryResponse(ResponseEntity response, long chatId, bool isGroup, string echo)
        {
            try
            {
                if (response.Status == "ok" && response.Data != null)
                {
                    var historyMessages = new List<ChatMessage>();

                    try
                    {
                        JToken messagesArray = null;

                        if (response.Data.Type == JTokenType.Object && response.Data["messages"] != null)
                            messagesArray = response.Data["messages"];
                        else if (response.Data.Type == JTokenType.Array)
                            messagesArray = response.Data;

                        if (messagesArray != null && messagesArray.Type == JTokenType.Array)
                            foreach (var messageToken in messagesArray)
                                try
                                {
                                    var chatMessage = ParseHistoryMessage(messageToken, isGroup, chatId);
                                    if (chatMessage != null)
                                    {
                                        historyMessages.Add(chatMessage);

                                        var messageId = messageToken.Value<long>("message_id");
                                        DataAccess.SaveMessage(messageId, chatId, isGroup, chatMessage.Content,
                                            chatMessage.MessageType, chatMessage.SenderId, chatMessage.SenderName,
                                            chatMessage.IsFromMe, chatMessage.Timestamp, chatMessage.Segments);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"解析單條創建歷史消息錯誤: {ex.Message}");
                                }

                        ChatCreationHelper.ProcessHistoryMessageResponse(response.ToString(), echo);

                        Task.Run(async () =>
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                CoreDispatcherPriority.Normal, () => { _mainView?.RefreshChatList(); });
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"處理聊天創建歷史消息數據錯誤: {ex.Message}");
                        NotifyHistoryLoadingError(chatId, isGroup);
                    }
                }
                else
                {
                    NotifyHistoryLoadingError(chatId, isGroup);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理聊天創建歷史消息響應錯誤: {ex.Message}");
                NotifyHistoryLoadingError(chatId, isGroup);
            }
        }

        /// <summary>
        ///     通知MainView歷史消息加載錯誤
        /// </summary>
        private void NotifyHistoryLoadingError(long chatId, bool isGroup)
        {
            try
            {
                Task.Run(async () =>
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        () => { _mainView?.HandleHistoryLoadingError(chatId, isGroup); });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"通知歷史消息加載錯誤異常: {ex.Message}");
            }
        }

        /// <summary>
        ///     請求群組成員信息
        /// </summary>
        public async void RequestGroupMemberInfo(long groupId, long userId)
        {
            try
            {
                var requestData = new
                {
                    action = "get_group_member_info",
                    @params = new
                    {
                        group_id = groupId,
                        user_id = userId,
                        no_cache = false
                    },
                    echo = $"get_group_member_info_{groupId}_{userId}"
                };

                var jsonString = JsonConvert.SerializeObject(requestData);
                await MainPage.SocketClientStarter._socket.Send(jsonString);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求群組成員信息失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     修復版處理群組成員信息響應 - 避免觸發不必要的頭像請求
        /// </summary>
        private void HandleGroupMemberInfoResponse(ResponseEntity response, string echo)
        {
            try
            {
                if (response.Status == "ok" && response.Data != null)
                {
                    var parts = echo.Substring("get_group_member_info_".Length).Split('_');
                    if (parts.Length >= 2 && long.TryParse(parts[0], out var groupId) &&
                        long.TryParse(parts[1], out var userId))
                    {
                        var memberInfo = new GroupMemberInfo
                        {
                            GroupId = groupId,
                            UserId = userId,
                            Nickname = response.Data.Value<string>("nickname") ?? "",
                            Card = response.Data.Value<string>("card") ?? "",
                            Sex = response.Data.Value<string>("sex") ?? "",
                            Age = response.Data.Value<int>("age"),
                            Area = response.Data.Value<string>("area") ?? "",
                            JoinTime = response.Data.Value<long>("join_time"),
                            LastSentTime = response.Data.Value<long>("last_sent_time"),
                            Level = response.Data.Value<string>("level") ?? "",
                            Role = response.Data.Value<string>("role") ?? "",
                            Unfriendly = response.Data.Value<bool>("unfriendly"),
                            Title = response.Data.Value<string>("title") ?? "",
                            TitleExpireTime = response.Data.Value<long>("title_expire_time"),
                            CardChangeable = response.Data.Value<bool>("card_changeable"),
                            ShutUpTimestamp = response.Data.Value<long>("shut_up_timestamp")
                        };

                        Task.Run(() =>
                        {
                            // 重要修復：只保存群組成員信息，不觸發頭像載入或UI更新
                            DataAccess.SaveGroupMember(memberInfo);
                            Debug.WriteLine(
                                $"已保存群組成員信息: GroupId={groupId}, UserId={userId}, DisplayName={memberInfo.GetDisplayName()}");

                            // 只更新用戶信息，不刷新聊天消息避免觸發頭像載入
                            // DataAccess.UpdateUserInfoInMessages(); // 暫時移除或限制執行

                            // 只有在當前聊天是該群組時才刷新消息
                            if (_mainView?.IsCurrentChat(groupId, true) == true)
                                Task.Run(async () =>
                                {
                                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                        CoreDispatcherPriority.Normal,
                                        () => { _mainView?.RefreshCurrentChatMessages(); });
                                });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理群組成員信息響應錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理群組列表響應 - 優化版本，減少日誌輸出
        /// </summary>
        private void GroupListHandler(ResponseEntity response)
        {
            try
            {
                if (response.Status == "ok" && response.Data != null)
                {
                    var groups = new List<GroupInfo>();

                    try
                    {
                        if (response.Data.Type == JTokenType.Array)
                            foreach (var groupToken in response.Data)
                                try
                                {
                                    var group = new GroupInfo
                                    {
                                        GroupId = groupToken.Value<long>("group_id"),
                                        GroupName = groupToken.Value<string>("group_name") ?? "",
                                        GroupRemark = groupToken.Value<string>("group_remark") ?? "",
                                        MemberCount = groupToken.Value<int>("member_count"),
                                        MaxMemberCount = groupToken.Value<int>("max_member_count"),
                                        GroupAllShut = groupToken.Value<bool>("group_all_shut")
                                    };
                                    groups.Add(group);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"解析單個群組錯誤: {ex.Message}");
                                }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"解析群組數據錯誤: {ex.Message}");
                    }

                    // 使用安全的異步方法保存到數據庫並異步載入頭像
                    Task.Run(async () =>
                    {
                        // 修復：使用正確的方法名稱
                        DataAccess.SaveGroups(groups);

                        // 更新UI
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () => { _mainView?.UpdateGroupInfoInChatList(); });

                        // 後台異步載入群組頭像（低優先級）
                        _ = LoadGroupAvatarsInBackground(groups);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理群組列表響應錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     後台載入群組頭像
        /// </summary>
        private async Task LoadGroupAvatarsInBackground(List<GroupInfo> groups)
        {
            try
            {
                await Task.Delay(1000); // 延遲1秒避免與聊天列表頭像載入衝突

                const int batchSize = 5;
                for (var i = 0; i < groups.Count; i += batchSize)
                {
                    var batch = groups.Skip(i).Take(batchSize);
                    var tasks = batch.Select(async group =>
                    {
                        try
                        {
                            var avatar = await AvatarManager.GetAvatarAsync("group", group.GroupId, 3, true);
                            if (avatar != null)
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Low, () =>
                                    {
                                        // 更新群組列表頭像
                                        group.AvatarImage = avatar;

                                        // 同時更新聊天列表中對應項目的頭像
                                        _mainView?.UpdateChatItemAvatar(group.GroupId, true, avatar);
                                    });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"載入群組頭像錯誤: {group.GroupId}, {ex.Message}");
                        }
                    });

                    await Task.WhenAll(tasks);
                    await Task.Delay(200); // 批次間延遲
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"後台載入群組頭像錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     定期重新整理群組和好友列表
        /// </summary>
        public async void RefreshGroupAndFriendListPeriodically()
        {
            try
            {
                LoadContactsAndGroups();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"定期重新整理錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理聊天歷史消息響應
        /// </summary>
        private void HandleChatHistoryResponse(ResponseEntity response, long chatId, bool isGroup)
        {
            try
            {
                if (response.Status == "ok" && response.Data != null)
                {
                    var historyMessages = new List<ChatMessage>();

                    try
                    {
                        JToken messagesArray = null;

                        if (response.Data.Type == JTokenType.Object && response.Data["messages"] != null)
                            messagesArray = response.Data["messages"];
                        else if (response.Data.Type == JTokenType.Array)
                            messagesArray = response.Data;

                        if (messagesArray != null && messagesArray.Type == JTokenType.Array)
                            foreach (var messageToken in messagesArray)
                                try
                                {
                                    var chatMessage = ParseHistoryMessage(messageToken, isGroup, chatId);
                                    if (chatMessage != null)
                                    {
                                        historyMessages.Add(chatMessage);

                                        var messageId = messageToken.Value<long>("message_id");
                                        DataAccess.SaveMessage(messageId, chatId, isGroup, chatMessage.Content,
                                            chatMessage.MessageType, chatMessage.SenderId, chatMessage.SenderName,
                                            chatMessage.IsFromMe, chatMessage.Timestamp, chatMessage.Segments);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"解析單條歷史消息錯誤: {ex.Message}");
                                }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"解析歷史消息數據錯誤: {ex.Message}");
                        NotifyHistoryLoadingError(chatId, isGroup);
                        return;
                    }

                    Task.Run(async () =>
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal,
                            () => { _mainView?.HandleHistoryMessages(historyMessages, chatId, isGroup); });
                    });
                }
                else
                {
                    NotifyHistoryLoadingError(chatId, isGroup);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理聊天歷史消息響應錯誤: {ex.Message}");
                NotifyHistoryLoadingError(chatId, isGroup);
            }
        }

        /// <summary>
        ///     解析歷史消息 - 減少日誌輸出
        /// </summary>
        private ChatMessage ParseHistoryMessage(JToken messageToken, bool isGroup, long chatId = 0)
        {
            try
            {
                var messageId = messageToken.Value<long>("message_id");
                var unixTimestamp = messageToken.Value<long>("time");

                DateTime timestamp;
                try
                {
                    var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                    timestamp = utcTime.ToLocalTime();
                }
                catch (Exception)
                {
                    timestamp = DateTime.Now;
                }

                var senderId = messageToken.Value<long>("user_id");
                var messageType = messageToken.Value<string>("message_type") ?? (isGroup ? "group" : "private");

                var segments = new List<MessageSegment>();
                var plainText = "";

                if (messageToken["raw_message"] != null) plainText = messageToken.Value<string>("raw_message") ?? "";

                var messageArray = messageToken["message"];
                if (messageArray != null && messageArray.Type == JTokenType.Array)
                {
                    segments = MessageSegmentParser.ParseMessageArray(messageArray, isGroup ? chatId : 0);
                    if (string.IsNullOrEmpty(plainText))
                        plainText = MessageSegmentParser.GenerateTextFromSegments(segments);
                }
                else if (!string.IsNullOrEmpty(plainText))
                {
                    segments = new List<MessageSegment> { new TextSegment(plainText) };
                }

                var senderName = "";
                var isFromMe = false;
                var currentUserId = GetCurrentUserId();

                if (isGroup)
                {
                    isFromMe = senderId == currentUserId;
                    if (isFromMe)
                    {
                        senderName = "我";
                    }
                    else
                    {
                        var senderToken = messageToken["sender"];
                        if (senderToken != null)
                        {
                            var card = senderToken.Value<string>("card") ?? "";
                            var nickname = senderToken.Value<string>("nickname") ?? "";
                            senderName = !string.IsNullOrEmpty(card) ? card : nickname;
                        }

                        if (string.IsNullOrEmpty(senderName)) senderName = DataAccess.GetFriendNameById(senderId);
                    }
                }
                else
                {
                    isFromMe = senderId == currentUserId;
                    if (isFromMe)
                        senderName = "我";
                    else
                        senderName = DataAccess.GetFriendNameById(senderId);
                }

                if (string.IsNullOrEmpty(senderName)) senderName = isFromMe ? "我" : $"用戶 {senderId}";

                return new ChatMessage
                {
                    Content = plainText,
                    Timestamp = timestamp,
                    IsFromMe = isFromMe,
                    SenderName = senderName,
                    SenderId = senderId,
                    MessageType = messageType,
                    Segments = segments
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析歷史消息錯誤: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     獲取當前用戶ID
        /// </summary>
        private long GetCurrentUserId()
        {
            try
            {
                if (_currentUserId != 0) return _currentUserId;

                var settings = DataAccess.GetAllDatas();
                var account = settings.Get("Account") ?? "";
                if (long.TryParse(account, out var userId))
                {
                    _currentUserId = userId;
                    return userId;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取當前用戶ID錯誤: {ex.Message}");
            }

            return 0;
        }

        private async void HandleIncomingMessage(JsonObject json)
        {
            try
            {
                var postType = json.GetNamedString("post_type", "");
                var messageType = json.GetNamedString("message_type", "");

                if (postType == "message" && (messageType == "group" || messageType == "private"))
                    await HandleReceivedMessage(json, messageType);
                else if (postType == "message_sent" && (messageType == "group" || messageType == "private"))
                    await HandleSentMessage(json, messageType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理消息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     請求獲取指定消息的內容
        /// </summary>
        public async void RequestMessageContent(long messageId, string echo = null)
        {
            try
            {
                if (_requestingMessages.Contains(messageId))
                    return;

                _requestingMessages.Add(messageId);

                var requestData = new
                {
                    action = "get_msg",
                    @params = new
                    {
                        message_id = messageId
                    },
                    echo = echo ?? $"get_msg_{messageId}"
                };

                var jsonString = JsonConvert.SerializeObject(requestData);
                await MainPage.SocketClientStarter._socket.Send(jsonString);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求獲取消息錯誤: {ex.Message}");
                _requestingMessages.Remove(messageId);
            }
        }

        /// <summary>
        ///     處理 get_msg 響應
        /// </summary>
        private void HandleGetMessageResponse(ResponseEntity response, string echo)
        {
            try
            {
                if (response.Status == "ok" && response.Data != null)
                {
                    var messageIdStr = "";
                    if (echo.StartsWith("get_msg_reply_"))
                        messageIdStr = echo.Substring("get_msg_reply_".Length);
                    else if (echo.StartsWith("get_msg_"))
                        messageIdStr = echo.Substring("get_msg_".Length);

                    if (long.TryParse(messageIdStr, out var messageId))
                        try
                        {
                            var unixTimestamp = response.Data.Value<long>("time");
                            DateTime timestamp;
                            try
                            {
                                var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                                timestamp = utcTime.ToLocalTime();
                            }
                            catch
                            {
                                timestamp = DateTime.Now;
                            }

                            var senderId = response.Data.Value<long>("user_id");
                            var messageType = response.Data.Value<string>("message_type") ?? "text";
                            var isGroup = messageType == "group";
                            var chatId = isGroup ? response.Data.Value<long>("group_id") : senderId;

                            var segments = new List<MessageSegment>();
                            var plainText = "";

                            if (response.Data["raw_message"] != null)
                                plainText = response.Data.Value<string>("raw_message") ?? "";

                            var messageArray = response.Data["message"];
                            if (messageArray != null && messageArray.Type == JTokenType.Array)
                            {
                                segments = MessageSegmentParser.ParseMessageArray(messageArray, isGroup ? chatId : 0);
                                if (string.IsNullOrEmpty(plainText))
                                    plainText = MessageSegmentParser.GenerateTextFromSegments(segments);
                            }
                            else if (!string.IsNullOrEmpty(plainText))
                            {
                                segments = new List<MessageSegment> { new TextSegment(plainText) };
                            }

                            var senderName = "";
                            var currentUserId = GetCurrentUserId();
                            var isFromMe = senderId == currentUserId;

                            if (isFromMe)
                            {
                                senderName = "我";
                            }
                            else if (isGroup)
                            {
                                var senderToken = response.Data["sender"];
                                if (senderToken != null)
                                {
                                    var card = senderToken.Value<string>("card") ?? "";
                                    var nickname = senderToken.Value<string>("nickname") ?? "";
                                    senderName = !string.IsNullOrEmpty(card) ? card : nickname;
                                }

                                if (string.IsNullOrEmpty(senderName))
                                    senderName = DataAccess.GetFriendNameById(senderId);
                            }
                            else
                            {
                                senderName = DataAccess.GetFriendNameById(senderId);
                            }

                            if (string.IsNullOrEmpty(senderName) || senderName.StartsWith("用戶 "))
                                senderName = isFromMe ? "我" : $"用戶 {senderId}";

                            DataAccess.SaveMessage(messageId, chatId, isGroup, plainText, messageType, senderId,
                                senderName, isFromMe, timestamp, segments);

                            _requestingMessages.Remove(messageId);

                            Task.Run(async () =>
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Normal, () => { _mainView?.RefreshCurrentChatMessages(); });
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"解析獲取的消息錯誤: {ex.Message}");
                        }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理獲取消息響應錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取消息內容 - 減少日誌輸出
        /// </summary>
        private MessageContentResult GetMessageContent(JsonObject json, long groupId = 0)
        {
            var plainText = "";
            var segments = new List<MessageSegment>();

            try
            {
                if (json.ContainsKey("raw_message")) plainText = json.GetNamedString("raw_message", "");

                if (json.ContainsKey("message"))
                {
                    var messageValue = json.GetNamedValue("message");
                    if (messageValue.ValueType == JsonValueType.Array)
                    {
                        var messageJson = messageValue.ToString();
                        var messageArray = JToken.Parse(messageJson);

                        segments = MessageSegmentParser.ParseMessageArray(messageArray, groupId);
                        if (string.IsNullOrEmpty(plainText))
                            plainText = MessageSegmentParser.GenerateTextFromSegments(segments);
                    }
                    else if (messageValue.ValueType == JsonValueType.String)
                    {
                        plainText = messageValue.GetString();
                        segments.Add(new TextSegment(plainText));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析消息內容錯誤: {ex.Message}");
                plainText = "[無法解析的消息]";
                segments = new List<MessageSegment> { new TextSegment(plainText) };
            }

            if (string.IsNullOrEmpty(plainText))
            {
                plainText = "[空消息]";
                if (segments.Count == 0) segments.Add(new TextSegment(plainText));
            }

            return new MessageContentResult(plainText, segments);
        }

        /// <summary>
        ///     處理接收到的消息 - 減少日誌輸出
        /// </summary>
        private async Task HandleReceivedMessage(JsonObject json, string messageType)
        {
            var senderName = "";
            var actualSenderId = 0L;
            var chatId = 0L;
            var isGroup = messageType == "group";

            var messageId = (long)json.GetNamedNumber("message_id", 0);
            var currentTime = DateTime.Now;

            try
            {
                if (messageType == "group")
                {
                    var groupId = json.GetNamedNumber("group_id", 0);
                    chatId = (long)groupId;

                    var messageContent = GetMessageContent(json, chatId);
                    var messageText = messageContent.PlainText;
                    var messageSegments = messageContent.Segments;

                    actualSenderId = (long)json.GetNamedNumber("user_id", 0);
                    var groupName = DataAccess.GetGroupNameById(chatId);

                    var actualSenderName = "";
                    try
                    {
                        if (json.ContainsKey("sender"))
                        {
                            var sender = json.GetNamedObject("sender");
                            var nickname = sender.GetNamedString("nickname", "");
                            var card = sender.GetNamedString("card", "");
                            actualSenderName = !string.IsNullOrEmpty(card) ? card : nickname;
                        }
                    }
                    catch
                    {
                    }

                    if (string.IsNullOrEmpty(actualSenderName)) actualSenderName = "群成員";
                    senderName = actualSenderName;

                    // 修復：使用正確的方法名稱
                    DataAccess.SaveMessage(messageId, chatId, isGroup, messageText,
                        "group", actualSenderId, senderName, false, currentTime, messageSegments);

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            _mainView?.AddIncomingMessage(actualSenderName, chatId, actualSenderId, messageText, true,
                                messageSegments);

                            var displayMessage = $"{actualSenderName}: {messageText}";
                            _mainView?.UpdateChatItem(actualSenderName, chatId, isGroup, displayMessage);
                        });
                }
                else
                {
                    var messageContent = GetMessageContent(json);
                    var messageText = messageContent.PlainText;
                    var messageSegments = messageContent.Segments;
                    var userId = json.GetNamedNumber("user_id", 0);
                    chatId = (long)userId;
                    actualSenderId = (long)userId;

                    senderName = DataAccess.GetFriendNameById(chatId);

                    // 修復：使用正確的方法名稱
                    DataAccess.SaveMessage(messageId, chatId, isGroup, messageText,
                        "private", actualSenderId, senderName, false, currentTime, messageSegments);

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            _mainView?.AddIncomingMessage(senderName, chatId, actualSenderId, messageText, false,
                                messageSegments);

                            _mainView?.UpdateChatItem(senderName, chatId, isGroup, messageText);
                        });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理接收消息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理發送的消息 - 減少日誌輸出
        /// </summary>
        private async Task HandleSentMessage(JsonObject json, string messageType)
        {
            try
            {
                var targetName = "";
                var targetId = 0L;
                var isGroup = messageType == "group";

                MessageContentResult messageContent;
                if (isGroup)
                {
                    var groupId = json.GetNamedNumber("group_id", 0);
                    messageContent = GetMessageContent(json, (long)groupId);
                    targetId = (long)groupId;
                }
                else
                {
                    messageContent = GetMessageContent(json);
                }

                var messageText = messageContent.PlainText;
                var messageSegments = messageContent.Segments;

                var messageId = (long)json.GetNamedNumber("message_id", 0);
                var currentUserId = (long)json.GetNamedNumber("self_id", 0);

                if (messageType == "group")
                {
                    targetName = DataAccess.GetGroupNameById(targetId);
                }
                else
                {
                    var targetUserId = json.GetNamedNumber("target_id", 0);

                    if (targetUserId == 0)
                    {
                        targetUserId = json.GetNamedNumber("user_id", 0);

                        var messageSentType = json.GetNamedString("message_sent_type", "");
                        var selfId = json.GetNamedNumber("self_id", 0);

                        if (messageSentType == "self" && targetUserId == selfId)
                            if (json.ContainsKey("raw"))
                                try
                                {
                                    var rawObject = json.GetNamedObject("raw");
                                    if (rawObject.ContainsKey("peerUin"))
                                    {
                                        var peerUin = rawObject.GetNamedString("peerUin", "");
                                        if (long.TryParse(peerUin, out var peerUserId)) targetUserId = peerUserId;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"解析 raw 字段錯誤: {ex.Message}");
                                }
                    }

                    targetId = (long)targetUserId;
                    targetName = DataAccess.GetFriendNameById(targetId);

                    if (targetName.StartsWith("用戶 ")) targetName = $"好友 {targetId}";
                }

                // 修復：使用正確的方法名稱
                DataAccess.SaveMessage(messageId, targetId, isGroup, messageText,
                    isGroup ? "group" : "private", currentUserId, "我", true, DateTime.Now, messageSegments);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        if (_mainView?.IsCurrentChat(targetId, isGroup) == true)
                            _mainView?.AddOutgoingMessage("我", targetId, currentUserId, messageText, isGroup,
                                messageSegments);

                        _mainView?.UpdateChatItem("我", targetId, isGroup, $"我: {messageText}", false);
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理發送消息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理最近聯繫人響應 - 使用新的緩存機制
        /// </summary>
        private void RecentContactHandler(ResponseEntity response)
        {
            try
            {
                if (response.Status == "ok" && response.Data != null)
                {
                    var recentMessages = new List<RecentContactMessage>();

                    try
                    {
                        if (response.Data.Type == JTokenType.Array)
                            foreach (var contactToken in response.Data)
                                try
                                {
                                    var recentMessage = ParseRecentContactMessage(contactToken);
                                    if (recentMessage != null) recentMessages.Add(recentMessage);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"解析單個最近聯繫人錯誤: {ex.Message}");
                                }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"解析最近聯繫人數據錯誤: {ex.Message}");
                    }

                    Task.Run(async () =>
                    {
                        // 使用新的帶頭像緩存的方法保存數據
                        var currentAccount = GetCurrentAccount();
                        if (!string.IsNullOrEmpty(currentAccount))
                        {
                            var chatItems = DataAccess.CreateChatItemsFromRecentMessages(recentMessages);
                            DataAccess.SaveChatListCacheWithAvatars(currentAccount, chatItems);
                            Debug.WriteLine($"已保存聊天列表緩存（包含頭像關聯）: {chatItems.Count} 個項目");
                        }

                        // 在 UI 線程中刷新聊天列表
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal,
                            () => { _mainView?.LoadRecentContactsToUI(recentMessages); });

                        // 延遲載入聊天列表頭像（避免阻塞UI）
                        await Task.Delay(500);
                        await LoadChatListAvatarsInBackground(recentMessages);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理最近聯繫人響應錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     修復版後台載入聊天列表頭像 - 確保類型判斷正確
        /// </summary>
        private async Task LoadChatListAvatarsInBackground(List<RecentContactMessage> recentMessages)
        {
            try
            {
                const int batchSize = 3;
                var processedChats = new HashSet<string>();

                for (var i = 0; i < recentMessages.Count; i += batchSize)
                {
                    var batch = recentMessages.Skip(i).Take(batchSize);
                    var tasks = batch.Select(async message =>
                    {
                        try
                        {
                            var isGroup = message.ChatType == 2;
                            var chatId = isGroup ? message.GroupId : message.UserId;
                            var chatKey = $"{chatId}_{isGroup}";

                            if (processedChats.Contains(chatKey)) return;
                            processedChats.Add(chatKey);

                            // 重要修復：使用正確的頭像類型
                            var avatarType = isGroup ? "group" : "friend";
                            var expectedCacheKey = $"{avatarType}_{chatId}";

                            Debug.WriteLine($"後台載入頭像: ChatId={chatId}, IsGroup={isGroup}, CacheKey={expectedCacheKey}");

                            var avatar = await AvatarManager.GetAvatarAsync(avatarType, chatId, 2, true);

                            if (avatar != null)
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Low,
                                    () => { _mainView?.UpdateChatItemAvatar(chatId, isGroup, avatar); });

                                Debug.WriteLine($"✓ 後台載入頭像成功: {expectedCacheKey}");
                            }
                            else
                            {
                                Debug.WriteLine($"✗ 後台載入頭像失敗: {expectedCacheKey}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"載入聊天頭像錯誤: {ex.Message}");
                        }
                    });

                    await Task.WhenAll(tasks);
                    await Task.Delay(100); // 批次間延遲
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"後台載入聊天列表頭像錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     解析最近聯繫人消息 - 減少日誌輸出
        /// </summary>
        private RecentContactMessage ParseRecentContactMessage(JToken contactToken)
        {
            try
            {
                var latestMsgToken = contactToken["lastestMsg"];
                if (latestMsgToken == null) return null;

                var unixTimestamp = latestMsgToken.Value<long>("time");
                DateTime processedTimestamp;

                try
                {
                    var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                    var localTime = utcTime.ToLocalTime();
                    processedTimestamp = DataAccess.ProcessTimestamp(localTime);
                }
                catch (Exception)
                {
                    processedTimestamp = DateTime.Now;
                }

                var message = new RecentContactMessage
                {
                    SelfId = latestMsgToken.Value<long>("self_id"),
                    UserId = latestMsgToken.Value<long>("user_id"),
                    Time = unixTimestamp,
                    MessageId = latestMsgToken.Value<long>("message_id"),
                    MessageSeq = latestMsgToken.Value<long>("message_seq"),
                    RealId = latestMsgToken.Value<long>("real_id"),
                    MessageType = latestMsgToken.Value<string>("message_type") ?? "",
                    RawMessage = latestMsgToken.Value<string>("raw_message") ?? "",
                    Font = latestMsgToken.Value<long>("font"),
                    SubType = latestMsgToken.Value<string>("sub_type") ?? "",
                    PostType = latestMsgToken.Value<string>("post_type") ?? "",
                    MessageSentType = latestMsgToken.Value<string>("message_sent_type") ?? "",
                    GroupId = latestMsgToken.Value<long>("group_id"),
                    PeerUin = contactToken.Value<string>("peerUin") ?? "",
                    Remark = contactToken.Value<string>("remark") ?? "",
                    MsgTime = contactToken.Value<string>("msgTime") ?? "",
                    ChatType = contactToken.Value<long>("chatType"),
                    MsgId = contactToken.Value<string>("msgId") ?? "",
                    SendNickName = contactToken.Value<string>("sendNickName") ?? "",
                    SendMemberName = contactToken.Value<string>("sendMemberName") ?? "",
                    PeerName = contactToken.Value<string>("peerName") ?? "",
                    Message = contactToken.Value<string>("message") ?? "",
                    Wording = contactToken.Value<string>("wording") ?? "",
                    ProcessedTimestamp = processedTimestamp
                };

                var senderToken = latestMsgToken["sender"];
                if (senderToken != null)
                    message.Sender = new MessageSender
                    {
                        UserId = senderToken.Value<long>("user_id"),
                        Nickname = senderToken.Value<string>("nickname") ?? "",
                        Card = senderToken.Value<string>("card") ?? "",
                        Sex = senderToken.Value<string>("sex") ?? "",
                        Age = senderToken.Value<int>("age"),
                        Area = senderToken.Value<string>("area") ?? "",
                        Level = senderToken.Value<string>("level") ?? "",
                        Role = senderToken.Value<string>("role") ?? "",
                        Title = senderToken.Value<string>("title") ?? ""
                    };

                var messageArrayToken = latestMsgToken["message"];
                if (messageArrayToken != null && messageArrayToken.Type == JTokenType.Array)
                {
                    var segments = MessageSegmentParser.ParseMessageArray(messageArrayToken);
                    message.MessageSegments = segments;
                    message.ParsedMessage = MessageSegmentParser.GenerateTextFromSegments(segments);
                }
                else if (!string.IsNullOrEmpty(message.RawMessage))
                {
                    message.ParsedMessage = message.RawMessage;
                    message.MessageSegments = new List<MessageSegment> { new TextSegment(message.RawMessage) };
                }
                else
                {
                    message.MessageSegments = new List<MessageSegment>();
                }

                return message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析最近聯繫人消息錯誤: {ex.Message}");
                return null;
            }
        }

        private string GetMessageText(JsonObject json)
        {
            var content = GetMessageContent(json);
            return content.PlainText;
        }

        /// <summary>
        ///     處理好友列表響應 - 減少日誌輸出，異步載入頭像
        /// </summary>
        private void FriendsWithCategoryHandler(ResponseEntity response)
        {
            try
            {
                if (response.Status == "ok" && response.Data != null)
                {
                    var categories = new List<FriendCategory>();

                    try
                    {
                        if (response.Data.Type == JTokenType.Array)
                            foreach (var categoryToken in response.Data)
                                try
                                {
                                    var category = new FriendCategory
                                    {
                                        CategoryId = categoryToken.Value<long>("categoryId"),
                                        CategorySortId = categoryToken.Value<long>("categorySortId"),
                                        CategoryName = categoryToken.Value<string>("categoryName") ?? "",
                                        CategoryMbCount = categoryToken.Value<int>("categoryMbCount"),
                                        OnlineCount = categoryToken.Value<int>("onlineCount"),
                                        BuddyList = new List<FriendInfo>()
                                    };

                                    var buddyListToken = categoryToken["buddyList"];
                                    if (buddyListToken != null && buddyListToken.Type == JTokenType.Array)
                                        foreach (var buddyToken in buddyListToken)
                                            try
                                            {
                                                var friend = new FriendInfo
                                                {
                                                    UserId = buddyToken.Value<long>("user_id"),
                                                    BirthdayYear = buddyToken.Value<int>("birthday_year"),
                                                    BirthdayMonth = buddyToken.Value<int>("birthday_month"),
                                                    BirthdayDay = buddyToken.Value<int>("birthday_day"),
                                                    Age = buddyToken.Value<int>("age"),
                                                    Sex = buddyToken.Value<string>("sex") ?? "",
                                                    Email = buddyToken.Value<string>("email") ?? "",
                                                    PhoneNum = buddyToken.Value<string>("phone_num") ?? "",
                                                    CategoryId = buddyToken.Value<long>("category_id"),
                                                    Nickname = buddyToken.Value<string>("nickname") ?? "",
                                                    Remark = buddyToken.Value<string>("remark") ?? "",
                                                    Level = buddyToken.Value<int>("level"),
                                                    Qid = "",
                                                    LongNick = "",
                                                    RichTime = 0,
                                                    Uid = "",
                                                    Uin = "",
                                                    Nick = buddyToken.Value<string>("nickname") ?? ""
                                                };

                                                category.BuddyList.Add(friend);
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.WriteLine($"解析單個好友錯誤: {ex.Message}");
                                            }

                                    categories.Add(category);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"解析單個分類錯誤: {ex.Message}");
                                }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"解析好友數據錯誤: {ex.Message}");
                    }

                    Task.Run(async () =>
                    {
                        DataAccess.SaveFriendsWithCategories(categories);
                        DataAccess.UpdateUserInfoInMessages();

                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                _mainView?.RefreshContactsAndGroups();
                                _mainView?.UpdateFriendInfoInChatList();
                            });

                        // 後台異步載入好友頭像（低優先級）
                        _ = LoadFriendAvatarsInBackground(categories);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理好友列表響應錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     後台載入好友頭像
        /// </summary>
        private async Task LoadFriendAvatarsInBackground(List<FriendCategory> categories)
        {
            try
            {
                await Task.Delay(2000); // 延遲2秒避免與其他頭像載入衝突

                const int batchSize = 3;
                var allFriends = new List<FriendInfo>();

                foreach (var category in categories)
                    if (category.BuddyList != null)
                        allFriends.AddRange(category.BuddyList);

                for (var i = 0; i < allFriends.Count; i += batchSize)
                {
                    var batch = allFriends.Skip(i).Take(batchSize);
                    var tasks = batch.Select(async friend =>
                    {
                        try
                        {
                            var avatar = await AvatarManager.GetAvatarAsync("friend", friend.UserId, 3, true);
                            if (avatar != null)
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Low, () =>
                                    {
                                        // 更新聊天列表中對應項目的頭像
                                        _mainView?.UpdateChatItemAvatar(friend.UserId, false, avatar);
                                    });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"載入好友頭像錯誤: {friend.UserId}, {ex.Message}");
                        }
                    });

                    await Task.WhenAll(tasks);
                    await Task.Delay(300); // 批次間延遲
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"後台載入好友頭像錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理登入信息響應 - 優化版本，包含用戶頭像載入
        /// </summary>
        private async void LoginInfoHandler(ResponseEntity response)
        {
            try
            {
                var user_id = response.Data.Value<double>("user_id");
                var nickName = response.Data.Value<string>("nickname");

                _currentUserId = (long)user_id;

                // 在 UI 線程中更新用戶信息
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.High, () => { _mainView?.UpdateInfo(user_id, nickName); });

                // 開始階段2：載入聊天列表和用戶頭像
                LoadChatListAndUserAvatar();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理登入信息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     消息內容結果類
        /// </summary>
        private class MessageContentResult
        {
            public MessageContentResult(string plainText, List<MessageSegment> segments)
            {
                PlainText = plainText;
                Segments = segments;
            }

            public string PlainText { get; }
            public List<MessageSegment> Segments { get; }
        }
    }
}