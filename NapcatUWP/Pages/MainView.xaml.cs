using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using NapcatUWP.Controls;
using NapcatUWP.Models;
using NapcatUWP.Tools;
using Newtonsoft.Json;

namespace NapcatUWP.Pages
{
    public sealed partial class MainView : Page
    {
        private readonly ObservableCollection<ChatMessage> _currentMessages;
        private readonly HashSet<string> _loadedHistoryChats;

        private readonly Random _random = new Random();

        // 添加當前帳號追蹤
        private string _currentAccount = "";
        private ChatItem _currentChat;
        private string _currentPage = "Chats";
        private bool _sidebarOpen;

        public MainView()
        {
            InitializeComponent();
            InitializeAvatorAndInfo();
            SidebarColumn.Width = new GridLength(0);
            UpdateOverlay();

            // 獲取當前帳號
            _currentAccount = DataAccess.GetCurrentAccount();
            Debug.WriteLine($"當前帳號: {_currentAccount}");

            // 先載入緩存的聊天列表
            LoadCachedChatList();

            // 設置默認選中項
            ChatsItem.IsSelected = true;

            // 設置 API 處理器的 MainView 引用
            MainPage.SocketClientStarter.SetMainViewReference(this);

            // 加載好友和群組數據
            LoadContactsAndGroups();

            // 初始化聊天消息集合
            _currentMessages = new ObservableCollection<ChatMessage>();
            MessagesItemsControl.ItemsSource = _currentMessages;
            _loadedHistoryChats = new HashSet<string>();
            // 添加聊天列表點擊事件
            ChatListView.SelectionChanged += ChatListView_SelectionChanged;

            // 註冊應用程序關閉事件
            RegisterApplicationEvents();
        }


        public ObservableCollection<ChatItem> ChatItems { get; set; }
        public ObservableCollection<GroupInfo> GroupItems { get; set; }

        public ObservableCollection<FriendCategory> ContactCategories { get; set; }

        /// <summary>
        ///     載入緩存的聊天列表
        /// </summary>
        private void LoadCachedChatList()
        {
            try
            {
                ChatItems = new ObservableCollection<ChatItem>();

                if (!string.IsNullOrEmpty(_currentAccount))
                {
                    var cachedItems = DataAccess.LoadChatListCache(_currentAccount);

                    foreach (var item in cachedItems) ChatItems.Add(item);

                    Debug.WriteLine($"載入緩存聊天列表: {cachedItems.Count} 個項目");
                }
                else
                {
                    Debug.WriteLine("沒有當前帳號，初始化空聊天列表");
                }

                ChatListView.ItemsSource = ChatItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存聊天列表錯誤: {ex.Message}");
                ChatItems = new ObservableCollection<ChatItem>();
                ChatListView.ItemsSource = ChatItems;
            }
        }

