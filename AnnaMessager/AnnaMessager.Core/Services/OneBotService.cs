using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.WebSocket;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AnnaMessager.Core.Services
{
    public class OneBotService : MvxNotifyPropertyChanged, IOneBotService
    {
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, TaskCompletionSource<string>> _pendingRequests;
        private bool _isConnected;
        private IWebSocketClient _webSocketClient;
        private long _apiCallCounter = 0;
        private string _currentServerUrl;

        public OneBotService()
        {
            _pendingRequests = new Dictionary<string, TaskCompletionSource<string>>();
        }

        public event EventHandler<MessageEventArgs> MessageReceived;
        public event EventHandler<NoticeEventArgs> NoticeReceived;
        public event EventHandler<RequestEventArgs> RequestReceived;
        public event EventHandler<MetaEventArgs> MetaEventReceived;
        public event EventHandler<bool> ConnectionStatusChanged;

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                SetProperty(ref _isConnected, value);
                ConnectionStatusChanged?.Invoke(this, value);
            }
        }

        public string CurrentServerUrl => _currentServerUrl;

        #region Connection Management

        public async Task<bool> ConnectAsync(string serverUrl, string token)
        {
            try
            {
                _currentServerUrl = serverUrl; // 保存當前連線地址

                if (_webSocketClient != null)
                    await DisconnectAsync();

                // 從 IoC 容器獲取平台特定的 WebSocket 實作
                _webSocketClient = Mvx.Resolve<IWebSocketClient>();

                if (!string.IsNullOrEmpty(token))
                    _webSocketClient.SetRequestHeader("Authorization", $"Bearer {token}");

                _webSocketClient.Opened += OnWebSocketOpened;
                _webSocketClient.MessageReceived += OnWebSocketMessageReceived;
                _webSocketClient.Error += OnWebSocketError;
                _webSocketClient.Closed += OnWebSocketClosed;

                await _webSocketClient.ConnectAsync(new Uri(serverUrl));

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"連接失敗: {ex.Message}");
                IsConnected = false;
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_webSocketClient != null)
                {
                    // 取消註冊事件處理器
                    _webSocketClient.Opened -= OnWebSocketOpened;
                    _webSocketClient.MessageReceived -= OnWebSocketMessageReceived;
                    _webSocketClient.Error -= OnWebSocketError;
                    _webSocketClient.Closed -= OnWebSocketClosed;

                    await _webSocketClient.CloseAsync();
                    _webSocketClient.Dispose();
                    _webSocketClient = null;
                }

                // 清理待处理的请求
                lock (_lockObject)
                {
                    foreach (var tcs in _pendingRequests.Values)
                        tcs.SetCanceled();
                    _pendingRequests.Clear();
                }

                IsConnected = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"断开连接时发生错误: {ex.Message}");
            }
        }

        #endregion

        #region WebSocket Event Handlers

        private void OnWebSocketOpened(object sender, EventArgs e)
        {
            IsConnected = true;
            Debug.WriteLine("WebSocket 連接成功");
        }

        private void OnWebSocketMessageReceived(object sender, WebSocketMessageEventArgs e)
        {
            try
            {
                var message = e.Text;
                Debug.WriteLine($"收到消息: {message}");

                ProcessIncomingMessage(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理 WebSocket 消消息時發生錯誤: {ex.Message}");
            }
        }

        private void OnWebSocketError(object sender, WebSocketErrorEventArgs e)
        {
            IsConnected = false;
            Debug.WriteLine($"WebSocket 錯誤: {e.Message}");
        }

        private void OnWebSocketClosed(object sender, WebSocketClosedEventArgs e)
        {
            IsConnected = false;
            Debug.WriteLine($"WebSocket 連接關閉: {e.Code} - {e.Reason}");
        }

        #endregion

        #region Message Processing

        private void ProcessIncomingMessage(string message)
        {
            try
            {
                var json = JObject.Parse(message);

                // 检查是否是API响应
                if (json.ContainsKey("echo"))
                {
                    ProcessApiResponse(message);
                    return;
                }

                // 检查是否是事件
                if (json.ContainsKey("post_type"))
                {
                    ProcessEvent(json);
                    return;
                }

                Debug.WriteLine($"未知消息类型: {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析消息失败: {ex.Message}");
            }
        }

        private void ProcessApiResponse(string message)
        {
            try
            {
                var response = JObject.Parse(message);
                var echo = response["echo"]?.ToString();

                if (!string.IsNullOrEmpty(echo))
                    lock (_lockObject)
                    {
                        if (_pendingRequests.TryGetValue(echo, out var tcs))
                        {
                            _pendingRequests.Remove(echo);
                            tcs.SetResult(message);
                        }
                    }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理API響應失败: {ex.Message}");
            }
        }

        private void ProcessEvent(JObject json)
        {
            try
            {
                var postType = json["post_type"]?.ToString();

                switch (postType)
                {
                    case "message":
                    case "message_sent": // 新增: 自身發送事件 (OneBot v11 擴展)
                        {
                            try
                            {
                                var cloned = json;
                                if (postType == "message_sent")
                                {
                                    // 統一 post_type 以便下游判斷邏輯一致
                                    cloned["post_type"] = "message";
                                    // OneBot/NapCat 的 message_sent 私聊事件包含 target_id(對端); 原模型使用 user_id 導致誤建自我聊天
                                    try
                                    {
                                        var msgType = cloned["message_type"]?.ToString();
                                        var targetId = cloned["target_id"];
                                        if (msgType == "private" && targetId != null)
                                        {
                                            // 將 user_id 改寫為對端，保留原 self_id
                                            cloned["user_id"] = targetId;
                                          }
                                    }
                                    catch (Exception adjustEx)
                                    {
                                        Debug.WriteLine("調整 message_sent target_id 失敗: " + adjustEx.Message);
                                    }
                                }
                                var messageEvent = cloned.ToObject<MessageEvent>();
                                MessageReceived?.Invoke(this, new MessageEventArgs { Message = messageEvent });
                            }
                            catch (Exception ex2)
                            {
                                Debug.WriteLine("反序列化 message/message_sent 事件失敗: " + ex2.Message);
                            }
                        }
                        break;

                    case "notice":
                        var noticeEvent = json.ToObject<NoticeEvent>();
                        NoticeReceived?.Invoke(this, new NoticeEventArgs { Notice = noticeEvent });
                        break;

                    case "request":
                        var requestEvent = json.ToObject<RequestEvent>();
                        RequestReceived?.Invoke(this, new RequestEventArgs { Request = requestEvent });
                        break;

                    case "meta_event":
                        var metaEvent = json.ToObject<MetaEvent>();
                        MetaEventReceived?.Invoke(this, new MetaEventArgs { Meta = metaEvent });
                        break;

                    default:
                        Debug.WriteLine($"未知事件类型: {postType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理事件失败: {ex.Message}");
            }
        }

        #endregion

        #region API Methods

        private async Task<OneBotResponse<T>> SendApiRequestAsync<T>(string action, object parameters = null)
        {
            var callId = Interlocked.Increment(ref _apiCallCounter);
            if (!IsConnected)
            {
                Debug.WriteLine($"[OneBotService][#{callId}] 尚未連線，忽略 API 呼叫: {action}");
                return new OneBotResponse<T>
                {
                    Status = "failed",
                    RetCode = -1,
                    Data = default
                };
            }

            var echo = Guid.NewGuid().ToString();
            var request = new OneBotRequest
            {
                Action = action,
                Params = parameters,
                Echo = echo
            };

            var tcs = new TaskCompletionSource<string>();
            lock (_lockObject)
            {
                _pendingRequests[echo] = tcs;
            }

            try
            {
                var json = JsonConvert.SerializeObject(request);

                Debug.WriteLine($"[OneBotService][#{callId}] 呼叫 {action} 開始 (已連線={IsConnected})");
                Debug.WriteLine($"[OneBotService][#{callId}] 送出 {action} 請求: {json}");

                await _webSocketClient.SendAsync(json);

                // 等待響应，设置30秒超时
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    cts.Token.Register(() => tcs.TrySetCanceled());

                    var responseJson = await tcs.Task;
                    Debug.WriteLine($"[OneBotService][#{callId}] 收到 {action} 響應: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}...");
                    return JsonConvert.DeserializeObject<OneBotResponse<T>>(responseJson);
                }
            }
            catch (TaskCanceledException)
            {
                lock (_lockObject)
                {
                    _pendingRequests.Remove(echo);
                }

                Debug.WriteLine($"[OneBotService][#{callId}] {action} 超時");
                return new OneBotResponse<T>
                {
                    Status = "failed",
                    RetCode = -2, // 超时
                    Data = default
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OneBotService][#{callId}] {action} 發送失敗: {ex.Message}");

                lock (_lockObject)
                {
                    _pendingRequests.Remove(echo);
                }

                return new OneBotResponse<T>
                {
                    Status = "failed",
                    RetCode = -3, // 其他错误
                    Data = default
                };
            }
        }

        // 消息 API 實作
        public async Task<OneBotResponse<object>> SendPrivateMessageAsync(long userId, string message,
            bool autoEscape = false)
        {
            var parameters = new
            {
                user_id = userId,
                message,
                auto_escape = autoEscape
            };

            return await SendApiRequestAsync<object>("send_private_msg", parameters);
        }

        public async Task<OneBotResponse<object>> SendGroupMessageAsync(long groupId, string message,
            bool autoEscape = false)
        {
            var parameters = new
            {
                group_id = groupId,
                message,
                auto_escape = autoEscape
            };

            return await SendApiRequestAsync<object>("send_group_msg", parameters);
        }

        public async Task<OneBotResponse<object>> SendMessageAsync(long chatId, string message, bool isGroup = false,
            bool autoEscape = false)
        {
            if (isGroup) return await SendGroupMessageAsync(chatId, message, autoEscape);

            return await SendPrivateMessageAsync(chatId, message, autoEscape);
        }

        public async Task<OneBotResponse<object>> DeleteMessageAsync(long messageId)
        {
            return await SendApiRequestAsync<object>("delete_msg", new { message_id = messageId });
        }

        public async Task<OneBotResponse<object>> GetMessageAsync(long messageId)
        {
            return await SendApiRequestAsync<object>("get_msg", new { message_id = messageId });
        }

        public async Task<OneBotResponse<MessageHistoryData>> GetForwardMessageAsync(long id)
        {
            return await SendApiRequestAsync<MessageHistoryData>("get_forward_msg", new { id });
        }

        public async Task<OneBotResponse<object>> SendLikeAsync(long userId, int times = 1)
        {
            return await SendApiRequestAsync<object>("send_like", new { user_id = userId, times });
        }

        // 群管理 API
        public async Task<OneBotResponse<object>> SetGroupKickAsync(long groupId, long userId,
            bool rejectAddRequest = false)
        {
            return await SendApiRequestAsync<object>("set_group_kick", new
            {
                group_id = groupId,
                user_id = userId,
                reject_add_request = rejectAddRequest
            });
        }

        public async Task<OneBotResponse<object>> SetGroupBanAsync(long groupId, long userId, int duration = 30 * 60)
        {
            return await SendApiRequestAsync<object>("set_group_ban", new
            {
                group_id = groupId,
                user_id = userId,
                duration
            });
        }

        public async Task<OneBotResponse<object>> SetGroupAnonymousBanAsync(long groupId, AnonymousInfo anonymous,
            int duration = 30 * 60)
        {
            return await SendApiRequestAsync<object>("set_group_anonymous_ban", new
            {
                group_id = groupId,
                anonymous,
                duration
            });
        }

        public async Task<OneBotResponse<object>> SetGroupWholeBanAsync(long groupId, bool enable = true)
        {
            return await SendApiRequestAsync<object>("set_group_whole_ban", new
            {
                group_id = groupId,
                enable
            });
        }

        public async Task<OneBotResponse<object>> SetGroupAdminAsync(long groupId, long userId, bool enable = true)
        {
            return await SendApiRequestAsync<object>("set_group_admin", new
            {
                group_id = groupId,
                user_id = userId,
                enable
            });
        }

        public async Task<OneBotResponse<object>> SetGroupAnonymousAsync(long groupId, bool enable = true)
        {
            return await SendApiRequestAsync<object>("set_group_anonymous", new
            {
                group_id = groupId,
                enable
            });
        }

        public async Task<OneBotResponse<object>> SetGroupCardAsync(long groupId, long userId, string card)
        {
            return await SendApiRequestAsync<object>("set_group_card", new
            {
                group_id = groupId,
                user_id = userId,
                card
            });
        }

        public async Task<OneBotResponse<object>> SetGroupNameAsync(long groupId, string groupName)
        {
            return await SendApiRequestAsync<object>("set_group_name", new
            {
                group_id = groupId,
                group_name = groupName
            });
        }

        public async Task<OneBotResponse<object>> SetGroupLeaveAsync(long groupId, bool isDismiss = false)
        {
            return await SendApiRequestAsync<object>("set_group_leave", new
            {
                group_id = groupId,
                is_dismiss = isDismiss
            });
        }

        public async Task<OneBotResponse<object>> SetGroupSpecialTitleAsync(long groupId, long userId,
            string specialTitle, int duration = -1)
        {
            return await SendApiRequestAsync<object>("set_group_special_title", new
            {
                group_id = groupId,
                user_id = userId,
                special_title = specialTitle,
                duration
            });
        }

        // 请求处理 API
        public async Task<OneBotResponse<object>> SetFriendAddRequestAsync(string flag, bool approve = true,
            string remark = "")
        {
            return await SendApiRequestAsync<object>("set_friend_add_request", new
            {
                flag,
                approve,
                remark
            });
        }

        public async Task<OneBotResponse<object>> SetGroupAddRequestAsync(string flag, string subType,
            bool approve = true, string reason = "")
        {
            return await SendApiRequestAsync<object>("set_group_add_request", new
            {
                flag,
                sub_type = subType,
                approve,
                reason
            });
        }

        // 获取信息 API - 修正：返回 LoginInfoData
        public async Task<OneBotResponse<LoginInfoData>> GetLoginInfoAsync()
        {
            return await SendApiRequestAsync<LoginInfoData>("get_login_info");
        }

        public async Task<OneBotResponse<StrangerInfo>> GetStrangerInfoAsync(long userId, bool noCache = false)
        {
            var parameters = new
            {
                user_id = userId,
                no_cache = noCache
            };
            return await SendApiRequestAsync<StrangerInfo>("get_stranger_info", parameters);
        }

        public async Task<OneBotResponse<List<FriendInfo>>> GetFriendListAsync()
        {
            return await SendApiRequestAsync<List<FriendInfo>>("get_friend_list");
        }

        public async Task<OneBotResponse<List<GroupInfo>>> GetGroupListAsync()
        {
            return await SendApiRequestAsync<List<GroupInfo>>("get_group_list");
        }

        public async Task<OneBotResponse<GroupInfo>> GetGroupInfoAsync(long groupId, bool noCache = false)
        {
            var parameters = new
            {
                group_id = groupId,
                no_cache = noCache
            };
            return await SendApiRequestAsync<GroupInfo>("get_group_info", parameters);
        }

        public async Task<OneBotResponse<List<GroupMemberInfo>>> GetGroupMemberListAsync(long groupId)
        {
            var parameters = new { group_id = groupId };
            return await SendApiRequestAsync<List<GroupMemberInfo>>("get_group_member_list", parameters);
        }

        public async Task<OneBotResponse<GroupMemberInfo>> GetGroupMemberInfoAsync(long groupId, long userId,
            bool noCache = false)
        {
            var parameters = new
            {
                group_id = groupId,
                user_id = userId,
                no_cache = noCache
            };
            return await SendApiRequestAsync<GroupMemberInfo>("get_group_member_info", parameters);
        }

        public async Task<OneBotResponse<object>> GetCookiesAsync(string domain = "")
        {
            return await SendApiRequestAsync<object>("get_cookies", new { domain });
        }

        public async Task<OneBotResponse<object>> GetCsrfTokenAsync()
        {
            return await SendApiRequestAsync<object>("get_csrf_token");
        }

        public async Task<OneBotResponse<object>> GetCredentialsAsync(string domain = "")
        {
            return await SendApiRequestAsync<object>("get_credentials", new { domain });
        }

        // 修正：使用 OneBotFileInfo 而非 FileInfo
        public async Task<OneBotResponse<OneBotFileInfo>> GetRecordAsync(string file, string outFormat)
        {
            var parameters = new
            {
                file,
                out_format = outFormat
            };
            return await SendApiRequestAsync<OneBotFileInfo>("get_record", parameters);
        }

        public async Task<OneBotResponse<OneBotFileInfo>> GetImageAsync(string file)
        {
            var parameters = new { file };
            return await SendApiRequestAsync<OneBotFileInfo>("get_image", parameters);
        }

        public async Task<OneBotResponse<OneBotFileInfo>> GetFileAsync(string fileId)
        {
            var parameters = new { file_id = fileId };
            return await SendApiRequestAsync<OneBotFileInfo>("get_file", parameters);
        }

        public async Task<OneBotResponse<object>> CanSendImageAsync()
        {
            return await SendApiRequestAsync<object>("can_send_image");
        }

        public async Task<OneBotResponse<object>> CanSendRecordAsync()
        {
            return await SendApiRequestAsync<object>("can_send_record");
        }

        public async Task<OneBotResponse<object>> GetStatusAsync()
        {
            return await SendApiRequestAsync<object>("get_status");
        }

        public async Task<OneBotResponse<object>> GetVersionInfoAsync()
        {
            return await SendApiRequestAsync<object>("get_version_info");
        }

        public async Task<OneBotResponse<object>> SetRestartAsync(int delay = 0)
        {
            return await SendApiRequestAsync<object>("set_restart", new { delay });
        }

        public async Task<OneBotResponse<object>> CleanCacheAsync()
        {
            return await SendApiRequestAsync<object>("clean_cache");
        }

        public async Task<OneBotResponse<object>> GetUserStatusAsync(long userId)
        {
            // NapCat 擴展接口名稱推測為 nc_get_user_status
            var parameters = new { user_id = userId };
            return await SendApiRequestAsync<object>("nc_get_user_status", parameters);
        }

        #endregion

        #region NapCat 擴展 API Methods

        // 歷史消息 API
        public async Task<OneBotResponse<MessageHistoryResponse>> GetFriendMsgHistoryAsync(long userId,
            int messageSeq = 0, int count = 20)
        {
            var parameters = new
            {
                user_id = userId,
                message_seq = messageSeq,
                count
            };

            return await SendApiRequestAsync<MessageHistoryResponse>("get_friend_msg_history", parameters);
        }

        public async Task<OneBotResponse<MessageHistoryResponse>> GetGroupMsgHistoryAsync(long groupId,
            int messageSeq = 0, int count = 20)
        {
            var parameters = new
            {
                group_id = groupId,
                message_seq = messageSeq,
                count
            };

            return await SendApiRequestAsync<MessageHistoryResponse>("get_group_msg_history", parameters);
        }

        // 精華消息 API
        public async Task<OneBotResponse<List<EssenceMessage>>> GetEssenceMsgListAsync(long groupId)
        {
            var parameters = new { group_id = groupId };
            return await SendApiRequestAsync<List<EssenceMessage>>("get_essence_msg_list", parameters);
        }

        public async Task<OneBotResponse<object>> SetEssenceMsgAsync(long messageId)
        {
            var parameters = new { message_id = messageId };
            return await SendApiRequestAsync<object>("set_essence_msg", parameters);
        }

        public async Task<OneBotResponse<object>> DeleteEssenceMsgAsync(long messageId)
        {
            var parameters = new { message_id = messageId };
            return await SendApiRequestAsync<object>("delete_essence_msg", parameters);
        }

        // 群文件 API
        public async Task<OneBotResponse<List<GroupFileInfo>>> GetGroupFileSystemInfoAsync(long groupId)
        {
            var parameters = new { group_id = groupId };
            return await SendApiRequestAsync<List<GroupFileInfo>>("get_group_file_system_info", parameters);
        }

        public async Task<OneBotResponse<List<GroupFolderInfo>>> GetGroupRootFilesAsync(long groupId)
        {
            var parameters = new { group_id = groupId };
            return await SendApiRequestAsync<List<GroupFolderInfo>>("get_group_root_files", parameters);
        }

        public async Task<OneBotResponse<List<GroupFileInfo>>> GetGroupFilesByFolderAsync(long groupId, string folderId)
        {
            var parameters = new
            {
                group_id = groupId,
                folder_id = folderId
            };
            return await SendApiRequestAsync<List<GroupFileInfo>>("get_group_files_by_folder", parameters);
        }

        public async Task<OneBotResponse<object>> UploadGroupFileAsync(long groupId, string file, string name,
            string folder = "/")
        {
            var parameters = new
            {
                group_id = groupId,
                file,
                name,
                folder
            };
            return await SendApiRequestAsync<object>("upload_group_file", parameters);
        }

        public async Task<OneBotResponse<object>> DeleteGroupFileAsync(long groupId, string fileId, int busid)
        {
            var parameters = new
            {
                group_id = groupId,
                file_id = fileId,
                busid
            };
            return await SendApiRequestAsync<object>("delete_group_file", parameters);
        }

        public async Task<OneBotResponse<object>> CreateGroupFileFolderAsync(long groupId, string name,
            string parentId = "/")
        {
            var parameters = new
            {
                group_id = groupId,
                name,
                parent_id = parentId
            };
            return await SendApiRequestAsync<object>("create_group_file_folder", parameters);
        }

        public async Task<OneBotResponse<object>> DeleteGroupFolderAsync(long groupId, string folderId)
        {
            var parameters = new
            {
                group_id = groupId,
                folder_id = folderId
            };
            return await SendApiRequestAsync<object>("delete_group_folder", parameters);
        }

        public async Task<OneBotResponse<string>> GetGroupFileUrlAsync(long groupId, string fileId, int busid)
        {
            var parameters = new
            {
                group_id = groupId,
                file_id = fileId,
                busid
            };
            return await SendApiRequestAsync<string>("get_group_file_url", parameters);
        }

        // 其他 API
        public async Task<OneBotResponse<object>> MarkMsgAsReadAsync(long messageId)
        {
            var parameters = new { message_id = messageId };
            return await SendApiRequestAsync<object>("mark_msg_as_read", parameters);
        }

        // 更新歷史消息方法
        public async Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId,
            bool isGroup = false)
        {
            if (isGroup)
            {
                var groupHistory = await GetGroupMsgHistoryAsync(chatId);
                if (groupHistory.Status == "ok" && groupHistory.Data?.Messages != null)
                    return new OneBotResponse<MessageHistoryData>
                    {
                        Status = "ok",
                        RetCode = 0,
                        Data = new MessageHistoryData
                        {
                            Messages = groupHistory.Data.Messages.Select(m => new HistoryMessage
                            {
                                MessageId = m.MessageId,
                                RealId = m.RealId,
                                Sender = new SenderInfo
                                {
                                    UserId = m.Sender.UserId,
                                    Nickname = m.Sender.Nickname,
                                    Card = m.Sender.Card,
                                    Role = m.Sender.Role
                                },
                                Time = m.Time,
                                Message = m.Message
                            }).ToList()
                        }
                    };
            }
            else
            {
                var friendHistory = await GetFriendMsgHistoryAsync(chatId);
                if (friendHistory.Status == "ok" && friendHistory.Data?.Messages != null)
                    return new OneBotResponse<MessageHistoryData>
                    {
                        Status = "ok",
                        RetCode = 0,
                        Data = new MessageHistoryData
                        {
                            Messages = friendHistory.Data.Messages.Select(m => new HistoryMessage
                            {
                                MessageId = m.MessageId,
                                RealId = m.RealId,
                                Sender = new SenderInfo
                                {
                                    UserId = m.Sender.UserId,
                                    Nickname = m.Sender.Nickname
                                },
                                Time = m.Time,
                                Message = m.Message
                            }).ToList()
                        }
                    };
            }

            return new OneBotResponse<MessageHistoryData>
            {
                Status = "failed",
                RetCode = -1,
                Data = new MessageHistoryData
                {
                    Messages = new List<HistoryMessage>()
                }
            };
        }

        // 兼容多載 (簡單包裝，暫不使用額外參數)
        public Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count)
            => GetMessageHistoryAsync(chatId, isGroup);
        public Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count, int startSeq)
            => GetMessageHistoryAsync(chatId, isGroup);
        public Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count, int startSeq, int direction)
            => GetMessageHistoryAsync(chatId, isGroup);
        public Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count, int startSeq, int direction, bool includeSelf)
            => GetMessageHistoryAsync(chatId, isGroup);
        public Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count, int startSeq, int direction, bool includeSelf, bool forceRemote)
            => GetMessageHistoryAsync(chatId, isGroup);
        // 實作接口要求的別名方法 (介面新增後需補齊)
        public Task<OneBotResponse<object>> SendPrivateMsgAsync(long userId, string message, bool autoEscape = false)
            => SendPrivateMessageAsync(userId, message, autoEscape);
        public Task<OneBotResponse<object>> SendGroupMsgAsync(long groupId, string message, bool autoEscape = false)
            => SendGroupMessageAsync(groupId, message, autoEscape);
        public Task<OneBotResponse<List<FriendCategoryItem>>> GetFriendsWithCategoryAsync()
            => SendApiRequestAsync<List<FriendCategoryItem>>("get_friends_with_category");
        public Task<OneBotResponse<List<RecentContact>>> GetRecentContactAsync(int count = 30)
            => SendApiRequestAsync<List<RecentContact>>("get_recent_contact", new { count });
        #endregion // NapCat 擴展 API Methods 結束

        public async Task<List<MessageHistoryItem>> TryGetRecentMessagesAsync(long id, bool isGroup, int limit)
        {
            try
            {
                OneBotResponse<MessageHistoryResponse> resp = isGroup
                    ? await GetGroupMsgHistoryAsync(id, 0, limit)
                    : await GetFriendMsgHistoryAsync(id, 0, limit);
                if (resp != null && resp.Status == "ok" && resp.Data != null && resp.Data.Messages != null)
                {
                    return resp.Data.Messages;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryGetRecentMessagesAsync 失敗: {ex.Message}");
            }
            return new List<MessageHistoryItem>();
        }
    }
}