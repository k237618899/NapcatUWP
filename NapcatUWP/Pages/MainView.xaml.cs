﻿using System;
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
using Windows.UI.Popups;
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
        private const double SCROLL_TOLERANCE = 50.0; // 滾動容忍度（像素）

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

        // 在類的頂部添加字段
        private DatabaseStatistics _databaseStatistics;

        /// <summary>
        ///     滾動狀態追蹤字段
        /// </summary>
        private bool _isUserScrolledAwayFromBottom; // 使用者是否手動滾動離開底部

        private bool _sidebarOpen;

        public MainView()
        {
            InitializeComponent();
            InitializeAvatorAndInfo();
            SidebarColumn.Width = new GridLength(0);
            UpdateOverlay();

            // 初始化數據庫統計信息
            _databaseStatistics = new DatabaseStatistics();

            // 檢查數據庫健康狀態
            CheckDatabaseHealth();

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

            // 改善：分階段執行後台修復任務，避免數據庫鎖定
            StartBackgroundMaintenanceAsync();

            RegisterScrollEventHandlers();

            // 添加群組列表點擊事件
            GroupListView.ItemClick += GroupListView_ItemClick;
            GroupListView.IsItemClickEnabled = true;
        }

        public ObservableCollection<ChatItem> ChatItems { get; set; }
        public ObservableCollection<GroupInfo> GroupItems { get; set; }

        public ObservableCollection<FriendCategory> ContactCategories { get; set; }

        /// <summary>
        ///     分階段執行後台維護任務 - 避免數據庫鎖定
        /// </summary>
        private async void StartBackgroundMaintenanceAsync()
        {
            try
            {
                // 延遲3秒後開始，讓界面先完全加載
                await Task.Delay(3000);

                Debug.WriteLine("開始分階段後台維護任務");

                // 階段1：修復時區偏移問題
                await Task.Run(async () =>
                {
                    try
                    {
                        DataAccess.ForceFixTimestampOffset();
                        await Task.Delay(1000); // 給數據庫一些休息時間
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"修復時區偏移時發生錯誤: {ex.Message}");
                    }
                });

                // 階段2：修復時間戳問題
                await Task.Run(async () =>
                {
                    try
                    {
                        DataAccess.FixAllTimestampIssues();
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"修復時間戳時發生錯誤: {ex.Message}");
                    }
                });

                // 階段3：更新用戶信息
                await Task.Run(async () =>
                {
                    try
                    {
                        DataAccess.UpdateUserInfoInMessages();
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"更新用戶信息時發生錯誤: {ex.Message}");
                    }
                });

                // 階段4：數據庫優化（可選）
                await Task.Run(async () =>
                {
                    try
                    {
                        await DatabaseManager.OptimizeDatabaseAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"數據庫優化時發生錯誤: {ex.Message}");
                    }
                });

                Debug.WriteLine("後台維護任務完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"後台維護任務發生錯誤: {ex.Message}");
            }
        }

        // 新增數據庫健康檢查方法
        private async void CheckDatabaseHealth()
        {
            try
            {
                var isHealthy = await DatabaseManager.CheckDatabaseHealthAsync();
                if (!isHealthy)
                    Debug.WriteLine("數據庫健康檢查失敗，可能需要重新初始化");
                // 可以在這裡添加數據庫修復邏輯
                else
                    Debug.WriteLine("數據庫健康檢查通過");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"數據庫健康檢查時發生錯誤: {ex.Message}");
            }
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
        ///     处理消息段控件的视频播放请求
        /// </summary>
        private void MessageSegmentControl_VideoPlayRequested(object sender, VideoPlayEventArgs e)
        {
            Debug.WriteLine(
                $"MainView: 收到视频播放请求 - Sender: {sender?.GetType().Name}, URL: {e.VideoUrl}, Title: {e.Title}");
            PlayVideo(e.VideoUrl, e.Title);
        }

        private void MessageSegmentControl_ImageViewRequested(object sender, ImageViewEventArgs e)
        {
            try
            {
                Debug.WriteLine($"MainView: 請求查看圖片 - URL: {e.ImageUrl}");

                // 顯示圖片查看器
                ImageViewerOverlay.ShowImage(e.ImageUrl);
                ImageViewerOverlayContainer.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView: 顯示圖片查看器時發生錯誤: {ex.Message}");
            }
        }

        private void ImageViewerOverlay_CloseRequested(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("MainView: 關閉圖片查看器");
                ImageViewerOverlayContainer.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView: 關閉圖片查看器時發生錯誤: {ex.Message}");
            }
        }

        private void MessageSegmentControl_AudioPlayRequested(object sender, AudioPlayRequestEventArgs e)
        {
            try
            {
                Debug.WriteLine($"MainView: 請求播放音頻 - URL: {e.AudioUrl}");
                // 音頻播放由 AudioPlayerManager 直接處理，這裡可以添加其他邏輯（如日誌記錄等）
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView: 處理音頻播放請求時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     处理视频播放请求
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
                    Debug.WriteLine("MainView: 设置视频播放器容器可见性为 Visible");

                    VideoPlayerOverlay.PlayVideo(videoUrl, title);
                    Debug.WriteLine("MainView: 调用视频播放器播放方法完成");

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
        ///     处理视频播放器关闭请求
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
        ///     註冊滾動事件處理程序 - 用於追蹤使用者滾動行為
        /// </summary>
        private void RegisterScrollEventHandlers()
        {
            try
            {
                // 當 MessageScrollViewer 載入完成後註冊滾動事件
                MessageScrollViewer.Loaded += (sender, e) =>
                {
                    MessageScrollViewer.ViewChanged += MessageScrollViewer_ViewChanged;
                    Debug.WriteLine("已註冊 MessageScrollViewer 滾動事件處理程序");
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"註冊滾動事件處理程序時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     滾動視圖變化事件處理 - 追蹤使用者是否手動滾動離開底部
        /// </summary>
        private void MessageScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            try
            {
                if (MessageScrollViewer == null) return;

                var scrollViewer = MessageScrollViewer;
                var scrollableHeight = scrollViewer.ScrollableHeight;
                var verticalOffset = scrollViewer.VerticalOffset;

                // 檢查是否接近底部（在容忍範圍內）
                var isNearBottom = scrollableHeight == 0 ||
                                   verticalOffset >= scrollableHeight - SCROLL_TOLERANCE;

                // 只有在使用者手動操作且不在底部時才設置標記
                if (!e.IsIntermediate) // 只在滾動結束時檢查
                {
                    _isUserScrolledAwayFromBottom = !isNearBottom;

                    Debug.WriteLine($"滾動狀態更新: VerticalOffset={verticalOffset:F1}, " +
                                    $"ScrollableHeight={scrollableHeight:F1}, " +
                                    $"IsNearBottom={isNearBottom}, " +
                                    $"UserScrolledAway={_isUserScrolledAwayFromBottom}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理滾動視圖變化事件時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     檢查是否應該自動滾動到底部
        /// </summary>
        private bool ShouldAutoScrollToBottom()
        {
            // 如果使用者沒有手動滾動離開底部，則允許自動滾動
            return !_isUserScrolledAwayFromBottom;
        }

        /// <summary>
        ///     智能滾動到底部 - 根據使用者行為決定是否滾動
        /// </summary>
        /// <param name="forceScroll">是否強制滾動（如發送消息、打開聊天等情況）</param>
        private async void SmartScrollToBottom(bool forceScroll = false)
        {
            try
            {
                if (forceScroll || ShouldAutoScrollToBottom())
                {
                    await Task.Delay(100); // 稍微延遲確保UI更新完成

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            MessageScrollViewer.UpdateLayout();
                            var scrollableHeight = MessageScrollViewer.ScrollableHeight;

                            if (scrollableHeight > 0)
                            {
                                MessageScrollViewer.ChangeView(null, scrollableHeight, null, false);

                                // 重置用戶滾動狀態（表示現在在底部）
                                _isUserScrolledAwayFromBottom = false;

                                Debug.WriteLine($"智能滾動到底部: ScrollableHeight={scrollableHeight}, Force={forceScroll}");
                            }
                        });
                }
                else
                {
                    Debug.WriteLine("使用者已滾動離開底部，跳過自動滾動");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"智能滾動失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     強制滾動到底部 - 用於打開聊天、載入歷史等場景
        /// </summary>
        private async void ForceScrollToBottom()
        {
            try
            {
                // 等待更長時間確保所有UI元素都已渲染
                await Task.Delay(200);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        // 強制更新佈局多次以確保正確計算高度
                        MessageScrollViewer.UpdateLayout();
                        MessagesItemsControl.UpdateLayout();

                        // 再次延遲以確保所有元素完全渲染
                        Task.Delay(50).ContinueWith(_ =>
                        {
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                CoreDispatcherPriority.Normal, () =>
                                {
                                    var scrollableHeight = MessageScrollViewer.ScrollableHeight;

                                    if (scrollableHeight > 0)
                                    {
                                        MessageScrollViewer.ChangeView(null, scrollableHeight, null, false);

                                        // 重置用戶滾動狀態
                                        _isUserScrolledAwayFromBottom = false;

                                        Debug.WriteLine($"強制滾動到底部: ScrollableHeight={scrollableHeight}");
                                    }
                                    else
                                    {
                                        Debug.WriteLine("ScrollableHeight為0，無需滾動");
                                    }
                                });
                        });
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"強制滾動失敗: {ex.Message}");
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
                    if (e.Handled) VideoPlayerOverlayContainer.Visibility = Visibility.Collapsed;

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
        ///     修改原有的同步保存方法，使用異步版本
        /// </summary>
        private void SaveChatListCache()
        {
            // 不等待異步操作完成，避免阻塞
            var _ = SaveChatListCacheAsync();
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

                // 立即保存聊天列表緩存，確保下次登入時能保留
                SaveChatListCache();

                Debug.WriteLine($"UI更新完成並保存緩存，聊天列表總數: {ChatItems.Count}");
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

            // 重置用戶滾動狀態（進入新聊天時總是滾動到底部）
            _isUserScrolledAwayFromBottom = false;

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

            // 強制滾動到底部（進入聊天時）
            ForceScrollToBottom();

            Debug.WriteLine($"打開聊天: {chatItem.Name} (ID: {chatItem.ChatId}, 是否群組: {chatItem.IsGroup})");
        }

        /// <summary>
        ///     修改 LoadHistoryFromServer 方法 - 增強錯誤處理
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

                // 設置一個超時處理，如果5秒後還沒有收到響應，就清除加載提示
                var timeoutTask = Task.Delay(5000).ContinueWith(_ =>
                {
                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            // 檢查是否還有加載提示存在
                            var loadingMessages = _currentMessages.Where(m =>
                                m.MessageType == "system" && m.Content.Contains("正在載入")).ToList();

                            if (loadingMessages.Any())
                            {
                                Debug.WriteLine("歷史消息請求超時，清除加載提示");
                                HandleHistoryLoadingError(chatItem.ChatId, chatItem.IsGroup);
                            }
                        });
                });

                Debug.WriteLine($"已請求聊天歷史消息: {chatItem.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"從服務器載入歷史消息錯誤: {ex.Message}");
                // 如果載入失敗，清除加載提示並顯示錯誤消息
                HandleHistoryLoadingError(chatItem.ChatId, chatItem.IsGroup);
            }
        }

        /// <summary>
        ///     處理歷史消息加載錯誤 - 清除加載提示並顯示空聊天界面
        /// </summary>
        /// <param name="chatId">聊天ID</param>
        /// <param name="isGroup">是否為群組</param>
        public void HandleHistoryLoadingError(long chatId, bool isGroup)
        {
            try
            {
                Debug.WriteLine($"處理歷史消息加載錯誤: ChatId={chatId}, IsGroup={isGroup}");

                // 檢查是否是當前聊天
                if (_currentChat != null && _currentChat.ChatId == chatId && _currentChat.IsGroup == isGroup)
                {
                    // 清除所有系統消息（包括"正在載入聊天記錄..."）
                    var systemMessages = _currentMessages.Where(m => m.MessageType == "system").ToList();
                    foreach (var msg in systemMessages) _currentMessages.Remove(msg);

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

                    // 強制滾動到底部
                    ForceScrollToBottom();

                    Debug.WriteLine($"已清除加載提示並顯示空聊天界面: {_currentChat.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理歷史消息加載錯誤時發生異常: {ex.Message}");
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
        ///     修改 HandleHistoryMessages 方法 - 確保清除加載提示
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
                            // 清除系統提示消息（包括加載提示）
                            var systemMessages = _currentMessages.Where(m => m.MessageType == "system").ToList();
                            foreach (var msg in systemMessages) _currentMessages.Remove(msg);

                            // 清除所有現有消息，重新加載完整的歷史記錄
                            _currentMessages.Clear();

                            // 從數據庫重新加載所有消息（包括剛保存的歷史消息）
                            var allMessages = DataAccess.GetChatMessages(chatId, isGroup);
                            foreach (var message in allMessages.OrderBy(m => m.Timestamp))
                                _currentMessages.Add(message);

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

                            // 載入歷史消息時強制滾動到底部
                            ForceScrollToBottom();

                            Debug.WriteLine($"已更新聊天界面，總消息數: {_currentMessages.Count}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理歷史消息錯誤: {ex.Message}");
                // 發生錯誤時也要清除加載提示
                HandleHistoryLoadingError(chatId, isGroup);
            }
        }

        // 修改 SwitchToChatInterface 方法
        private void SwitchToChatInterface()
        {
            try
            {
                Debug.WriteLine("切換到聊天界面");

                // 隱藏所有列表頁面
                ChatListView.Visibility = Visibility.Collapsed;
                ContactsScrollViewer.Visibility = Visibility.Collapsed;
                GroupListView.Visibility = Visibility.Collapsed;
                SettingsScrollViewer.Visibility = Visibility.Collapsed;

                // 顯示聊天界面
                ChatInterfaceGrid.Visibility = Visibility.Visible;

                // 更新當前頁面狀態
                _currentPage = "Chat";

                // 更新系統返回鍵可見性
                UpdateBackButtonVisibility();

                Debug.WriteLine(
                    $"聊天界面切換完成 - ChatListView.Visibility: {ChatListView.Visibility}, ChatInterfaceGrid.Visibility: {ChatInterfaceGrid.Visibility}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"切換到聊天界面時發生錯誤: {ex.Message}");
            }
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
        ///     公共方法：切換頁面 - 供外部調用
        /// </summary>
        /// <param name="pageName">頁面名稱</param>
        public void SwitchPagePublic(string pageName)
        {
            SwitchPage(pageName);
        }

        /// <summary>
        ///     修改 OpenChatDirectly 方法 - 確保正確的界面切換
        /// </summary>
        public void OpenChatDirectly(ChatItem chatItem)
        {
            try
            {
                Debug.WriteLine($"直接打開聊天界面: {chatItem.Name} (ID: {chatItem.ChatId})");

                // 先切換到聊天頁面（確保聊天列表可見）
                SwitchPage("Chats");

                // 短暫延遲確保頁面切換完成
                Task.Delay(50).ContinueWith(_ =>
                {
                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            // 設置選中的聊天項目
                            ChatListView.SelectedItem = chatItem;

                            // 直接調用OpenChat方法
                            OpenChat(chatItem);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"延遲打開聊天時發生錯誤: {ex.Message}");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"直接打開聊天界面時發生錯誤: {ex.Message}");
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

        /// <summary>
        ///     安全載入聊天記錄 - 避免 SQLite 鎖定
        /// </summary>
        private async void LoadChatHistory(ChatItem chatItem)
        {
            try
            {
                Debug.WriteLine($"開始安全加載聊天記錄: {chatItem.Name}");

                // 使用安全的異步方法加載聊天記錄，帶重試機制
                var messages = await LoadChatMessagesWithRetryAsync(chatItem.ChatId, chatItem.IsGroup);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            _currentMessages.Clear();

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

                            // 載入歷史記錄時強制滾動到底部
                            ForceScrollToBottom();

                            Debug.WriteLine($"聊天記錄加載完成: {messages.Count} 條消息");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"更新UI時發生錯誤: {ex.Message}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加載聊天記錄錯誤: {ex.Message}");

                // 如果加載失敗，顯示錯誤提示
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        var errorMessage = new ChatMessage
                        {
                            Content = "聊天記錄加載失敗，請稍後重試",
                            Timestamp = DateTime.Now,
                            IsFromMe = false,
                            SenderName = "系統",
                            SenderId = -1,
                            MessageType = "system"
                        };
                        _currentMessages.Add(errorMessage);
                    });
            }
        }

        /// <summary>
        ///     帶重試機制的聊天消息加載 - 修正為 C# 7.0 兼容版本
        /// </summary>
        private async Task<List<ChatMessage>> LoadChatMessagesWithRetryAsync(long chatId, bool isGroup,
            int retryCount = 0)
        {
            const int maxRetries = 3;

            try
            {
                // 使用 Task.Run 將同步 DataAccess 方法包裝為異步，兼容 C# 7.0
                return await Task.Run(() => DataAccess.GetChatMessages(chatId, isGroup));
            }
            catch (Exception ex) when (retryCount < maxRetries)
            {
                Debug.WriteLine($"加載聊天消息失敗，重試 {retryCount + 1}/{maxRetries}: {ex.Message}");

                // 指數退避重試
                var delay = (int)Math.Pow(2, retryCount) * 100;
                await Task.Delay(delay);

                return await LoadChatMessagesWithRetryAsync(chatId, isGroup, retryCount + 1);
            }
        }

        /// <summary>
        ///     安全保存聊天列表緩存 - 避免並發衝突
        /// </summary>
        private async Task SaveChatListCacheAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentAccount) && ChatItems != null && ChatItems.Count > 0)
                    // 使用後台線程保存，避免阻塞UI
                    await Task.Run(async () =>
                    {
                        try
                        {
                            // 創建聊天列表的副本以避免集合修改異常
                            var chatItemsCopy = new List<ChatItem>();

                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                CoreDispatcherPriority.Normal, () =>
                                {
                                    foreach (var item in ChatItems)
                                        chatItemsCopy.Add(new ChatItem
                                        {
                                            ChatId = item.ChatId,
                                            Name = item.Name,
                                            LastMessage = item.LastMessage,
                                            LastTime = item.LastTime,
                                            UnreadCount = item.UnreadCount,
                                            AvatarColor = item.AvatarColor,
                                            IsGroup = item.IsGroup,
                                            MemberCount = item.MemberCount
                                        });
                                });

                            DataAccess.SaveChatListCache(_currentAccount, chatItemsCopy);
                            Debug.WriteLine($"安全保存聊天列表緩存: {chatItemsCopy.Count} 個項目");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"後台保存聊天列表緩存錯誤: {ex.Message}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存聊天列表緩存錯誤: {ex.Message}");
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

                    // 發送自己的消息時總是滾動到底部
                    SmartScrollToBottom(true);

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
        ///     安全刷新當前聊天消息 - 避免數據庫鎖定
        /// </summary>
        public async void RefreshCurrentChatMessages()
        {
            try
            {
                if (_currentMessages == null || _currentChat == null) return;

                Debug.WriteLine("RefreshCurrentChatMessages: 開始安全刷新當前聊天消息");

                // 使用重試機制重新加載消息
                var allMessages = await LoadChatMessagesWithRetryAsync(_currentChat.ChatId, _currentChat.IsGroup);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            // 清空當前消息列表
                            _currentMessages.Clear();

                            // 重新添加所有消息
                            foreach (var message in allMessages.OrderBy(m => m.Timestamp))
                                _currentMessages.Add(message);

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

                            // 刷新消息時保持當前滾動位置
                            SmartScrollToBottom();

                            Debug.WriteLine($"RefreshCurrentChatMessages: 消息刷新完成，總數: {_currentMessages.Count}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"RefreshCurrentChatMessages: UI更新時發生錯誤: {ex.Message}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshCurrentChatMessages: 刷新消息時發生錯誤: {ex.Message}");
            }
        }

        // 保留舊的 ScrollToBottom 方法但標記為過時
        [Obsolete("請使用 SmartScrollToBottom 或 ForceScrollToBottom 方法")]
        private async void ScrollToBottom()
        {
            // 重定向到智能滾動
            SmartScrollToBottom(true);
        }

        // 替換現有的 EnsureScrollToBottom 方法
        /// <summary>
        ///     確保滾動到底部的輔助方法 - 智能滾動版本
        /// </summary>
        /// <param name="forceScroll">是否強制滾動，預設為 false</param>
        private async void EnsureScrollToBottom(bool forceScroll = false)
        {
            try
            {
                // 使用智能滾動邏輯
                SmartScrollToBottom(forceScroll);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnsureScrollToBottom 發生錯誤: {ex.Message}");
            }
        }


        // 修改 AddIncomingMessage 方法，移除數據庫保存操作（已在 OneBotAPIHandler 中處理）
        public void AddIncomingMessage(string senderName, long chatId, long actualSenderId, string messageText,
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
                        IsFromMe = false,
                        SenderName = senderName,
                        SenderId = actualSenderId,
                        MessageType = isGroup ? "group" : "private",
                        Segments = segments ?? new List<MessageSegment>() // 設置消息段
                    };

                    _currentMessages.Add(message);

                    // 收到新消息時使用智能滾動（只有在底部時才自動滾動）
                    SmartScrollToBottom();

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

        /// <summary>
        ///     優化的載入聯繫人方法 - 減少數據庫壓力
        /// </summary>
        private async void LoadContactsAndGroups()
        {
            try
            {
                Debug.WriteLine("開始安全加載聯繫人和群組數據");

                // 分別異步加載群組和聯繫人，避免同時訪問數據庫
                await LoadGroupsAsync();
                await Task.Delay(100); // 短暫延迟避免並發
                await LoadContactsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加載聯繫人和群組數據時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     異步加載群組數據 - 修正為 C# 7.0 兼容版本
        /// </summary>
        private async Task LoadGroupsAsync()
        {
            try
            {
                // 使用 Task.Run 將同步 DataAccess 方法包裝為異步
                var groups = await Task.Run(() => DataAccess.GetAllGroups());

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        GroupItems = new ObservableCollection<GroupInfo>(groups);
                        GroupListView.ItemsSource = GroupItems;
                        Debug.WriteLine($"群組數據加載完成: {groups.Count} 個群組");
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加載群組數據時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     異步加載聯繫人數據，先顯示分組標題，再懶加載好友列表
        /// </summary>
        private async Task LoadContactsAsync()
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
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                Tag = friend // 保存好友信息
            };

            // 添加點擊事件
            grid.Tapped += OnFriendItemTapped;

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

        /// <summary>
        ///     好友項目點擊事件處理 - 創建或打開聊天（修正版）
        /// </summary>
        private async void OnFriendItemTapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                if (sender is Grid grid && grid.Tag is FriendInfo friend)
                {
                    Debug.WriteLine($"點擊好友: {friend.Nickname} (ID: {friend.UserId})");

                    // 使用 ChatCreationHelper 創建聊天
                    var success =
                        await ChatCreationHelper.CreateChatFromFriendAsync(friend, this, MainPage.SocketClientStarter);

                    if (success)
                    {
                        Debug.WriteLine($"成功創建或打開好友聊天: {friend.Nickname}");

                        // 如果側邊欄開啟，關閉它
                        if (_sidebarOpen)
                        {
                            SidebarColumn.Width = new GridLength(0);
                            _sidebarOpen = false;
                            UpdateOverlay();
                        }

                        // 不要在這裡切換頁面，讓 ChatCreationHelper 處理導航
                        Debug.WriteLine("好友聊天創建完成，等待導航");
                    }
                    else
                    {
                        Debug.WriteLine($"創建好友聊天失敗: {friend.Nickname}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理好友點擊事件時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     群組項目點擊事件處理 - 創建或打開聊天（修正版）
        /// </summary>
        private async void OnGroupItemTapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                if (sender is Grid grid && grid.Tag is GroupInfo group)
                {
                    Debug.WriteLine($"點擊群組: {group.GroupName} (ID: {group.GroupId})");

                    // 使用 ChatCreationHelper 創建聊天
                    var success =
                        await ChatCreationHelper.CreateChatFromGroupAsync(group, this, MainPage.SocketClientStarter);

                    if (success)
                    {
                        Debug.WriteLine($"成功創建或打開群組聊天: {group.GroupName}");

                        // 如果側邊欄開啟，關閉它
                        if (_sidebarOpen)
                        {
                            SidebarColumn.Width = new GridLength(0);
                            _sidebarOpen = false;
                            UpdateOverlay();
                        }

                        // 不要在這裡切換頁面，讓 ChatCreationHelper 處理導航
                        Debug.WriteLine("群組聊天創建完成，等待導航");
                    }
                    else
                    {
                        Debug.WriteLine($"創建群組聊天失敗: {group.GroupName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理群組點擊事件時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     群組列表項目點擊事件處理（修正版）
        /// </summary>
        private async void GroupListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (e.ClickedItem is GroupInfo group)
                {
                    Debug.WriteLine($"點擊群組列表項目: {group.GroupName} (ID: {group.GroupId})");

                    // 使用 ChatCreationHelper 創建聊天
                    var success =
                        await ChatCreationHelper.CreateChatFromGroupAsync(group, this, MainPage.SocketClientStarter);

                    if (success)
                    {
                        Debug.WriteLine($"成功創建或打開群組聊天: {group.GroupName}");

                        // 如果側邊欄開啟，關閉它
                        if (_sidebarOpen)
                        {
                            SidebarColumn.Width = new GridLength(0);
                            _sidebarOpen = false;
                            UpdateOverlay();
                        }

                        // 不要在這裡切換頁面，讓 ChatCreationHelper 處理導航
                        Debug.WriteLine("群組聊天創建完成，等待導航");
                    }
                    else
                    {
                        Debug.WriteLine($"創建群組聊天失敗: {group.GroupName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理群組列表點擊事件時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     刷新聊天列表 - 公共方法供外部調用
        /// </summary>
        public void RefreshChatList()
        {
            try
            {
                Debug.WriteLine("刷新聊天列表");

                // 重新加載緩存的聊天列表
                LoadCachedChatList();

                // 觸發UI更新
                ChatListView.ItemsSource = ChatItems;

                Debug.WriteLine($"聊天列表刷新完成，共 {ChatItems.Count} 個項目");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新聊天列表時發生錯誤: {ex.Message}");
            }
        }

        private void SwitchPage(string pageName)
        {
            _currentPage = pageName;

            // 隱藏所有頁面
            ChatListView.Visibility = Visibility.Collapsed;
            ContactsScrollViewer.Visibility = Visibility.Collapsed;
            GroupListView.Visibility = Visibility.Collapsed;
            SettingsScrollViewer.Visibility = Visibility.Collapsed; // 修改這行

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
                    SettingsScrollViewer.Visibility = Visibility.Visible; // 修改這行
                    SearchTextBox.PlaceholderText = "Search (Settings)";
                    // 加載設定頁面時刷新統計信息
                    LoadSettingsPage();
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

        // 添加設定頁面相關的新方法

        #region 設定頁面方法

        /// <summary>
        ///     加載設定頁面
        /// </summary>
        private void LoadSettingsPage()
        {
            try
            {
                Debug.WriteLine("加載設定頁面");
                RefreshDatabaseStatistics();
                RefreshRecentMessages();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加載設定頁面時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     刷新數據庫統計信息
        /// </summary>
        private async void RefreshDatabaseStatistics()
        {
            try
            {
                Debug.WriteLine("開始刷新數據庫統計信息");

                // 在後台線程獲取統計信息
                await Task.Run(() => { _databaseStatistics = DataAccess.GetDatabaseStatistics(); });

                // 更新UI
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        TotalMessagesText.Text = _databaseStatistics.TotalMessages.ToString();
                        TotalGroupsText.Text = _databaseStatistics.TotalGroups.ToString();
                        TotalFriendsText.Text = _databaseStatistics.TotalFriends.ToString();
                        TotalCategoriesText.Text = _databaseStatistics.TotalCategories.ToString();
                        TotalGroupMembersText.Text = _databaseStatistics.TotalGroupMembers.ToString();
                        TotalChatListItemsText.Text = _databaseStatistics.TotalChatListItems.ToString();
                        TotalSettingsText.Text = _databaseStatistics.TotalSettings.ToString();
                        DatabaseSizeText.Text = _databaseStatistics.DatabaseSizeFormatted;

                        Debug.WriteLine("數據庫統計信息UI更新完成");
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新數據庫統計信息時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     刷新最近消息
        /// </summary>
        private async void RefreshRecentMessages()
        {
            try
            {
                Debug.WriteLine("開始刷新最近消息");

                var recentMessages = await Task.Run(() => DataAccess.GetRecentChatMessages());

                // 更新UI
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        RecentMessagesItemsControl.ItemsSource = recentMessages;
                        Debug.WriteLine($"最近消息UI更新完成，共 {recentMessages.Count} 條消息");
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新最近消息時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     刷新統計信息按鈕點擊事件
        /// </summary>
        private void RefreshStatsButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("用戶點擊刷新統計信息按鈕");
            RefreshDatabaseStatistics();
        }

        /// <summary>
        ///     刷新最近消息按鈕點擊事件
        /// </summary>
        private void RefreshRecentMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("用戶點擊刷新最近消息按鈕");
            RefreshRecentMessages();
        }

        /// <summary>
        ///     刪除所有聊天資料按鈕點擊事件
        /// </summary>
        private async void DeleteAllChatDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("用戶點擊刪除所有聊天資料按鈕");

                // 顯示確認對話框
                var dialog = new MessageDialog(
                    "此操作將永久刪除所有聊天消息和聊天列表，且無法恢復。\n\n您確定要繼續嗎？",
                    "確認刪除");

                dialog.Commands.Add(new UICommand("確定", null, "confirm"));
                dialog.Commands.Add(new UICommand("取消", null, "cancel"));
                dialog.DefaultCommandIndex = 1; // 默認選中取消

                var result = await dialog.ShowAsync();

                if (result.Id.ToString() == "confirm")
                {
                    Debug.WriteLine("用戶確認刪除操作");

                    // 顯示加載狀態
                    DeleteAllChatDataButton.IsEnabled = false;
                    DeleteAllChatDataButton.Content = "正在刪除...";

                    try
                    {
                        // 在後台線程執行刪除操作
                        await Task.Run(() => { DataAccess.DeleteAllChatData(); });

                        // 清空當前聊天列表
                        ChatItems.Clear();

                        // 刷新統計信息
                        RefreshDatabaseStatistics();
                        RefreshRecentMessages();

                        // 顯示成功消息
                        var successDialog = new MessageDialog(
                            "所有聊天資料已成功刪除！",
                            "刪除完成");
                        await successDialog.ShowAsync();

                        Debug.WriteLine("聊天資料刪除操作完成");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"刪除聊天資料時發生錯誤: {ex.Message}");

                        // 顯示錯誤消息
                        var errorDialog = new MessageDialog(
                            $"刪除聊天資料時發生錯誤：{ex.Message}",
                            "刪除失敗");
                        await errorDialog.ShowAsync();
                    }
                    finally
                    {
                        // 恢復按鈕狀態
                        DeleteAllChatDataButton.IsEnabled = true;
                        DeleteAllChatDataButton.Content = "刪除所有聊天資料";
                    }
                }
                else
                {
                    Debug.WriteLine("用戶取消刪除操作");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理刪除聊天資料請求時發生錯誤: {ex.Message}");
            }
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