        /// <summary>
        ///     保存聊天列表緩存
        /// </summary>
        private void SaveChatListCache()
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentAccount) && ChatItems != null && ChatItems.Count > 0)
                {
                    DataAccess.SaveChatListCache(_currentAccount, ChatItems);
                    Debug.WriteLine($"保存聊天列表緩存: {ChatItems.Count} 個項目");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存聊天列表緩存錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     註冊應用程序事件
        /// </summary>
        private void RegisterApplicationEvents()
        {
            try
            {
                // 註冊應用程序掛起事件
                CoreApplication.Suspending += OnApplicationSuspending;

                Debug.WriteLine("應用程序事件註冊完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"註冊應用程序事件錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     應用程序掛起時保存聊天列表
        /// </summary>
        private void OnApplicationSuspending(object sender, SuspendingEventArgs e)
        {
            Debug.WriteLine("應用程序掛起，保存聊天列表緩存");
            SaveChatListCache();
        }

        /// <summary>
        ///     更新用戶信息並檢查帳號變化
        /// </summary>
        public void UpdateInfo(double id, string name)
        {
            TextUser.Text = name;
            TextID.Text = id.ToString();

            // 檢查帳號是否改變
            var newAccount = id.ToString();
            if (_currentAccount != newAccount)
            {
                Debug.WriteLine($"帳號發生變化: {_currentAccount} -> {newAccount}");

                // 保存舊帳號的聊天列表（如果有的話）
                if (!string.IsNullOrEmpty(_currentAccount)) SaveChatListCache();

                // 更新當前帳號
                _currentAccount = newAccount;

                // 清空當前聊天列表，準備載入新帳號的數據
                ChatItems.Clear();

                // 載入新帳號的緩存（如果有的話）
                var newCachedItems = DataAccess.LoadChatListCache(_currentAccount);
                foreach (var item in newCachedItems) ChatItems.Add(item);

                Debug.WriteLine($"載入新帳號的緩存聊天列表: {newCachedItems.Count} 個項目");
            }
        }

        /// <summary>
        ///     將最近聯繫人載入到UI
        /// </summary>
        public void LoadRecentContactsToUI(List<RecentContactMessage> recentMessages)
        {
            try
            {
                // 從最近消息創建聊天項目
                var newChatItems = DataAccess.CreateChatItemsFromRecentMessages(recentMessages);

                Debug.WriteLine($"從最近聯繫人載入 {newChatItems.Count} 個聊天項目到UI");

                // 合併到現有的聊天列表
                foreach (var newItem in newChatItems)
                {
                    // 檢查是否已存在相同的聊天
                    var existingChat = ChatItems.FirstOrDefault(c =>
                        c.ChatId == newItem.ChatId && c.IsGroup == newItem.IsGroup);

                    if (existingChat != null)
                    {
                        // 更新現有聊天的信息
                        existingChat.LastMessage = newItem.LastMessage;
                        existingChat.LastTime = newItem.LastTime;
                        existingChat.MemberCount = newItem.MemberCount;

                        // 移動到列表頂部
                        ChatItems.Remove(existingChat);
                        ChatItems.Insert(0, existingChat);
                    }
                    else
                    {
                        // 添加新的聊天項目
                        ChatItems.Insert(0, newItem);
                    }
                }

                Debug.WriteLine($"UI更新完成，聊天列表總數: {ChatItems.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入最近聯繫人到UI錯誤: {ex.Message}");
            }
        }

        // 添加聊天列表選擇事件處理
        private void ChatListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ChatItem selectedChat) OpenChat(selectedChat);
        }

        // 修改 OpenChat 方法以支持首次載入歷史消息
        private void OpenChat(ChatItem chatItem)
        {
            _currentChat = chatItem;

            // 設置聊天標題
            if (chatItem.IsGroup)
            {
                ChatTitleText.Text = chatItem.Name;
                ChatSubtitleText.Text = $"{chatItem.MemberCount} 成員";
            }
            else
            {
                ChatTitleText.Text = chatItem.Name;
                ChatSubtitleText.Text = $"QQ號：{chatItem.ChatId}";
            }

            // 清空當前消息並加載聊天記錄
            _currentMessages.Clear();

            // 檢查是否第一次打開此聊天
            var chatKey = $"{chatItem.ChatId}_{chatItem.IsGroup}";
            if (!_loadedHistoryChats.Contains(chatKey))
            {
                // 第一次打開，先從服務器獲取歷史消息
                LoadHistoryFromServer(chatItem);
                _loadedHistoryChats.Add(chatKey);
            }
            else
            {
                // 非第一次打開，直接從數據庫載入
                LoadChatHistory(chatItem);
            }

            // 清除未讀數
            chatItem.UnreadCount = 0;

            // 切換到聊天界面
            SwitchToChatInterface();

            // 確保滾動到底部
            EnsureScrollToBottom();

            Debug.WriteLine($"打開聊天: {chatItem.Name} (ID: {chatItem.ChatId}, 是否群組: {chatItem.IsGroup})");
        }

        /// <summary>
        ///     從服務器載入歷史消息（首次打開聊天時調用）
        /// </summary>
        private async void LoadHistoryFromServer(ChatItem chatItem)
        {
            try
            {
                Debug.WriteLine($"首次打開聊天，從服務器載入歷史消息: {chatItem.Name}");

                // 先從數據庫載入現有消息
                var existingMessages = DataAccess.GetChatMessages(chatItem.ChatId, chatItem.IsGroup);
                foreach (var message in existingMessages) _currentMessages.Add(message);

                // 如果沒有現有消息，顯示載入提示
                if (existingMessages.Count == 0)
                {
                    var loadingMessage = new ChatMessage
                    {
                        Content = "正在載入聊天記錄...",
                        Timestamp = DateTime.Now,
                        IsFromMe = false,
                        SenderName = "系統",
                        SenderId = -1,
                        MessageType = "system"
                    };
                    _currentMessages.Add(loadingMessage);
                }

                // 請求服務器歷史消息
                await RequestChatHistory(chatItem.ChatId, chatItem.IsGroup);

                Debug.WriteLine($"已請求聊天歷史消息: {chatItem.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"從服務器載入歷史消息錯誤: {ex.Message}");
                // 如果載入失敗，顯示錯誤消息
                var errorMessage = new ChatMessage
                {
                    Content = "載入聊天記錄失敗",
                    Timestamp = DateTime.Now,
                    IsFromMe = false,
                    SenderName = "系統",
                    SenderId = -1,
                    MessageType = "system"
                };
                _currentMessages.Add(errorMessage);
            }
        }

        /// <summary>
        ///     請求聊天歷史消息
        /// </summary>
        private async Task RequestChatHistory(long chatId, bool isGroup)
        {
            try
            {
                if (isGroup)
                {
                    // 請求群組歷史消息
                    var requestData = new
                    {
                        action = "get_group_msg_history",
                        @params = new
                        {
                            group_id = chatId,
                            count = 20,
                            reverseOrder = false
                        },
                        echo = $"get_group_msg_history_{chatId}"
                    };

                    var jsonString = JsonConvert.SerializeObject(requestData);
                    await MainPage.SocketClientStarter._socket.Send(jsonString);

                    Debug.WriteLine($"已發送群組歷史消息請求: {jsonString}");
                }
                else
                {
                    // 請求好友歷史消息
                    var requestData = new
                    {
                        action = "get_friend_msg_history",
                        @params = new
                        {
                            user_id = chatId,
                            count = 20,
                            reverseOrder = false
                        },
                        echo = $"get_friend_msg_history_{chatId}"
                    };

                    var jsonString = JsonConvert.SerializeObject(requestData);
                    await MainPage.SocketClientStarter._socket.Send(jsonString);

                    Debug.WriteLine($"已發送好友歷史消息請求: {jsonString}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求聊天歷史消息失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理從服務器獲取的歷史消息並更新界面
        /// </summary>
        public async void HandleHistoryMessages(List<ChatMessage> historyMessages, long chatId, bool isGroup)
        {
            try
            {
                Debug.WriteLine($"收到歷史消息: {historyMessages.Count} 條");

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        // 檢查是否是當前聊天的歷史消息
                        if (_currentChat != null && _currentChat.ChatId == chatId && _currentChat.IsGroup == isGroup)
                        {
                            // 清除系統提示消息
                            var systemMessages = _currentMessages.Where(m => m.MessageType == "system").ToList();
                            foreach (var msg in systemMessages) _currentMessages.Remove(msg);

                            // 添加歷史消息到當前消息列表的開頭
                            var sortedMessages = historyMessages.OrderBy(m => m.Timestamp).ToList();
                            foreach (var message in sortedMessages)
                            {
                                // 檢查消息是否已存在（避免重複顯示）
                                var existingMessage = _currentMessages.FirstOrDefault(m =>
                                    m.Timestamp == message.Timestamp &&
                                    m.SenderId == message.SenderId &&
                                    m.Content == message.Content);

                                if (existingMessage == null) _currentMessages.Insert(0, message);
                            }

                            // 如果沒有歷史消息，顯示歡迎消息
                            if (_currentMessages.Count == 0)
                            {
                                var welcomeMessage = new ChatMessage
                                {
                                    Content = isGroup ? "歡迎來到群組聊天！" : "開始聊天吧！",
                                    Timestamp = DateTime.Now.AddMinutes(-1),
                                    IsFromMe = false,
                                    SenderName = "系統",
                                    SenderId = -1,
                                    MessageType = "system"
                                };
                                _currentMessages.Add(welcomeMessage);
                            }

                            // 滾動到底部
                            EnsureScrollToBottom();

                            Debug.WriteLine($"已更新聊天界面，總消息數: {_currentMessages.Count}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理歷史消息錯誤: {ex.Message}");
            }
        }


        // 切換到聊天界面
        private void SwitchToChatInterface()
        {
            // 隱藏所有列表頁面
            ChatListView.Visibility = Visibility.Collapsed;
            ContactsScrollViewer.Visibility = Visibility.Collapsed;
            GroupListView.Visibility = Visibility.Collapsed;
            SettingsGrid.Visibility = Visibility.Collapsed;

            // 顯示聊天界面
            ChatInterfaceGrid.Visibility = Visibility.Visible;

            // 更新當前頁面狀態
            _currentPage = "Chat";
        }

        // 返回按鈕點擊事件
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 隱藏聊天界面
            ChatInterfaceGrid.Visibility = Visibility.Collapsed;

            // 清除聊天列表的選擇狀態
            ChatListView.SelectedItem = null;

            // 返回到聊天列表
            SwitchPage("Chats");
        }

        // 發送按鈕點擊事件
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        // 消息輸入框按鍵事件
        private void MessageInputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                SendMessage();
                e.Handled = true;
            }
        }

        // 修改 SendMessage 方法 - 不要立即添加到UI，等服務器確認
        private void SendMessage()
        {
            var messageText = MessageInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(messageText) || _currentChat == null)
                return;

            // 清空輸入框（立即清空，給用戶即時反饋）
            MessageInputTextBox.Text = "";

            // 更新聊天列表中的最後消息（樂觀更新）
            _currentChat.LastMessage = $"我: {messageText}";
            _currentChat.LastTime = DateTime.Now.ToString("HH:mm");

            // 發送消息到服務器，不要立即添加到UI
            SendMessageToServer(messageText);

            Debug.WriteLine($"發送消息到 {_currentChat.Name}: {messageText}");
        }

        // 修改 SendMessageToServer 方法使用正確的 API 格式
        private async void SendMessageToServer(string messageText)
        {
            if (_currentChat == null) return;

            try
            {
                if (_currentChat.IsGroup)
                {
                    // 群組消息 - 使用正確的消息格式
                    var messageData = new[]
                    {
                        new
                        {
                            type = "text",
                            data = new { text = messageText }
                        }
                    };

                    var actionParams = new
                    {
                        group_id = _currentChat.ChatId,
                        message = messageData
                    };

                    var action = new
                    {
                        action = "send_group_msg",
                        @params = actionParams,
                        echo = "send_group_msg"
                    };

                    var jsonString = JsonConvert.SerializeObject(action);
                    await MainPage.SocketClientStarter._socket.Send(jsonString);

                    Debug.WriteLine(
                        $"ChatId={_currentChat.ChatId}, IsGroup={_currentChat.IsGroup}, Content={messageText}, Sender=我");
                    Debug.WriteLine($"發送群組消息JSON: {jsonString}");
                }
                else
                {
                    // 私聊消息 - 使用正確的消息格式
                    var messageData = new[]
                    {
                        new
                        {
                            type = "text",
                            data = new { text = messageText }
                        }
                    };

                    var actionParams = new
                    {
                        user_id = _currentChat.ChatId,
                        message = messageData
                    };

                    var action = new
                    {
                        action = "send_private_msg",
                        @params = actionParams,
                        echo = "send_private_msg"
                    };

                    var jsonString = JsonConvert.SerializeObject(action);
                    await MainPage.SocketClientStarter._socket.Send(jsonString);

                    Debug.WriteLine($"發送私聊消息JSON: {jsonString}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"發送消息失敗: {ex.Message}");
            }
        }

        // 加載聊天記錄
        // 修改 LoadChatHistory 方法（用於非首次打開的情況）
        private async void LoadChatHistory(ChatItem chatItem)
        {
            try
            {
                // 從數據庫加載聊天記錄
                var messages = DataAccess.GetChatMessages(chatItem.ChatId, chatItem.IsGroup);

                foreach (var message in messages) _currentMessages.Add(message);

                // 如果沒有歷史消息，顯示歡迎消息
                if (messages.Count == 0)
                {
                    var welcomeMessage = new ChatMessage
                    {
                        Content = chatItem.IsGroup ? "歡迎來到群組聊天！" : "開始聊天吧！",
                        Timestamp = DateTime.Now.AddMinutes(-1),
                        IsFromMe = false,
                        SenderName = "系統",
                        SenderId = -1,
                        MessageType = "system"
                    };
                    _currentMessages.Add(welcomeMessage);
                }

                // 延遲滾動確保消息已加載
                await Task.Delay(100);
                EnsureScrollToBottom();

                Debug.WriteLine($"從數據庫加載聊天記錄: {chatItem.Name}, 消息數: {messages.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加載聊天記錄錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     <summary>
        ///         檢查指定的聊天是否是當前正在進行的聊天
        ///     </summary>
        public bool IsCurrentChat(long chatId, bool isGroup)
        {
            return _currentChat != null && _currentChat.ChatId == chatId && _currentChat.IsGroup == isGroup;
        }

        /// <summary>
        ///     添加發出的消息到當前聊天界面
        /// </summary>
        public void AddOutgoingMessage(string senderName, long chatId, long actualSenderId, string messageText,
            bool isGroup, List<MessageSegment> segments = null)
        {
            try
            {
                // 檢查消息是否屬於當前聊天
                if (_currentChat != null && _currentChat.ChatId == chatId && _currentChat.IsGroup == isGroup)
                {
                    var message = new ChatMessage
                    {
                        Content = messageText,
                        Timestamp = DateTime.Now,
                        IsFromMe = true, // 發出的消息
                        SenderName = senderName,
                        SenderId = actualSenderId,
                        MessageType = isGroup ? "group" : "private",
                        Segments = segments ?? new List<MessageSegment>()
                    };

                    _currentMessages.Add(message);

                    // 自動滾動到底部
                    EnsureScrollToBottom();

                    Debug.WriteLine($"添加發出消息到當前聊天: {senderName}, 內容: {messageText}, 段落數: {segments?.Count ?? 0}");
                }
                else
                {
                    Debug.WriteLine(
                        $"發出消息不屬於當前聊天 - 當前聊天: {_currentChat?.Name} (ID: {_currentChat?.ChatId}, IsGroup: {_currentChat?.IsGroup}), 消息聊天: (ID: {chatId}, IsGroup: {isGroup})");
                }

                // 注意：數據庫保存操作已在 OneBotAPIHandler 中處理，這裡不再重複保存
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"添加發出消息失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     刷新當前聊天的消息（例如，當群組成員信息更新後）
        /// </summary>
        public void RefreshCurrentChatMessages()
        {
            try
            {
                if (_currentMessages == null) return;

                Debug.WriteLine("RefreshCurrentChatMessages: 開始刷新當前聊天消息");

                // 觸發消息重新渲染
                foreach (var message in _currentMessages)
                    if (message.Segments != null)
                    {
                        var hasAtSegment = false;
                        foreach (var segment in message.Segments)
                            if (segment is AtSegment atSegment && atSegment.GroupId > 0)
                            {
                                // 刷新 @ 消息段的顯示文本
                                atSegment.RefreshDisplayText();
                                hasAtSegment = true;
                            }

                        if (hasAtSegment)
                        {
                            // 通過重新設置 Segments 屬性來觸發 UI 更新
                            var segments = message.Segments;
                            message.Segments = new List<MessageSegment>(segments);
                        }
                    }

                Debug.WriteLine("RefreshCurrentChatMessages: 消息刷新完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshCurrentChatMessages: 刷新消息時發生錯誤: {ex.Message}");
            }
        }

        // 修復滾動到底部方法 - 使用 UWP 15063 相容的方法
        private async void ScrollToBottom()
        {
            try
            {
                // 使用 CoreDispatcher.RunAsync 代替 Dispatcher.BeginInvoke
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        // 等待UI更新
                        MessageScrollViewer.UpdateLayout();

                        // 滾動到底部
                        MessageScrollViewer.ChangeView(null, MessageScrollViewer.ScrollableHeight, null, false);

                        Debug.WriteLine($"滾動到底部: ScrollableHeight={MessageScrollViewer.ScrollableHeight}");
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"滾動失敗: {ex.Message}");
            }
        }

        // 確保滾動到底部的輔助方法 - UWP 15063 相容版本
        private async void EnsureScrollToBottom()
        {
            // 延遲一小段時間確保UI更新完成
            await Task.Delay(50);

            // 檢查是否需要滾動
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () =>
                {
                    MessageScrollViewer.UpdateLayout();
                    var scrollableHeight = MessageScrollViewer.ScrollableHeight;
                    var verticalOffset = MessageScrollViewer.VerticalOffset;

                    Debug.WriteLine($"滾動檢查: ScrollableHeight={scrollableHeight}, VerticalOffset={verticalOffset}");

                    if (scrollableHeight > 0 && verticalOffset < scrollableHeight - 10)
                    {
                        MessageScrollViewer.ChangeView(null, scrollableHeight, null, false);
                        Debug.WriteLine("執行滾動到底部");
                    }
                });
        }

        // 修改 AddIncomingMessage 方法，移除數據庫保存操作（已在 OneBotAPIHandler 中處理）
        public void AddIncomingMessage(string senderName, long chatId, long actualSenderId, string messageText,
            bool isGroup,
            List<MessageSegment> segments = null)
        {
            try
            {
                // 檢查消息是否屬於當前聊天
                if (_currentChat != null && _currentChat.ChatId == chatId && _currentChat.IsGroup == isGroup)
                {
                    var message = new ChatMessage
                    {
                        Content = messageText,
                        Timestamp = DateTime.Now,
                        IsFromMe = false,
                        SenderName = senderName,
                        SenderId = actualSenderId,
                        MessageType = isGroup ? "group" : "private",
                        Segments = segments ?? new List<MessageSegment>() // 設置消息段
                    };

                    _currentMessages.Add(message);

                    // 自動滾動到底部
                    EnsureScrollToBottom();

                    Debug.WriteLine($"添加接收消息到當前聊天: {senderName}, 內容: {messageText}, 段落數: {segments?.Count ?? 0}");
                }
                else
                {
                    Debug.WriteLine(
                        $"消息不屬於當前聊天 - 當前聊天: {_currentChat?.Name} (ID: {_currentChat?.ChatId}, IsGroup: {_currentChat?.IsGroup}), 消息聊天: (ID: {chatId}, IsGroup: {isGroup})");
                }

                // 注意：數據庫保存操作已在 OneBotAPIHandler 中處理，這裡不再重複保存
                Debug.WriteLine($"消息處理完成: ChatId={chatId}, IsGroup={isGroup}, Sender={senderName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"添加接收消息失敗: {ex.Message}");
            }
        }

        // 為了兼容性，保留舊的方法簽名，但增加一個重載
        public void AddIncomingMessage(string senderName, long senderId, string messageText, bool isGroup,
            List<MessageSegment> segments = null)
        {
            // 舊的方法：senderId 既是聊天ID也是發送者ID
            AddIncomingMessage(senderName, senderId, senderId, messageText, isGroup, segments);
        }

        // 修改 InitializeChatList 方法，如果沒有緩存才添加示例
        private void InitializeChatList()
        {
            // InitializeChatList 現在已經在構造函數中通過 LoadCachedChatList 處理
            // 如果沒有緩存數據且沒有真實數據，則添加示例
            if (ChatItems.Count == 0)
            {
                var groups = DataAccess.GetAllGroups();
                var friendCategories = DataAccess.GetAllFriendsWithCategories();

                // 只在沒有真實數據和緩存數據時添加示例
                if (groups.Count == 0 && friendCategories.Count == 0)
                    ChatItems.Add(new ChatItem
                    {
                        Name = "技術交流群",
                        LastMessage = "今天的會議改到下午3點",
                        LastTime = "上午 10:30",
                        UnreadCount = 3,
                        AvatarColor = "#FF4A90E2",
                        ChatId = 123456789,
                        IsGroup = true,
                        MemberCount = 25
                    });
            }
        }

        private void LoadContactsAndGroups()
        {
            try
            {
                // 直接在 UI 線程上調用（如果數據量不大）
                var friendCategories = DataAccess.GetAllFriendsWithCategories();
                ContactCategories = new ObservableCollection<FriendCategory>(friendCategories);
                BuildContactsUI();

                var groups = DataAccess.GetAllGroups();
                GroupItems = new ObservableCollection<GroupInfo>(groups);
                GroupListView.ItemsSource = GroupItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加載聯繫人和群組數據時發生錯誤: {ex.Message}");
            }
        }

        private void BuildContactsUI()
        {
            ContactsStackPanel.Children.Clear();

            // 添加調試信息
            Debug.WriteLine($"BuildContactsUI: 開始構建界面，共有 {ContactCategories?.Count ?? 0} 個分類");

            if (ContactCategories == null) return;

            foreach (var category in ContactCategories)
            {
                Debug.WriteLine($"BuildContactsUI: 處理分類 {category.CategoryName}，好友數: {category.BuddyList?.Count ?? 0}");

                // 創建分類標題 - 添加點擊事件
                var categoryHeader = new Grid
                {
                    Height = 40,
                    Background = new SolidColorBrush(Color.FromArgb(255, 45, 62, 80)),
                    Margin = new Thickness(0, 0, 0, 1),
                    Tag = category // 保存分類引用
                };

                // 添加點擊事件
                categoryHeader.Tapped += CategoryHeader_Tapped;

                var headerStackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };

                // 展開/收起圖標
                var expandIcon = new FontIcon
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Glyph = category.IsExpanded ? "\uE70D" : "\uE70E", // 向下箭頭：展開，向右箭頭：收起
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var headerText = new TextBlock
                {
                    Text = $"{category.CategoryName} ({category.BuddyList?.Count ?? 0})",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };

                headerStackPanel.Children.Add(expandIcon);
                headerStackPanel.Children.Add(headerText);
                categoryHeader.Children.Add(headerStackPanel);
                ContactsStackPanel.Children.Add(categoryHeader);

                // 創建好友列表 - 只有在展開狀態才顯示
                if (category.IsExpanded && category.BuddyList != null)
                    foreach (var friend in category.BuddyList)
                    {
                        var friendItem = CreateFriendItem(friend);
                        ContactsStackPanel.Children.Add(friendItem);
                    }
            }

            Debug.WriteLine($"BuildContactsUI: 界面構建完成，總共添加了 {ContactsStackPanel.Children.Count} 個控件");
        }

        private void CategoryHeader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.Tag is FriendCategory category)
            {
                // 切換展開狀態
                category.IsExpanded = !category.IsExpanded;

                // 重新構建UI
                BuildContactsUI();

                Debug.WriteLine($"分類 {category.CategoryName} 切換為 {(category.IsExpanded ? "展開" : "收起")}");
            }
        }

        private Grid CreateFriendItem(FriendInfo friend)
        {
            var grid = new Grid
            {
                Height = 60,
                Margin = new Thickness(0, 0, 0, 1),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 頭像
            var avatar = new Border
            {
                Width = 40,
                Height = 40,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(255, 76, 144, 226)),
                CornerRadius = new CornerRadius(20)
            };

            var avatarIcon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Glyph = "\uE13D",
                FontSize = 20,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            avatar.Child = avatarIcon;
            Grid.SetColumn(avatar, 0);
            grid.Children.Add(avatar);

            // 好友信息
            var infoPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var displayName = !string.IsNullOrEmpty(friend.Remark) ? friend.Remark :
                !string.IsNullOrEmpty(friend.Nickname) ? friend.Nickname : friend.Nick;

            var nameText = new TextBlock
            {
                Text = displayName,
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            // 修改狀態文字：顯示 QQ號 而不是等級
            var statusText = new TextBlock
            {
                Text = $"QQ號：{friend.UserId}",
                Foreground = new SolidColorBrush(Colors.LightGray),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            infoPanel.Children.Add(nameText);
            infoPanel.Children.Add(statusText);
            Grid.SetColumn(infoPanel, 1);
            grid.Children.Add(infoPanel);

            return grid;
        }

        private void FilterContactsList(string searchText)
        {
            ContactsStackPanel.Children.Clear();

            foreach (var category in ContactCategories)
            {
                var filteredFriends = string.IsNullOrEmpty(searchText)
                    ? category.BuddyList
                    : category.BuddyList?.Where(friend =>
                            (!string.IsNullOrEmpty(friend.Remark) && friend.Remark.ToLower().Contains(searchText)) ||
                            (!string.IsNullOrEmpty(friend.Nickname) &&
                             friend.Nickname.ToLower().Contains(searchText)) ||
                            (!string.IsNullOrEmpty(friend.Nick) && friend.Nick.ToLower().Contains(searchText)) ||
                            friend.UserId.ToString().Contains(searchText) // 添加按 QQ號 搜索
                    ).ToList();

                if (filteredFriends?.Any() == true)
                {
                    // 創建分類標題 - 搜索時總是展開
                    var categoryHeader = new Grid
                    {
                        Height = 40,
                        Background = new SolidColorBrush(Color.FromArgb(255, 45, 62, 80)),
                        Margin = new Thickness(0, 0, 0, 1),
                        Tag = category
                    };

                    var headerStackPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };

                    var expandIcon = new FontIcon
                    {
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        Glyph = "\uE70D", // 搜索時總是顯示為展開
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var headerText = new TextBlock
                    {
                        Text = $"{category.CategoryName} ({filteredFriends.Count})",
                        Foreground = new SolidColorBrush(Colors.White),
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    headerStackPanel.Children.Add(expandIcon);
                    headerStackPanel.Children.Add(headerText);
                    categoryHeader.Children.Add(headerStackPanel);
                    ContactsStackPanel.Children.Add(categoryHeader);

                    // 添加過濾後的好友
                    foreach (var friend in filteredFriends)
                    {
                        var friendItem = CreateFriendItem(friend);
                        ContactsStackPanel.Children.Add(friendItem);
                    }
                }
            }
        }

        private void SwitchPage(string pageName)
        {
            _currentPage = pageName;

            // 隱藏所有頁面
            ChatListView.Visibility = Visibility.Collapsed;
            ContactsScrollViewer.Visibility = Visibility.Collapsed;
            GroupListView.Visibility = Visibility.Collapsed;
            SettingsGrid.Visibility = Visibility.Collapsed;

            // 更新搜索框提示文字
            switch (pageName)
            {
                case "Chats":
                    ChatListView.Visibility = Visibility.Visible;
                    SearchTextBox.PlaceholderText = "Search (Chats)";
                    // 清除聊天列表的選擇狀態
                    ChatListView.SelectedItem = null;
                    break;
                case "Contacts":
                    ContactsScrollViewer.Visibility = Visibility.Visible;
                    SearchTextBox.PlaceholderText = "Search (Contacts)";
                    break;
                case "Groups":
                    GroupListView.Visibility = Visibility.Visible;
                    SearchTextBox.PlaceholderText = "Search (Groups)";
                    break;
                case "Settings":
                    SettingsGrid.Visibility = Visibility.Visible;
                    SearchTextBox.PlaceholderText = "Search (Settings)";
                    break;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.ToLower();

            switch (_currentPage)
            {
                case "Chats":
                    FilterChatList(searchText);
                    break;
                case "Contacts":
                    FilterContactsList(searchText);
                    break;
                case "Groups":
                    FilterGroupsList(searchText);
                    break;
            }
        }

        private void FilterChatList(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                ChatListView.ItemsSource = ChatItems;
            }
            else
            {
                var filteredItems = ChatItems.Where(item =>
                    item.Name.ToLower().Contains(searchText) ||
                    item.LastMessage.ToLower().Contains(searchText)).ToList();
                ChatListView.ItemsSource = filteredItems;
            }
        }


        private void FilterGroupsList(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                GroupListView.ItemsSource = GroupItems;
            }
            else
            {
                var filteredItems = GroupItems.Where(group =>
                        group.GroupName.ToLower().Contains(searchText) ||
                        (!string.IsNullOrEmpty(group.GroupRemark) && group.GroupRemark.ToLower().Contains(searchText)))
                    .ToList();
                GroupListView.ItemsSource = filteredItems;
            }
        }

        #region 公共更新方法（用於數據刷新）

        public void RefreshContactsAndGroups()
        {
            LoadContactsAndGroups();
        }

        #endregion

        #region 聊天列表更新方法（修復群組和私聊識別）

        // 修改 UpdateChatItem 方法以正確處理群組和私聊
        public void UpdateChatItem(string chatName, string newMessage, bool incrementUnread = true)
        {
            var existingChat = FindChatItemByName(chatName);
            if (existingChat != null)
            {
                existingChat.LastMessage = newMessage;
                existingChat.LastTime = DateTime.Now.ToString("HH:mm");

                if (incrementUnread) existingChat.UnreadCount++;

                ChatItems.Remove(existingChat);
                ChatItems.Insert(0, existingChat);
            }
            else
            {
                // 新的聊天項目，需要確定是群組還是私聊
                AddChatItemFromMessage(chatName, newMessage, incrementUnread ? 1 : 0);
            }
        }

        // 新方法：從消息創建聊天項目（自動識別群組/私聊）
        private void AddChatItemFromMessage(string chatName, string lastMessage, int unreadCount = 0)
        {
            // 先檢查是否為群組
            var groups = DataAccess.GetAllGroups();
            var group = groups.FirstOrDefault(g => g.GroupName.Equals(chatName, StringComparison.OrdinalIgnoreCase));

            if (group != null)
            {
                // 是群組
                AddChatItem(chatName, lastMessage, unreadCount, null, group.GroupId, true, group.MemberCount);
                Debug.WriteLine($"添加群組聊天項目: {chatName}, ID: {group.GroupId}");
            }
            else
            {
                // 檢查是否為好友
                var friendCategories = DataAccess.GetAllFriendsWithCategories();
                FriendInfo friend = null;

                foreach (var category in friendCategories)
                    if (category.BuddyList != null)
                    {
                        friend = category.BuddyList.FirstOrDefault(f =>
                            (!string.IsNullOrEmpty(f.Remark) &&
                             f.Remark.Equals(chatName, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(f.Nickname) &&
                             f.Nickname.Equals(chatName, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(f.Nick) &&
                             f.Nick.Equals(chatName, StringComparison.OrdinalIgnoreCase)));

                        if (friend != null) break;
                    }

                if (friend != null)
                {
                    // 是好友
                    AddChatItem(chatName, lastMessage, unreadCount, null, friend.UserId);
                    Debug.WriteLine($"添加好友聊天項目: {chatName}, ID: {friend.UserId}");
                }
                else
                {
                    // 未知聊天，默認為私聊
                    AddChatItem(chatName, lastMessage, unreadCount, null, 0);
                    Debug.WriteLine($"添加未知聊天項目: {chatName}");
                }
            }
        }

        // 修改 AddChatItem 方法以支持更多參數
        public void AddChatItem(string name, string lastMessage, int unreadCount = 0, string avatarColor = null,
            long chatId = 0, bool isGroup = false, int memberCount = 0)
        {
            if (string.IsNullOrEmpty(avatarColor)) avatarColor = GetRandomAvatarColor();

            var newChatItem = new ChatItem
            {
                Name = name,
                LastMessage = lastMessage,
                LastTime = DateTime.Now.ToString("HH:mm"),
                UnreadCount = unreadCount,
                AvatarColor = avatarColor,
                ChatId = chatId,
                IsGroup = isGroup,
                MemberCount = memberCount
            };

            ChatItems.Insert(0, newChatItem);
        }

        #endregion

        #region 聊天列表更新方法（保持原有功能）

        // 修改現有的 AddChatItem 方法以支持新的屬性
        public void AddChatItem(string name, string lastMessage, int unreadCount = 0, string avatarColor = null)
        {
            if (string.IsNullOrEmpty(avatarColor)) avatarColor = GetRandomAvatarColor();

            var newChatItem = new ChatItem
            {
                Name = name,
                LastMessage = lastMessage,
                LastTime = DateTime.Now.ToString("HH:mm"),
                UnreadCount = unreadCount,
                AvatarColor = avatarColor,
                ChatId = 0, // 需要根據實際情況設置
                IsGroup = false, // 需要根據實際情況設置
                MemberCount = 0
            };

            ChatItems.Insert(0, newChatItem);
        }

        public ChatItem FindChatItemByName(string name)
        {
            return ChatItems.FirstOrDefault(chat =>
                chat.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public bool RemoveChatItem(string chatName)
        {
            var chatToRemove = FindChatItemByName(chatName);
            if (chatToRemove != null)
            {
                ChatItems.Remove(chatToRemove);
                return true;
            }

            return false;
        }

        public void ClearUnreadCount(string chatName)
        {
            var chat = FindChatItemByName(chatName);
            if (chat != null) chat.UnreadCount = 0;
        }

        #endregion

        #region 界面事件處理

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarOpen)
                SidebarColumn.Width = new GridLength(0);
            else
                SidebarColumn.Width = new GridLength(280);
            _sidebarOpen = !_sidebarOpen;
            UpdateOverlay();
        }

        private void OverlayRect_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_sidebarOpen)
            {
                SidebarColumn.Width = new GridLength(0);
                _sidebarOpen = false;
                UpdateOverlay();
            }
        }

        // 修改登出處理以保存聊天列表緩存
        private async void SidebarListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as ListView;
            var selectedItem = listView.SelectedItem as ListViewItem;

            if (selectedItem != null)
            {
                var tag = selectedItem.Tag?.ToString();

                switch (tag)
                {
                    case "Chats":
                        SwitchPage("Chats");
                        break;
                    case "Contacts":
                        SwitchPage("Contacts");
                        break;
                    case "Groups":
                        SwitchPage("Groups");
                        break;
                    case "Settings":
                        SwitchPage("Settings");
                        break;
                    case "LogOut":
                        // 登出前保存聊天列表緩存
                        Debug.WriteLine("準備登出，保存聊天列表緩存");
                        SaveChatListCache();

                        LogoutMask.Visibility = Visibility.Visible;
                        LogoutProgressRing.IsActive = true;

                        MainPage.SocketClientStarter._socket.Close(1000, "logout");
                        await Task.Delay(600);

                        LogoutProgressRing.IsActive = false;
                        LogoutMask.Visibility = Visibility.Collapsed;
                        Frame.Navigate(typeof(MainPage));
                        break;
                }

                // 自動關閉側邊欄
                if (_sidebarOpen && tag != "LogOut")
                {
                    SidebarColumn.Width = new GridLength(0);
                    _sidebarOpen = false;
                    UpdateOverlay();
                }

                // 清除搜索
                SearchTextBox.Text = "";
            }
        }

        #endregion

        #region 輔助方法

        private void UpdateOverlay()
        {
            OverlayRect.Visibility = _sidebarOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void InitializeAvatorAndInfo()
        {
            await MainPage.SocketClientStarter._socket.Send(JSONTools.ActionToJSON("get_login_info", new JsonObject(),
                "login_info"));
        }


        private string GetRandomAvatarColor()
        {
            var colors = new[]
            {
                "#FF4A90E2", "#FF50C878", "#FFE74C3C", "#FF9B59B6",
                "#FFF39C12", "#FF3498DB", "#FF2ECC71", "#FFE67E22",
                "#FF9B59B6", "#FF1ABC9C", "#FF34495E", "#FFF1C40F"
            };

            return colors[_random.Next(colors.Length)];
        }

        #endregion
    }
}