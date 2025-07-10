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
using Windows.UI.Xaml.Navigation;
using NapcatUWP.Controls;
using NapcatUWP.Models;
using NapcatUWP.Tools;
using Newtonsoft.Json;

namespace NapcatUWP.Pages
{
    public sealed partial class MainView : Page
    {
        // 添加好友列表緩存字典
        private readonly Dictionary<long, List<FriendInfo>> _categoryFriendsCache =
            new Dictionary<long, List<FriendInfo>>();

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

            // 註冊系統返回鍵處理
            RegisterBackButtonHandler();

            // 新增：在後台更新用戶信息和修復時間戳
            Task.Run(() =>
            {
                DataAccess.UpdateUserInfoInMessages();
                DataAccess.FixTimestampIssue();
            });
            // 测试视频播放器（5秒后自动测试）
            Task.Delay(5000).ContinueWith(_ =>
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { TestVideoPlayer(); });
            });
        }

        // 更新测试方法
        private void TestVideoPlayer()
        {
            try
            {
                Debug.WriteLine("MainView: 开始测试视频播放器");
                Debug.WriteLine($"MainView: VideoPlayerOverlay 是否为 null: {VideoPlayerOverlay == null}");
                Debug.WriteLine(
                    $"MainView: VideoPlayerOverlayContainer 是否为 null: {VideoPlayerOverlayContainer == null}");

                if (VideoPlayerOverlay != null && VideoPlayerOverlayContainer != null)
                {
                    Debug.WriteLine(
                        $"MainView: VideoPlayerOverlayContainer 类型: {VideoPlayerOverlayContainer.GetType().Name}");
                    Debug.WriteLine(
                        $"MainView: VideoPlayerOverlayContainer 当前可见性: {VideoPlayerOverlayContainer.Visibility}");

                    // 测试显示视频播放器
                    VideoPlayerOverlayContainer.Visibility = Visibility.Visible;
                    Debug.WriteLine($"MainView: 设置后的可见性: {VideoPlayerOverlayContainer.Visibility}");

                    // 延迟一秒后隐藏
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            VideoPlayerOverlayContainer.Visibility = Visibility.Collapsed;
                            Debug.WriteLine("MainView: 测试完成，隐藏视频播放器");
                        });
                    });
                }
                else
                {
                    Debug.WriteLine("MainView: VideoPlayerOverlay 或 VideoPlayerOverlayContainer 为 null！");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView: 测试视频播放器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理消息段控件的视频播放请求
        /// </summary>
        private void MessageSegmentControl_VideoPlayRequested(object sender, VideoPlayEventArgs e)
        {
            Debug.WriteLine(
                $"MainView: 收到视频播放请求 - Sender: {sender?.GetType().Name}, URL: {e.VideoUrl}, Title: {e.Title}");
            PlayVideo(e.VideoUrl, e.Title);
        }

        /// <summary>
        /// 处理视频播放请求
        /// </summary>
        /// <param name="videoUrl">视频URL</param>
        /// <param name="title">视频标题</param>
        public void PlayVideo(string videoUrl, string title = "视频播放")
        {
            try
            {
                Debug.WriteLine($"MainView: 请求播放视频 - URL: {videoUrl}, Title: {title}");
                Debug.WriteLine($"MainView: VideoPlayerOverlay 是否为 null: {VideoPlayerOverlay == null}");
                Debug.WriteLine(
                    $"MainView: VideoPlayerOverlayContainer 是否为 null: {VideoPlayerOverlayContainer == null}");

                if (VideoPlayerOverlay != null && VideoPlayerOverlayContainer != null)
                {
                    Debug.WriteLine($"MainView: 视频播放器当前可见性: {VideoPlayerOverlayContainer.Visibility}");

                    // 显示视频播放器容器
                    VideoPlayerOverlayContainer.Visibility = Visibility.Visible;
                    Debug.WriteLine($"MainView: 设置视频播放器容器可见性为 Visible");

                    VideoPlayerOverlay.PlayVideo(videoUrl, title);
                    Debug.WriteLine($"MainView: 调用视频播放器播放方法完成");

                    Debug.WriteLine($"MainView: 视频播放器最终可见性: {VideoPlayerOverlayContainer.Visibility}");
                }
                else
                {
                    Debug.WriteLine("MainView: VideoPlayerOverlay 或 VideoPlayerOverlayContainer 为 null！");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView: 播放视频时发生错误: {ex.Message}");
                Debug.WriteLine($"MainView: 错误堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 处理视频播放器关闭请求
        /// </summary>
        private void VideoPlayerOverlay_CloseRequested(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("MainView: 视频播放器请求关闭");
                VideoPlayerOverlayContainer.Visibility = Visibility.Collapsed;
                Debug.WriteLine($"MainView: 视频播放器关闭后可见性: {VideoPlayerOverlayContainer.Visibility}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView: 关闭视频播放器时发生错误: {ex.Message}");
            }
        }

        public ObservableCollection<ChatItem> ChatItems { get; set; }
        public ObservableCollection<GroupInfo> GroupItems { get; set; }

        public ObservableCollection<FriendCategory> ContactCategories { get; set; }

        /// <summary>
        ///     註冊系統返回鍵處理事件
        /// </summary>
        private void RegisterBackButtonHandler()
        {
            try
            {
                // 註冊硬體返回鍵事件處理
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

                // 確保應用程序可以處理返回鍵事件
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Visible;

                Debug.WriteLine("系統返回鍵事件處理已註冊");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"註冊系統返回鍵處理時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     頁面導航到此頁面時調用
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 確保返回鍵處理程序已註冊
            RegisterBackButtonHandler();
        }

        /// <summary>
        ///     頁面離開時調用
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // 取消註冊返回鍵處理程序
            try
            {
                SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
                Debug.WriteLine("系統返回鍵事件處理已取消註冊");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"取消註冊系統返回鍵處理時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理系統返回鍵事件
        /// </summary>
        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            try
            {
                Debug.WriteLine($"系統返回鍵被按下 - 當前頁面: {_currentPage}, 側邊欄狀態: {(_sidebarOpen ? "開啟" : "關閉")}");

                // 首先检查视频播放器是否正在显示
                if (VideoPlayerOverlayContainer != null && VideoPlayerOverlayContainer.Visibility == Visibility.Visible)
                {
                    e.Handled = VideoPlayerOverlay.HandleBackButton();
                    if (e.Handled)
                    {
                        VideoPlayerOverlayContainer.Visibility = Visibility.Collapsed;
                    }

                    return;
                }

                // 如果側邊欄開啟，先關閉側邊欄
                if (_sidebarOpen)
                {
                    SidebarColumn.Width = new GridLength(0);
                    _sidebarOpen = false;
                    UpdateOverlay();
                    e.Handled = true; // 阻止默認的返回行為
                    Debug.WriteLine("已關閉側邊欄");
                    return;
                }

                // 如果在聊天界面，返回到聊天列表
                if (_currentPage == "Chat" && ChatInterfaceGrid.Visibility == Visibility.Visible)
                {
                    // 執行與 BackButton_Click 相同的邏輯
                    ChatInterfaceGrid.Visibility = Visibility.Collapsed;
                    ChatListView.SelectedItem = null;
                    SwitchPage("Chats");

                    e.Handled = true; // 阻止默認的返回行為
                    Debug.WriteLine("已從聊天界面返回到聊天列表");
                    return;
                }

                // 如果在其他頁面（聯繫人、群組、設置），返回到聊天列表
                if (_currentPage != "Chats")
                {
                    SwitchPage("Chats");
                    e.Handled = true; // 阻止默認的返回行為
                    Debug.WriteLine($"已從 {_currentPage} 頁面返回到聊天列表");
                    return;
                }

                // 如果已經在聊天列表頁面，不處理返回鍵，讓系統處理（退出應用或返回上一頁面）
                Debug.WriteLine("已在聊天列表頁面，使用系統默認返回行為");
                // e.Handled = false; // 讓系統處理返回事件
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理系統返回鍵時發生錯誤: {ex.Message}");
                // 發生錯誤時不阻止系統默認行為
            }
        }

        /// <summary>
        ///     檢查是否可以處理返回事件
        /// </summary>
        public bool CanGoBack()
        {
            // 如果側邊欄開啟，可以關閉側邊欄
            if (_sidebarOpen) return true;

            // 如果在聊天界面，可以返回聊天列表
            if (_currentPage == "Chat" && ChatInterfaceGrid.Visibility == Visibility.Visible) return true;

            // 如果不在聊天列表頁面，可以返回聊天列表
            if (_currentPage != "Chats") return true;

            // 其他情況不處理
            return false;
        }

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

                            // 清除所有現有消息，重新加載完整的歷史記錄
                            _currentMessages.Clear();

                            // 從數據庫重新加載所有消息（包括剛保存的歷史消息）
                            var allMessages = DataAccess.GetChatMessages(chatId, isGroup);
                            foreach (var message in allMessages.OrderBy(m => m.Timestamp))
                            {
                                _currentMessages.Add(message);
                            }

                            // 如果沒有任何消息，顯示歡迎消息
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

            // 更新系統返回鍵可見性
            UpdateBackButtonVisibility();
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

        /// <summary>
        ///     更新聊天列表中的群组信息
        /// </summary>
        public void UpdateGroupInfoInChatList()
        {
            try
            {
                var allGroups = DataAccess.GetAllGroups();
                var hasUpdates = false;

                foreach (var chatItem in ChatItems.ToList())
                    if (chatItem.IsGroup)
                    {
                        var group = allGroups.FirstOrDefault(g => g.GroupId == chatItem.ChatId);
                        if (group != null)
                        {
                            // 检查群组名是否需要更新
                            var newGroupName = !string.IsNullOrEmpty(group.GroupRemark)
                                ? group.GroupRemark
                                : group.GroupName;

                            if (chatItem.Name != newGroupName)
                            {
                                Debug.WriteLine($"更新群组名: {chatItem.Name} -> {newGroupName}");
                                chatItem.Name = newGroupName;
                                hasUpdates = true;
                            }

                            // 更新成员数量
                            if (chatItem.MemberCount != group.MemberCount)
                            {
                                Debug.WriteLine($"更新群组成员数: {chatItem.MemberCount} -> {group.MemberCount}");
                                chatItem.MemberCount = group.MemberCount;
                                hasUpdates = true;
                            }
                        }
                    }

                if (hasUpdates)
                {
                    Debug.WriteLine("群组信息已更新，保存聊天列表缓存");
                    SaveChatListCache();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新群组信息时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     更新聊天列表中的好友信息
        /// </summary>
        public void UpdateFriendInfoInChatList()
        {
            try
            {
                var allFriendCategories = DataAccess.GetAllFriendsWithCategories();
                var hasUpdates = false;

                foreach (var chatItem in ChatItems.ToList())
                    if (!chatItem.IsGroup)
                    {
                        FriendInfo friend = null;

                        // 查找好友信息
                        foreach (var category in allFriendCategories)
                            if (category.BuddyList != null)
                            {
                                friend = category.BuddyList.FirstOrDefault(f => f.UserId == chatItem.ChatId);
                                if (friend != null) break;
                            }

                        if (friend != null)
                        {
                            // 确定显示名称（备注 > 昵称 > 原始昵称）
                            var newFriendName = !string.IsNullOrEmpty(friend.Remark) ? friend.Remark :
                                !string.IsNullOrEmpty(friend.Nickname) ? friend.Nickname :
                                friend.Nick;

                            if (!string.IsNullOrEmpty(newFriendName) && chatItem.Name != newFriendName)
                            {
                                Debug.WriteLine($"更新好友名: {chatItem.Name} -> {newFriendName}");
                                chatItem.Name = newFriendName;
                                hasUpdates = true;
                            }
                        }
                    }

                if (hasUpdates)
                {
                    Debug.WriteLine("好友信息已更新，保存聊天列表缓存");
                    SaveChatListCache();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新好友信息时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     刷新聊天列表中所有联系人信息
        /// </summary>
        public void RefreshChatListContactInfo()
        {
            UpdateGroupInfoInChatList();
            UpdateFriendInfoInChatList();
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
                if (_currentMessages == null || _currentChat == null) return;

                Debug.WriteLine("RefreshCurrentChatMessages: 開始刷新當前聊天消息");

                // 重新從數據庫加載當前聊天的所有消息
                var allMessages = DataAccess.GetChatMessages(_currentChat.ChatId, _currentChat.IsGroup);

                // 清空當前消息列表
                _currentMessages.Clear();

                // 重新添加所有消息
                foreach (var message in allMessages.OrderBy(m => m.Timestamp))
                {
                    _currentMessages.Add(message);
                }

                // 如果沒有消息，顯示歡迎消息
                if (_currentMessages.Count == 0)
                {
                    var welcomeMessage = new ChatMessage
                    {
                        Content = _currentChat.IsGroup ? "歡迎來到群組聊天！" : "開始聊天吧！",
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
                // 首先快速加載群組數據
                var groups = DataAccess.GetAllGroups();
                GroupItems = new ObservableCollection<GroupInfo>(groups);
                GroupListView.ItemsSource = GroupItems;

                // 異步加載聯繫人數據，提升響應速度
                LoadContactsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加載聯繫人和群組數據時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     異步加載聯繫人數據，先顯示分組標題，再懶加載好友列表
        /// </summary>
        private async void LoadContactsAsync()
        {
            try
            {
                Debug.WriteLine("開始異步加載聯繫人數據");

                // 在後台線程獲取分類數據
                var friendCategories = await Task.Run(() => DataAccess.GetAllFriendsWithCategories());

                // 初始化分類，但設置為折疊狀態且暫時不加載好友列表
                ContactCategories = new ObservableCollection<FriendCategory>();

                foreach (var category in friendCategories)
                {
                    // 創建分類的副本，但暫時清空好友列表以提升響應速度
                    var categoryHeader = new FriendCategory
                    {
                        CategoryId = category.CategoryId,
                        CategorySortId = category.CategorySortId,
                        CategoryName = category.CategoryName,
                        CategoryMbCount = category.CategoryMbCount,
                        OnlineCount = category.OnlineCount,
                        IsExpanded = false, // 初始設置為折疊
                        BuddyList = null // 暫時不加載好友列表
                    };

                    // 保存原始好友列表到一個字典中，供後續懶加載使用
                    if (!_categoryFriendsCache.ContainsKey(category.CategoryId))
                        _categoryFriendsCache[category.CategoryId] = category.BuddyList ?? new List<FriendInfo>();

                    ContactCategories.Add(categoryHeader);
                }

                // 快速構建初始UI（只顯示折疊的分組標題）
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () => { BuildContactsUIFast(); });

                Debug.WriteLine($"聯繫人分組初始化完成，共 {ContactCategories.Count} 個分組");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"異步加載聯繫人數據時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     快速構建聯繫人UI（僅顯示分組標題）
        /// </summary>
        private void BuildContactsUIFast()
        {
            ContactsStackPanel.Children.Clear();

            Debug.WriteLine($"BuildContactsUIFast: 快速構建界面，共有 {ContactCategories?.Count ?? 0} 個分類");

            if (ContactCategories == null) return;

            foreach (var category in ContactCategories)
            {
                Debug.WriteLine($"BuildContactsUIFast: 處理分類 {category.CategoryName}");

                // 創建分類標題
                var categoryHeader = CreateCategoryHeader(category);
                ContactsStackPanel.Children.Add(categoryHeader);

                // 如果分類是展開的，則顯示好友列表（懶加載）
                if (category.IsExpanded)
                {
                    var _ = LoadCategoryFriendsAsync(category); // 避免 async void 警告
                }
            }

            Debug.WriteLine($"BuildContactsUIFast: 快速界面構建完成，總共添加了 {ContactsStackPanel.Children.Count} 個分組標題");
        }

        /// <summary>
        ///     創建分類標題控件
        /// </summary>
        private Grid CreateCategoryHeader(FriendCategory category)
        {
            var categoryHeader = new Grid
            {
                Height = 40,
                Background = new SolidColorBrush(Color.FromArgb(255, 45, 62, 80)),
                Margin = new Thickness(0, 0, 0, 1),
                Tag = category // 保存分類引用
            };

            // 添加點擊事件
            categoryHeader.Tapped += CategoryHeader_Tapped_Optimized;

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

            // 獲取好友數量（從緩存中獲取以避免重複查詢）
            var friendCount = _categoryFriendsCache.ContainsKey(category.CategoryId)
                ? _categoryFriendsCache[category.CategoryId].Count
                : category.CategoryMbCount;

            var headerText = new TextBlock
            {
                Text = $"{category.CategoryName} ({friendCount})",
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerStackPanel.Children.Add(expandIcon);
            headerStackPanel.Children.Add(headerText);
            categoryHeader.Children.Add(headerStackPanel);

            return categoryHeader;
        }

        /// <summary>
        ///     優化的分類標題點擊事件處理
        /// </summary>
        private async void CategoryHeader_Tapped_Optimized(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.Tag is FriendCategory category)
            {
                Debug.WriteLine($"點擊分類 {category.CategoryName}，當前狀態：{(category.IsExpanded ? "展開" : "折疊")}");

                // 切換展開狀態
                category.IsExpanded = !category.IsExpanded;

                // 更新圖標
                var headerStackPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                var expandIcon = headerStackPanel?.Children.OfType<FontIcon>().FirstOrDefault();
                if (expandIcon != null)
                    expandIcon.Glyph = category.IsExpanded ? "\uE70D" : "\uE70E";

                if (category.IsExpanded)
                    // 展開：異步加載並顯示好友列表
                    await LoadCategoryFriendsAsync(category);
                else
                    // 折疊：移除好友列表UI
                    RemoveCategoryFriendsUI(category);

                Debug.WriteLine($"分類 {category.CategoryName} 切換為 {(category.IsExpanded ? "展開" : "收起")}");
            }
        }

        /// <summary>
        ///     異步加載指定分類的好友列表
        /// </summary>
        private async Task LoadCategoryFriendsAsync(FriendCategory category)
        {
            try
            {
                Debug.WriteLine($"開始異步加載分類 {category.CategoryName} 的好友列表");

                // 如果好友列表還沒有加載，從緩存中獲取
                if (category.BuddyList == null || category.BuddyList.Count == 0)
                {
                    if (_categoryFriendsCache.ContainsKey(category.CategoryId))
                    {
                        category.BuddyList = _categoryFriendsCache[category.CategoryId];
                        Debug.WriteLine($"從緩存中加載分類 {category.CategoryName} 的 {category.BuddyList.Count} 個好友");
                    }
                    else
                    {
                        Debug.WriteLine($"分類 {category.CategoryName} 沒有找到緩存的好友列表");
                        return;
                    }
                }

                // 在UI線程中添加好友項目
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () => { AddCategoryFriendsToUI(category); });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"異步加載分類好友列表時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     將分類的好友添加到UI中
        /// </summary>
        private void AddCategoryFriendsToUI(FriendCategory category)
        {
            try
            {
                if (category.BuddyList == null || category.BuddyList.Count == 0)
                {
                    Debug.WriteLine($"分類 {category.CategoryName} 沒有好友需要顯示");
                    return;
                }

                // 找到分類標題在UI中的位置
                var categoryHeaderIndex = -1;
                for (var i = 0; i < ContactsStackPanel.Children.Count; i++)
                    if (ContactsStackPanel.Children[i] is Grid grid &&
                        grid.Tag is FriendCategory cat &&
                        cat.CategoryId == category.CategoryId)
                    {
                        categoryHeaderIndex = i;
                        break;
                    }

                if (categoryHeaderIndex == -1)
                {
                    Debug.WriteLine($"無法找到分類 {category.CategoryName} 的標題位置");
                    return;
                }

                // 在分類標題後面插入好友項目
                var insertIndex = categoryHeaderIndex + 1;
                foreach (var friend in category.BuddyList)
                {
                    var friendItem = CreateFriendItem(friend);
                    ContactsStackPanel.Children.Insert(insertIndex, friendItem);
                    insertIndex++;
                }

                Debug.WriteLine($"成功將 {category.BuddyList.Count} 個好友添加到分類 {category.CategoryName} 下");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"添加分類好友到UI時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     移除分類的好友UI項目
        /// </summary>
        private void RemoveCategoryFriendsUI(FriendCategory category)
        {
            try
            {
                if (category.BuddyList == null || category.BuddyList.Count == 0)
                    return;

                // 找到分類標題在UI中的位置
                var categoryHeaderIndex = -1;
                for (var i = 0; i < ContactsStackPanel.Children.Count; i++)
                    if (ContactsStackPanel.Children[i] is Grid grid &&
                        grid.Tag is FriendCategory cat &&
                        cat.CategoryId == category.CategoryId)
                    {
                        categoryHeaderIndex = i;
                        break;
                    }

                if (categoryHeaderIndex == -1)
                {
                    Debug.WriteLine($"無法找到分類 {category.CategoryName} 的標題位置");
                    return;
                }

                // 移除該分類下的所有好友項目
                var friendsToRemove = new List<UIElement>();
                for (var i = categoryHeaderIndex + 1; i < ContactsStackPanel.Children.Count; i++)
                {
                    var element = ContactsStackPanel.Children[i];

                    // 如果遇到下一個分類標題，停止移除
                    if (element is Grid grid && grid.Tag is FriendCategory)
                        break;

                    friendsToRemove.Add(element);
                }

                // 從UI中移除好友項目
                foreach (var friendItem in friendsToRemove)
                    ContactsStackPanel.Children.Remove(friendItem);

                Debug.WriteLine($"成功移除分類 {category.CategoryName} 下的 {friendsToRemove.Count} 個好友項目");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"移除分類好友UI時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     優化的搜索過濾方法
        /// </summary>
        private async void FilterContactsList(string searchText)
        {
            try
            {
                // 清空當前UI
                ContactsStackPanel.Children.Clear();

                if (string.IsNullOrEmpty(searchText))
                {
                    // 如果搜索為空，重新構建快速UI
                    BuildContactsUIFast();
                    return;
                }

                Debug.WriteLine($"開始搜索聯繫人: {searchText}");

                // 在後台線程進行搜索
                await Task.Run(async () =>
                {
                    var filteredCategories = new List<FilteredCategoryResult>();

                    foreach (var category in ContactCategories)
                    {
                        List<FriendInfo> friendsToSearch;

                        // 從緩存中獲取好友列表
                        if (_categoryFriendsCache.ContainsKey(category.CategoryId))
                            friendsToSearch = _categoryFriendsCache[category.CategoryId];
                        else
                            friendsToSearch = new List<FriendInfo>();

                        var filteredFriends = friendsToSearch.Where(friend =>
                                (!string.IsNullOrEmpty(friend.Remark) &&
                                 friend.Remark.ToLower().Contains(searchText)) ||
                                (!string.IsNullOrEmpty(friend.Nickname) &&
                                 friend.Nickname.ToLower().Contains(searchText)) ||
                                (!string.IsNullOrEmpty(friend.Nick) && friend.Nick.ToLower().Contains(searchText)) ||
                                friend.UserId.ToString().Contains(searchText) // 添加按 QQ號 搜索
                        ).ToList();

                        if (filteredFriends.Any())
                            filteredCategories.Add(new FilteredCategoryResult
                            {
                                Category = category,
                                FilteredFriends = filteredFriends
                            });
                    }

                    // 在UI線程中顯示搜索結果
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            foreach (var result in filteredCategories)
                            {
                                var category = result.Category;
                                var filteredFriends = result.FilteredFriends;

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
                        });
                });

                Debug.WriteLine($"搜索完成，找到匹配的分類數: {ContactsStackPanel.Children.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索聯繫人時發生錯誤: {ex.Message}");
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

            // 更新系統返回鍵可見性
            UpdateBackButtonVisibility();
        }

        /// <summary>
        ///     更新系統返回鍵的可見性
        /// </summary>
        private void UpdateBackButtonVisibility()
        {
            try
            {
                // 根據當前狀態決定是否顯示返回鍵
                var shouldShowBackButton = CanGoBack();

                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    shouldShowBackButton ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;

                Debug.WriteLine($"返回鍵可見性已更新: {(shouldShowBackButton ? "顯示" : "隱藏")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新返回鍵可見性時發生錯誤: {ex.Message}");
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

        /// <summary>
        ///     定義過濾結果類別（替代元組）
        /// </summary>
        private class FilteredCategoryResult
        {
            public FriendCategory Category { get; set; }
            public List<FriendInfo> FilteredFriends { get; set; }
        }

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

            // 更新返回鍵可見性
            UpdateBackButtonVisibility();
        }

        private void OverlayRect_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_sidebarOpen)
            {
                SidebarColumn.Width = new GridLength(0);
                _sidebarOpen = false;
                UpdateOverlay();

                // 更新返回鍵可見性
                UpdateBackButtonVisibility();
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