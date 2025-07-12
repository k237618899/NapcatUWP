using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private MainView _mainView;

        public void SetMainView(MainView mainView)
        {
            _mainView = mainView;
            Debug.WriteLine($"OneBotAPIHandler: MainView 設置成功，引用: {(_mainView != null ? "有效" : "無效")}");

            // 設置 ReplySegment 的靜態委託
            ReplySegment.RequestMessageContentDelegate = messageId =>
            {
                RequestMessageContent(messageId, $"get_msg_reply_{messageId}");
            };

            // 初始化時請求群組和好友列表
            RequestGroupAndFriendList();
        }

        /// <summary>
        ///     請求群組列表和好友列表，然後請求最近聯繫人消息
        /// </summary>
        private void RequestGroupAndFriendList()
        {
            try
            {
                // 請求群組列表
                var groupListAction = JSONTools.ActionToJSON("get_group_list", new JsonObject(), "get_group_list");
                MainPage.SocketClientStarter._socket.Send(groupListAction);
                Debug.WriteLine("OneBotAPIHandler: 已發送群組列表請求");

                // 請求好友列表
                var friendListAction = JSONTools.ActionToJSON("get_friends_with_category", new JsonObject(),
                    "get_friends_with_category");
                MainPage.SocketClientStarter._socket.Send(friendListAction);
                Debug.WriteLine("OneBotAPIHandler: 已發送好友列表請求");

                // 請求最近聯繫人消息
                RequestRecentContacts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 請求群組和好友列表時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     請求最近聯繫人消息 - 使用 Newtonsoft.Json，修改為100條
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
                        count = 50 // 修改為100條
                    },
                    echo = "get_recent_contact"
                };

                var jsonString = JsonConvert.SerializeObject(requestData);
                await MainPage.SocketClientStarter._socket.Send(jsonString);

                Debug.WriteLine("OneBotAPIHandler: 已發送最近聯繫人請求（100條）");
                Debug.WriteLine($"請求 JSON: {jsonString}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 請求最近聯繫人時發生錯誤: {ex.Message}");
            }
        }

        public void IncomingTask(string messages)
        {
            var json = JsonObject.Parse(messages);
            var echo = json.GetNamedString("echo", "null");
            if ("null" != echo)
            {
                ActionResponseHandler(messages, echo);
            }
            else
            {
                // 處理接收到的消息
                HandleIncomingMessage(json);
                Debug.WriteLine(json.ToString());
            }
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
                    else if (echo.StartsWith("get_msg_")) // 新增：處理 get_msg 響應
                    {
                        HandleGetMessageResponse(response, echo);
                    }

                    break;
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

                Debug.WriteLine($"已發送群組成員信息請求: GroupId={groupId}, UserId={userId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求群組成員信息失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理群組成員信息響應
        /// </summary>
        private void HandleGroupMemberInfoResponse(ResponseEntity response, string echo)
        {
            try
            {
                Debug.WriteLine("OneBotAPIHandler: 開始處理群組成員信息響應");

                if (response.Status == "ok" && response.Data != null)
                {
                    // 解析 echo 以獲取 groupId 和 userId
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

                        // 保存到數據庫
                        Task.Run(() =>
                        {
                            DataAccess.SaveGroupMember(memberInfo);
                            Debug.WriteLine($"OneBotAPIHandler: 成功保存群組成員信息 - {memberInfo.GetDisplayName()}");

                            // 新增：更新舊消息中的用戶信息
                            DataAccess.UpdateUserInfoInMessages();

                            // 通知UI更新（如果當前聊天是該群組）
                            Task.Run(async () =>
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Normal, () =>
                                    {
                                        if (_mainView?.IsCurrentChat(groupId, true) == true)
                                        {
                                            // 如果當前正在該群組聊天，觸發消息重新渲染
                                            Debug.WriteLine($"當前正在群組 {groupId} 聊天，成員信息已更新，觸發UI刷新");
                                            _mainView?.RefreshCurrentChatMessages();
                                        }
                                    });
                            });
                        });
                    }
                }
                else
                {
                    Debug.WriteLine($"OneBotAPIHandler: 群組成員信息請求失敗 - Status: {response.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 處理群組成員信息響應時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理群組列表響應 - 現在使用 JToken
        /// </summary>
        private void GroupListHandler(ResponseEntity response)
        {
            try
            {
                Debug.WriteLine("OneBotAPIHandler: 開始處理群組列表響應");

                if (response.Status == "ok" && response.Data != null)
                {
                    var groups = new List<GroupInfo>();

                    try
                    {
                        Debug.WriteLine($"OneBotAPIHandler: 群組數據類型: {response.Data.Type}");

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
                                    Debug.WriteLine($"OneBotAPIHandler: 解析單個群組時發生錯誤: {ex.Message}");
                                }
                        else
                            Debug.WriteLine($"OneBotAPIHandler: 群組數據不是陣列類型: {response.Data.Type}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OneBotAPIHandler: 解析群組數據時發生錯誤: {ex.Message}");
                    }

                    // 使用安全的異步方法保存到數據庫
                    Task.Run(async () =>
                    {
                        await SafeDataAccess.SaveGroupsAsync(groups);
                        Debug.WriteLine($"OneBotAPIHandler: 成功處理並保存 {groups.Count} 個群組");

                        // 更新UI
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                _mainView?.UpdateGroupInfoInChatList();
                                Debug.WriteLine("已觸發聊天列表群組信息更新");
                            });
                    });
                }
                else
                {
                    Debug.WriteLine($"OneBotAPIHandler: 群組列表請求失敗 - Status: {response.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 處理群組列表響應時發生錯誤: {ex.Message}");
            }
        }


        /// <summary>
        ///     定期重新整理群組和好友列表（可選）
        /// </summary>
        public async void RefreshGroupAndFriendListPeriodically()
        {
            try
            {
                Debug.WriteLine("OneBotAPIHandler: 定期重新整理群組和好友列表");
                RequestGroupAndFriendList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 定期重新整理時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理聊天歷史消息響應
        /// </summary>
        private void HandleChatHistoryResponse(ResponseEntity response, long chatId, bool isGroup)
        {
            try
            {
                Debug.WriteLine($"OneBotAPIHandler: 開始處理聊天歷史消息響應 - ChatId: {chatId}, IsGroup: {isGroup}");

                if (response.Status == "ok" && response.Data != null)
                {
                    var historyMessages = new List<ChatMessage>();

                    try
                    {
                        // 檢查數據結構
                        JToken messagesArray = null;

                        if (response.Data.Type == JTokenType.Object && response.Data["messages"] != null)
                            // 數據格式: { "messages": [...] }
                            messagesArray = response.Data["messages"];
                        else if (response.Data.Type == JTokenType.Array)
                            // 數據格式: [...]
                            messagesArray = response.Data;

                        if (messagesArray != null && messagesArray.Type == JTokenType.Array)
                            foreach (var messageToken in messagesArray)
                                try
                                {
                                    var chatMessage = ParseHistoryMessage(messageToken, isGroup, chatId);
                                    if (chatMessage != null)
                                    {
                                        historyMessages.Add(chatMessage);

                                        // 保存到數據庫（使用服務器的 message_id）
                                        var messageId = messageToken.Value<long>("message_id");
                                        DataAccess.SaveMessage(messageId, chatId, isGroup, chatMessage.Content,
                                            chatMessage.MessageType, chatMessage.SenderId, chatMessage.SenderName,
                                            chatMessage.IsFromMe, chatMessage.Timestamp, chatMessage.Segments);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"OneBotAPIHandler: 解析單條歷史消息時發生錯誤: {ex.Message}");
                                }
                        else
                            Debug.WriteLine($"OneBotAPIHandler: 歷史消息數據格式不正確: {response.Data.Type}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OneBotAPIHandler: 解析歷史消息數據時發生錯誤: {ex.Message}");
                    }

                    Debug.WriteLine($"OneBotAPIHandler: 成功解析 {historyMessages.Count} 條歷史消息");

                    // 在UI線程中更新界面
                    Task.Run(async () =>
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal,
                            () => { _mainView?.HandleHistoryMessages(historyMessages, chatId, isGroup); });
                    });
                }
                else
                {
                    Debug.WriteLine($"OneBotAPIHandler: 聊天歷史消息請求失敗 - Status: {response.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 處理聊天歷史消息響應時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     解析歷史消息 - 修正時間戳處理（改進版）
        /// </summary>
        private ChatMessage ParseHistoryMessage(JToken messageToken, bool isGroup, long chatId = 0)
        {
            try
            {
                var messageId = messageToken.Value<long>("message_id");
                var unixTimestamp = messageToken.Value<long>("time");

                // 正確處理Unix時間戳轉換
                DateTime timestamp;
                try
                {
                    // 將Unix時間戳轉換為UTC時間，然後轉為本地時間
                    var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                    timestamp = utcTime.ToLocalTime();

                    Debug.WriteLine(
                        $"歷史消息時間戳處理: Unix={unixTimestamp}, UTC={utcTime:yyyy-MM-dd HH:mm:ss}, Local={timestamp:yyyy-MM-dd HH:mm:ss}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"時間戳轉換失敗: {ex.Message}，使用當前時間");
                    timestamp = DateTime.Now;
                }

                var senderId = messageToken.Value<long>("user_id");
                var messageType = messageToken.Value<string>("message_type") ?? (isGroup ? "group" : "private");

                // 解析消息內容和段落
                var segments = new List<MessageSegment>();
                var plainText = "";

                // 嘗試獲取 raw_message
                if (messageToken["raw_message"] != null) plainText = messageToken.Value<string>("raw_message") ?? "";

                // 解析消息段（傳遞群組ID）
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

                // 確定發送者名稱
                var senderName = "";
                var isFromMe = false;
                var currentUserId = GetCurrentUserId();

                if (isGroup)
                {
                    // 群組消息
                    isFromMe = senderId == currentUserId;
                    if (isFromMe)
                    {
                        senderName = "我";
                    }
                    else
                    {
                        // 嘗試從 sender 信息獲取名稱
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
                    // 私聊消息
                    isFromMe = senderId == currentUserId;
                    if (isFromMe)
                        senderName = "我";
                    else
                        senderName = DataAccess.GetFriendNameById(senderId);
                }

                if (string.IsNullOrEmpty(senderName)) senderName = isFromMe ? "我" : $"用戶 {senderId}";

                var chatMessage = new ChatMessage
                {
                    Content = plainText,
                    Timestamp = timestamp, // 使用正確處理的時間戳
                    IsFromMe = isFromMe,
                    SenderName = senderName,
                    SenderId = senderId,
                    MessageType = messageType,
                    Segments = segments
                };

                Debug.WriteLine(
                    $"解析歷史消息: ID={messageId}, Time={timestamp:HH:mm}, Sender={senderName}, Content={plainText.Substring(0, Math.Min(50, plainText.Length))}...");
                return chatMessage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析歷史消息時發生錯誤: {ex.Message}");
                Debug.WriteLine($"消息數據: {messageToken}");
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
                var settings = DataAccess.GetAllDatas();
                var account = settings.Get("Account") ?? "";
                if (long.TryParse(account, out var userId)) return userId;
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
                Debug.WriteLine($"OneBotAPIHandler: 開始處理消息，MainView 引用: {(_mainView != null ? "有效" : "無效")}");

                // 檢查消息類型
                var postType = json.GetNamedString("post_type", "");
                var messageType = json.GetNamedString("message_type", "");

                Debug.WriteLine($"OneBotAPIHandler: post_type: {postType}, message_type: {messageType}");

                if (postType == "message" && (messageType == "group" || messageType == "private"))
                    // 處理接收到的消息
                    await HandleReceivedMessage(json, messageType);
                else if (postType == "message_sent" && (messageType == "group" || messageType == "private"))
                    // 處理發送的消息
                    await HandleSentMessage(json, messageType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理消息時發生錯誤: {ex.Message}");
                Debug.WriteLine($"錯誤堆疊: {ex.StackTrace}");
            }
        }

        /// <summary>
        ///     請求獲取指定消息的內容
        /// </summary>
        public async void RequestMessageContent(long messageId, string echo = null)
        {
            try
            {
                // 檢查是否已經在請求中，避免重複請求
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

                Debug.WriteLine($"OneBotAPIHandler: 已發送獲取消息請求 - MessageId: {messageId}");
                Debug.WriteLine($"請求 JSON: {jsonString}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 請求獲取消息時發生錯誤: {ex.Message}");
                _requestingMessages.Remove(messageId); // 出錯時移除請求狀態
            }
        }

        /// <summary>
        ///     處理 get_msg 響應 - 修正時間戳處理
        /// </summary>
        private void HandleGetMessageResponse(ResponseEntity response, string echo)
        {
            try
            {
                Debug.WriteLine("OneBotAPIHandler: 開始處理獲取消息響應");

                if (response.Status == "ok" && response.Data != null)
                {
                    // 解析 echo 以獲取 messageId
                    var messageIdStr = "";
                    if (echo.StartsWith("get_msg_reply_"))
                        messageIdStr = echo.Substring("get_msg_reply_".Length);
                    else if (echo.StartsWith("get_msg_"))
                        messageIdStr = echo.Substring("get_msg_".Length);

                    if (long.TryParse(messageIdStr, out var messageId))
                        try
                        {
                            // 正確處理時間戳
                            var unixTimestamp = response.Data.Value<long>("time");
                            DateTime timestamp;
                            try
                            {
                                var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                                timestamp = utcTime.ToLocalTime();
                                Debug.WriteLine(
                                    $"get_msg時間戳處理: Unix={unixTimestamp}, Local={timestamp:yyyy-MM-dd HH:mm:ss}");
                            }
                            catch
                            {
                                timestamp = DateTime.Now;
                            }

                            var senderId = response.Data.Value<long>("user_id");
                            var messageType = response.Data.Value<string>("message_type") ?? "text";
                            var isGroup = messageType == "group";
                            var chatId = isGroup ? response.Data.Value<long>("group_id") : senderId;

                            // 解析消息內容和段落
                            var segments = new List<MessageSegment>();
                            var plainText = "";

                            // 嘗試獲取 raw_message
                            if (response.Data["raw_message"] != null)
                                plainText = response.Data.Value<string>("raw_message") ?? "";

                            // 解析消息段
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

                            // 確定發送者名稱
                            var senderName = "";
                            var currentUserId = GetCurrentUserId();
                            var isFromMe = senderId == currentUserId;

                            if (isFromMe)
                            {
                                senderName = "我";
                            }
                            else if (isGroup)
                            {
                                // 嘗試從 sender 信息獲取名稱
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

                            // 保存到數據庫
                            DataAccess.SaveMessage(messageId, chatId, isGroup, plainText, messageType, senderId,
                                senderName, isFromMe, timestamp, segments);

                            // 清除請求狀態
                            _requestingMessages.Remove(messageId);

                            Debug.WriteLine(
                                $"OneBotAPIHandler: 成功獲取並保存消息 - MessageId: {messageId}, Content: {plainText}");

                            // 通知UI更新回復消息的顯示
                            Task.Run(async () =>
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Normal, () =>
                                    {
                                        // 刷新當前聊天界面，讓回復消息顯示更新後的內容
                                        _mainView?.RefreshCurrentChatMessages();
                                    });
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"OneBotAPIHandler: 解析獲取的消息時發生錯誤: {ex.Message}");
                        }
                }
                else
                {
                    Debug.WriteLine($"OneBotAPIHandler: 獲取消息請求失敗 - Status: {response.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 處理獲取消息響應時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     安全地獲取消息內容 - 改進版，支持完整的消息段解析，UWP 15063 兼容
        /// </summary>
        private MessageContentResult GetMessageContent(JsonObject json, long groupId = 0)
        {
            var plainText = "";
            var segments = new List<MessageSegment>();

            try
            {
                // 首先嘗試獲取 raw_message（純文本版本）
                if (json.ContainsKey("raw_message"))
                {
                    plainText = json.GetNamedString("raw_message", "");
                    Debug.WriteLine($"OneBotAPIHandler: 使用 raw_message: {plainText}");
                }

                // 解析消息段數組（傳遞群組ID）
                if (json.ContainsKey("message"))
                {
                    var messageValue = json.GetNamedValue("message");
                    if (messageValue.ValueType == JsonValueType.Array)
                    {
                        var messageJson = messageValue.ToString();
                        var messageArray = JToken.Parse(messageJson);

                        segments = MessageSegmentParser.ParseMessageArray(messageArray, groupId);
                        // 如果沒有 raw_message，從段落生成純文本
                        if (string.IsNullOrEmpty(plainText))
                            plainText = MessageSegmentParser.GenerateTextFromSegments(segments);

                        Debug.WriteLine($"OneBotAPIHandler: 解析到 {segments.Count} 個消息段，純文本: {plainText}");
                    }
                    else if (messageValue.ValueType == JsonValueType.String)
                    {
                        plainText = messageValue.GetString();
                        // 創建一個文本段
                        segments.Add(new TextSegment(plainText));
                        Debug.WriteLine($"OneBotAPIHandler: 使用字符串消息: {plainText}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析消息內容時發生錯誤: {ex.Message}");
                plainText = "[無法解析的消息]";
                segments = new List<MessageSegment> { new TextSegment(plainText) };
            }

            // 確保消息不為空
            if (string.IsNullOrEmpty(plainText))
            {
                plainText = "[空消息]";
                if (segments.Count == 0) segments.Add(new TextSegment(plainText));
            }

            return new MessageContentResult(plainText, segments);
        }

        /// <summary>
        ///     處理接收到的消息 - 更新版，支持富消息，UWP 15063 兼容
        /// </summary>
        private async Task HandleReceivedMessage(JsonObject json, string messageType)
        {
            var senderName = "";
            var actualSenderId = 0L; // 实际发送者ID（个人）
            var chatId = 0L; // 聊天ID（群组ID或好友ID）
            var isGroup = messageType == "group";

            // 获取消息ID
            var messageId = (long)json.GetNamedNumber("message_id", 0);

            // 获取并修正时间戳
            var currentTime = DateTime.Now;

            try
            {
                // 获取发送者信息
                if (messageType == "group")
                {
                    // 群组消息处理
                    var groupId = json.GetNamedNumber("group_id", 0);
                    chatId = (long)groupId;

                    // 传递群组ID进行消息解析
                    var messageContent = GetMessageContent(json, chatId);
                    var messageText = messageContent.PlainText;
                    var messageSegments = messageContent.Segments;

                    // 获取实际发送者ID（群组中的个人）
                    actualSenderId = (long)json.GetNamedNumber("user_id", 0);

                    // 从数据库获取群组名称
                    var groupName = DataAccess.GetGroupNameById(chatId);

                    // 获取实际发送者昵称（显示在消息内容中）
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
                        // 忽略发送者信息错误
                    }

                    if (string.IsNullOrEmpty(actualSenderName)) actualSenderName = "群成员";
                    senderName = actualSenderName;

                    Debug.WriteLine(
                        $"OneBotAPIHandler: 收到群组消息 - 群组: {groupName} (ID: {chatId}), 发送者: {actualSenderName} (ID: {actualSenderId}), 消息: {messageText}");

                    // 使用安全的异步方法保存到数据库
                    await SafeDataAccess.SaveMessageAsync(messageId, chatId, isGroup, messageText,
                        "group", actualSenderId, senderName, false, currentTime, messageSegments);

                    // 在 UI 线程中处理消息
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            // 添加到聊天界面和数据库，传递正确的参数
                            _mainView?.AddIncomingMessage(actualSenderName, chatId, actualSenderId, messageText, true,
                                messageSegments);

                            // 更新聊天列表（显示发送者: 消息内容）
                            var displayMessage = $"{actualSenderName}: {messageText}";
                            _mainView?.UpdateChatItem(groupName, displayMessage);

                            Debug.WriteLine("OneBotAPIHandler: 群组消息处理完成");
                        });
                }
                else
                {
                    // 私聊消息处理（不需要群组ID）
                    var messageContent = GetMessageContent(json);
                    var messageText = messageContent.PlainText;
                    var messageSegments = messageContent.Segments;
                    var userId = json.GetNamedNumber("user_id", 0);
                    chatId = (long)userId; // 聊天ID是好友ID
                    actualSenderId = (long)userId; // 发送者也是好友ID

                    // 从数据库获取好友名称
                    senderName = DataAccess.GetFriendNameById(chatId);

                    Debug.WriteLine($"OneBotAPIHandler: 收到私聊消息 - 好友: {senderName} (ID: {chatId}), 消息: {messageText}");

                    // 使用安全的异步方法保存到数据库
                    await SafeDataAccess.SaveMessageAsync(messageId, chatId, isGroup, messageText,
                        "private", actualSenderId, senderName, false, currentTime, messageSegments);

                    // 在 UI 线程中处理消息
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            // 添加到聊天界面和数据库，传递正确的参数
                            _mainView?.AddIncomingMessage(senderName, chatId, actualSenderId, messageText, false,
                                messageSegments);

                            // 更新聊天列表
                            _mainView?.UpdateChatItem(senderName, messageText);

                            Debug.WriteLine("OneBotAPIHandler: 私聊消息处理完成");
                        });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 处理接收消息时发生错误: {ex.Message}");
                Debug.WriteLine($"错误堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        ///     處理發送的消息 - 更新版，支持富消息，UWP 15063 兼容
        /// </summary>
        private async Task HandleSentMessage(JsonObject json, string messageType)
        {
            try
            {
                var targetName = "";
                var targetId = 0L;
                var isGroup = messageType == "group";

                // 安全地獲取消息內容和段落
                MessageContentResult messageContent;
                if (isGroup)
                {
                    // 群組消息：嘗試獲取群組ID
                    var groupId = json.GetNamedNumber("group_id", 0);
                    messageContent = GetMessageContent(json, (long)groupId);
                    targetId = (long)groupId;
                }
                else
                {
                    // 私聊消息：不需要群組ID
                    messageContent = GetMessageContent(json);
                }

                var messageText = messageContent.PlainText;
                var messageSegments = messageContent.Segments;

                // 獲取消息ID和當前用戶ID
                var messageId = (long)json.GetNamedNumber("message_id", 0);
                var currentUserId = (long)json.GetNamedNumber("self_id", 0);

                // 獲取目標信息
                if (messageType == "group")
                {
                    // 群組消息處理
                    // 從數據庫獲取群組名稱
                    targetName = DataAccess.GetGroupNameById(targetId);

                    Debug.WriteLine($"OneBotAPIHandler: 發送群組消息確認 - 群組: {targetName}, 消息: {messageText}");
                }
                else
                {
                    // 私聊消息處理 - 修復邏輯
                    // 首先嘗試獲取 target_id（發送給誰）
                    var targetUserId = json.GetNamedNumber("target_id", 0);

                    if (targetUserId == 0)
                    {
                        // 如果沒有 target_id，嘗試從 user_id 獲取（但這可能是發送者ID）
                        targetUserId = json.GetNamedNumber("user_id", 0);

                        // 檢查是否是自己發送的消息（message_sent_type: "self"）
                        var messageSentType = json.GetNamedString("message_sent_type", "");
                        var selfId = json.GetNamedNumber("self_id", 0);

                        if (messageSentType == "self" && targetUserId == selfId)
                            // 這是自己發送的消息，需要從其他字段獲取目標ID
                            // 檢查 raw 字段中的 peerUin
                            if (json.ContainsKey("raw"))
                                try
                                {
                                    var rawObject = json.GetNamedObject("raw");
                                    if (rawObject.ContainsKey("peerUin"))
                                    {
                                        var peerUin = rawObject.GetNamedString("peerUin", "");
                                        if (long.TryParse(peerUin, out var peerUserId))
                                        {
                                            targetUserId = peerUserId;
                                            Debug.WriteLine($"OneBotAPIHandler: 從 raw.peerUin 獲取目標ID: {targetUserId}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"OneBotAPIHandler: 解析 raw 字段時發生錯誤: {ex.Message}");
                                }
                    }

                    targetId = (long)targetUserId;

                    // 從數據庫獲取好友名稱
                    targetName = DataAccess.GetFriendNameById(targetId);

                    // 如果沒有找到好友名稱，使用默認名稱
                    if (targetName.StartsWith("用戶 ")) targetName = $"好友 {targetId}";

                    Debug.WriteLine(
                        $"OneBotAPIHandler: 發送私聊消息確認 - 目標好友: {targetName} (ID: {targetId}), 消息: {messageText}");
                }

                // 使用安全的異步方法保存到數據庫（發送的消息應該標记為 isFromMe = true）
                await SafeDataAccess.SaveMessageAsync(messageId, targetId, isGroup, messageText,
                    isGroup ? "group" : "private", currentUserId, "我", true, DateTime.Now, messageSegments);

                // 在 UI 線程中處理消息確認
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        // 檢查當前是否正在與目標聊天
                        if (_mainView?.IsCurrentChat(targetId, isGroup) == true)
                        {
                            // 如果正在與目標聊天，添加消息到聊天界面
                            _mainView?.AddOutgoingMessage("我", targetId, currentUserId, messageText, isGroup,
                                messageSegments);
                            Debug.WriteLine("OneBotAPIHandler: 已添加發送消息到當前聊天界面");
                        }

                        // 更新聊天列表（不增加未讀數量）
                        _mainView?.UpdateChatItem(targetName, $"我: {messageText}", false);

                        Debug.WriteLine("OneBotAPIHandler: 發送消息確認處理完成");
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 處理發送消息時發生錯誤: {ex.Message}");
                Debug.WriteLine($"錯誤堆疊: {ex.StackTrace}");
            }
        }

        /// <summary>
        ///     處理最近聯繫人響應 - 修改版：只更新UI，不保存消息內容到數據庫
        /// </summary>
        private void RecentContactHandler(ResponseEntity response)
        {
            try
            {
                Debug.WriteLine("OneBotAPIHandler: 開始處理最近聯繫人響應");

                if (response.Status == "ok" && response.Data != null)
                {
                    var recentMessages = new List<RecentContactMessage>();

                    try
                    {
                        Debug.WriteLine($"OneBotAPIHandler: 最近聯繫人數據類型: {response.Data.Type}");

                        if (response.Data.Type == JTokenType.Array)
                            foreach (var contactToken in response.Data)
                                try
                                {
                                    var recentMessage = ParseRecentContactMessage(contactToken);
                                    if (recentMessage != null) recentMessages.Add(recentMessage);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"OneBotAPIHandler: 解析單個最近聯繫人時發生錯誤: {ex.Message}");
                                }
                        else
                            Debug.WriteLine($"OneBotAPIHandler: 最近聯繫人數據不是數組類型: {response.Data.Type}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OneBotAPIHandler: 解析最近聯繫人數據時發生錯誤: {ex.Message}");
                    }

                    // 只更新UI界面，不保存消息內容到數據庫
                    Task.Run(async () =>
                    {
                        Debug.WriteLine($"OneBotAPIHandler: 成功處理 {recentMessages.Count} 條最近消息，僅更新UI");

                        // 在 UI 線程中刷新聊天列表
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal,
                            () => { _mainView?.LoadRecentContactsToUI(recentMessages); });
                    });
                }
                else
                {
                    Debug.WriteLine($"OneBotAPIHandler: 最近聯繫人請求失敗 - Status: {response.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 處理最近聯繫人響應時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     解析最近聯繫人消息 - 修復時間戳處理，支持富消息，UWP 15063 兼容
        /// </summary>
        private RecentContactMessage ParseRecentContactMessage(JToken contactToken)
        {
            try
            {
                // 注意：API 返回的字段名是 "lastestMsg" 而不是 "latestMsg"
                var latestMsgToken = contactToken["lastestMsg"];
                if (latestMsgToken == null)
                {
                    Debug.WriteLine("OneBotAPIHandler: 找不到 lastestMsg 字段，跳過此聯繫人");
                    return null;
                }

                // 獲取並處理時間戳
                var unixTimestamp = latestMsgToken.Value<long>("time");
                DateTime processedTimestamp;

                try
                {
                    // 使用正確的時間戳轉換方式
                    var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                    var localTime = utcTime.ToLocalTime();

                    // 使用 DataAccess 中的 ProcessTimestamp 方法進行進一步處理
                    processedTimestamp = DataAccess.ProcessTimestamp(localTime);

                    Debug.WriteLine(
                        $"最近聯繫人時間戳處理: Unix={unixTimestamp}, UTC={utcTime:yyyy-MM-dd HH:mm:ss}, Local={localTime:yyyy-MM-dd HH:mm:ss}, Processed={processedTimestamp:yyyy-MM-dd HH:mm:ss}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"最近聯繫人時間戳轉換失敗: {ex.Message}，使用當前時間");
                    processedTimestamp = DateTime.Now;
                }

                var message = new RecentContactMessage
                {
                    // 基本信息
                    SelfId = latestMsgToken.Value<long>("self_id"),
                    UserId = latestMsgToken.Value<long>("user_id"),
                    Time = unixTimestamp, // 保持原始Unix時間戳
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

                    // 添加處理後的時間戳字段
                    ProcessedTimestamp = processedTimestamp
                };

                // 解析發送者信息
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

                // 解析消息內容 - 支持消息段
                var messageArrayToken = latestMsgToken["message"];
                if (messageArrayToken != null && messageArrayToken.Type == JTokenType.Array)
                {
                    var segments = MessageSegmentParser.ParseMessageArray(messageArrayToken);
                    message.MessageSegments = segments; // 設置消息段
                    message.ParsedMessage = MessageSegmentParser.GenerateTextFromSegments(segments);
                }
                else if (!string.IsNullOrEmpty(message.RawMessage))
                {
                    message.ParsedMessage = message.RawMessage;
                    // 為純文本創建文本段
                    message.MessageSegments = new List<MessageSegment> { new TextSegment(message.RawMessage) };
                }
                else
                {
                    // 確保 MessageSegments 不為 null
                    message.MessageSegments = new List<MessageSegment>();
                }

                Debug.WriteLine(
                    $"OneBotAPIHandler: 成功解析最近聯繫人消息 - PeerName: {message.PeerName}, Time: {processedTimestamp:HH:mm}, Message: {message.ParsedMessage?.Substring(0, Math.Min(50, message.ParsedMessage?.Length ?? 0))}...");
                return message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析最近聯繫人消息時發生錯誤: {ex.Message}");
                Debug.WriteLine($"聯繫人數據: {contactToken}");
                return null;
            }
        }

        // 保留舊的方法作為兼容性（如果其他地方還在使用）
        private string GetMessageText(JsonObject json)
        {
            var content = GetMessageContent(json);
            return content.PlainText;
        }


        /// <summary>
        ///     处理好友列表响应 - 现在使用 JToken，並更新舊消息使用者信息
        /// </summary>
        private void FriendsWithCategoryHandler(ResponseEntity response)
        {
            try
            {
                Debug.WriteLine("OneBotAPIHandler: 開始處理好友列表響應");

                if (response.Status == "ok" && response.Data != null)
                {
                    var categories = new List<FriendCategory>();

                    try
                    {
                        Debug.WriteLine($"OneBotAPIHandler: 好友數據類型: {response.Data.Type}");

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

                                    Debug.WriteLine($"處理分類: {category.CategoryName}, ID: {category.CategoryId}");

                                    // 解析該分類下的好友列表
                                    var buddyListToken = categoryToken["buddyList"];
                                    if (buddyListToken != null && buddyListToken.Type == JTokenType.Array)
                                    {
                                        var friendCount = 0;
                                        foreach (var buddyToken in buddyListToken)
                                            try
                                            {
                                                var friend = new FriendInfo
                                                {
                                                    // 使用正確的欄位名
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

                                                    // 這些欄位在新的 API 響應中不存在，設置為預設值
                                                    Qid = "",
                                                    LongNick = "",
                                                    RichTime = 0,
                                                    Uid = "",
                                                    Uin = "",
                                                    Nick = buddyToken.Value<string>("nickname") ?? ""
                                                };

                                                Debug.WriteLine(
                                                    $"成功解析好友: UserId={friend.UserId}, Nickname={friend.Nickname}, Remark={friend.Remark}");

                                                category.BuddyList.Add(friend);
                                                friendCount++;
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.WriteLine($"OneBotAPIHandler: 解析單個好友時發生錯誤: {ex.Message}");
                                                Debug.WriteLine($"好友數據: {buddyToken}");
                                            }

                                        Debug.WriteLine($"分類 {category.CategoryName} 解析了 {friendCount} 個好友");
                                    }

                                    categories.Add(category);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"OneBotAPIHandler: 解析單個分類時發生錯誤: {ex.Message}");
                                }
                        else
                            Debug.WriteLine($"OneBotAPIHandler: 好友數據不是陣列類型: {response.Data.Type}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OneBotAPIHandler: 解析好友數據時發生錯誤: {ex.Message}");
                    }

                    // 保存到數據庫並重新整理界面
                    Task.Run(async () =>
                    {
                        DataAccess.SaveFriendsWithCategories(categories);
                        // 計算總好友數
                        var totalFriends = 0;
                        foreach (var category in categories)
                            if (category.BuddyList != null)
                                totalFriends += category.BuddyList.Count;

                        Debug.WriteLine($"OneBotAPIHandler: 成功處理並保存 {categories.Count} 個分類和 {totalFriends} 個好友");

                        // 新增：更新舊消息中的使用者信息
                        DataAccess.UpdateUserInfoInMessages();

                        // 數據保存完成後，在 UI 線程中重新整理界面
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, () =>
                            {
                                _mainView?.RefreshContactsAndGroups();
                                // 新增：更新聊天列表中的好友信息
                                _mainView?.UpdateFriendInfoInChatList();
                                Debug.WriteLine("已觸發聊天列表好友信息更新");
                            });
                    });
                }
                else
                {
                    Debug.WriteLine($"OneBotAPIHandler: 好友列表請求失敗 - Status: {response.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OneBotAPIHandler: 處理好友列表響應時發生錯誤: {ex.Message}");
            }
        }

        private async void LoginInfoHandler(ResponseEntity response)
        {
            var user_id = response.Data.Value<double>("user_id");
            var nickName = response.Data.Value<string>("nickname");

            Debug.WriteLine("response status:" + response.Status + "\r\nLogin ID:" + user_id + "\r\nNickName:" +
                            nickName);
            // 在 UI 線程中更新聊天列表
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () => { _mainView?.UpdateInfo(user_id, nickName); });
        }

        /// <summary>
        ///     消息內容結果類 - 用於替代 tuple，UWP 15063 兼容
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