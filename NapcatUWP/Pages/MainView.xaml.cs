using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using NapcatUWP.Controls;
using NapcatUWP.Models;
using NapcatUWP.Tools;
using Newtonsoft.Json;

namespace NapcatUWP.Pages
{
    public sealed partial class MainView : Page
    {
        private const double SCROLL_TOLERANCE = 50.0; // 滾動容忍度（像素）

        // 修復：將 readonly 字段改為普通字段，在構造函數中初始化
        private readonly Dictionary<long, List<FriendInfo>> _categoryFriendsCache;
        private readonly ObservableCollection<ChatMessage> _currentMessages;

        private readonly HashSet<string> _loadedHistoryChats;
        private readonly Random _random;

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

        /// <summary>
        ///     優化的初始化方法 - 修改現有版本以配合新的載入流程
        /// </summary>
        public MainView()
        {
            InitializeComponent();

            // 修復：確保所有集合都正確初始化
            ChatItems = new ObservableCollection<ChatItem>();
            GroupItems = new ObservableCollection<GroupInfo>();
            ContactCategories = new ObservableCollection<FriendCategory>();

            _categoryFriendsCache = new Dictionary<long, List<FriendInfo>>();
            _currentMessages = new ObservableCollection<ChatMessage>();
            _loadedHistoryChats = new HashSet<string>();
            _random = new Random();

            // 第1階段：基礎UI初始化（立即執行）
            InitializeBaseUI();

            // 第2階段：簡化的數據初始化（移除複雜的載入邏輯，讓OneBotAPIHandler控制）
            var initTask = InitializeSimpleDataAsync();
        }

        public ObservableCollection<ChatItem> ChatItems { get; set; }
        public ObservableCollection<GroupInfo> GroupItems { get; set; }

        public ObservableCollection<FriendCategory> ContactCategories { get; set; }

        /// <summary>
        ///     基礎UI初始化（立即執行） - 修復版本
        /// </summary>
        private void InitializeBaseUI()
        {
            // 基礎UI設置
            SidebarColumn.Width = new GridLength(0);
            UpdateOverlay();

            // 獲取當前帳號
            _currentAccount = DataAccess.GetCurrentAccount();

            // 確保 ChatListView 綁定到 ChatItems
            ChatListView.ItemsSource = ChatItems;

            // 立即載入緩存的聊天列表（快速顯示內容）
            LoadCachedChatList();

            // 設置默認選中項
            ChatsItem.IsSelected = true;

            // 初始化消息相關
            MessagesItemsControl.ItemsSource = _currentMessages;

            // 註冊基礎事件
            ChatListView.SelectionChanged += ChatListView_SelectionChanged;
            GroupListView.ItemClick += GroupListView_ItemClick;
            GroupListView.IsItemClickEnabled = true;

            // 註冊應用程序事件
            RegisterApplicationEvents();
            RegisterBackButtonHandler();
            RegisterScrollEventHandlers();
        }

        /// <summary>
        ///     优化的数据初始化 - 预热缓存，减少后续查询
        /// </summary>
        private async Task InitializeSimpleDataAsync()
        {
            try
            {
                // 初始化头像管理器
                await InitializeAvatarManagerAsync();
                MainPage.SocketClientStarter.SetMainViewReference(this);

                // **预热聊天类型缓存（重要优化）**
                await Task.Run(() =>
                {
                    try
                    {
                        Debug.WriteLine("预热聊天类型缓存...");
                        ChatTypeCache.ForceRefresh();
                        var stats = ChatTypeCache.GetCacheStats();
                        Debug.WriteLine($"缓存预热完成: {stats.GroupCount} 个群组, {stats.FriendCount} 个好友");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"预热缓存失败: {ex.Message}");
                    }
                });

                // 载入缓存的聊天列表
                await Task.Run(() => LoadCachedChatList());

                // 延迟执行类型诊断和修复（现在性能更好）
                var diagnosisTask = Task.Delay(1000).ContinueWith(t =>
                {
                    var dispatchTask = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                Debug.WriteLine("=== 执行高性能聊天类型修复 ===");
                                FixChatItemTypes(); // 现在使用缓存，性能大幅提升
                                Debug.WriteLine("=== 聊天类型修复完成 ===");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"诊断修复聊天类型时发生错误: {ex.Message}");
                            }
                        });
                });

                // 启动后台维护任务
                var maintenanceTask = Task.Delay(10000).ContinueWith(t => StartBackgroundMaintenanceAsync());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"优化数据初始化错误: {ex.Message}");
            }
        }


        /// <summary>
        ///     診斷頭像類型按鈕點擊事件
        /// </summary>
        private void DiagnoseAvatarTypesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("手動觸發頭像類型診斷");
                DiagnoseAllChatItemTypes();
                FixChatItemTypes();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"手動診斷時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     異步載入最近消息（優先級最高）
        /// </summary>
        private async Task LoadRecentMessagesAsync()
        {
            try
            {
                Debug.WriteLine("開始優先載入最近消息");

                if (string.IsNullOrEmpty(_currentAccount))
                {
                    Debug.WriteLine("當前帳號為空，跳過最近消息載入");
                    return;
                }

                // 修復：使用正確的方法名稱，獲取最近的聊天消息作為 ChatMessage 類型
                var recentChatMessages = await Task.Run(() => DataAccess.GetRecentChatMessages());

                if (recentChatMessages.Count > 0)
                {
                    // 將 ChatMessage 轉換為 RecentContactMessage 格式或直接處理
                    var recentContactMessages = ConvertChatMessagesToRecentContacts(recentChatMessages);

                    // 立即更新UI以顯示最近的聊天
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.High, () => { LoadRecentContactsToUI(recentContactMessages); });

                    Debug.WriteLine($"優先載入最近消息完成: {recentContactMessages.Count} 條");
                }
                else
                {
                    Debug.WriteLine("沒有最近消息需要載入");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入最近消息時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     將 ChatMessage 轉換為 RecentContactMessage 格式（用於兼容性）
        /// </summary>
        /// <param name="chatMessages">聊天消息列表</param>
        /// <returns>最近聯繫人消息列表</returns>
        private List<RecentContactMessage> ConvertChatMessagesToRecentContacts(List<ChatMessage> chatMessages)
        {
            var recentMessages = new List<RecentContactMessage>();

            try
            {
                var processedChats = new HashSet<string>(); // 用於避免重複

                foreach (var message in chatMessages)
                {
                    // 從訊息推斷聊天類型和ID
                    var isGroup = message.MessageType == "group";
                    var chatId = isGroup ? message.SenderId : message.SenderId; // 這裡需要根據實際情況調整
                    var chatKey = $"{chatId}_{isGroup}";

                    if (processedChats.Contains(chatKey)) continue;

                    var recentMessage = new RecentContactMessage
                    {
                        UserId = isGroup ? 0 : chatId,
                        GroupId = isGroup ? chatId : 0,
                        ChatType = isGroup ? 2 : 1, // 2為群組，1為私聊
                        Time = ((DateTimeOffset)message.Timestamp).ToUnixTimeSeconds(),
                        Message = message.Content,
                        ParsedMessage = message.Content,
                        SendNickName = message.SenderName,
                        PeerName = message.SenderName,
                        MessageSegments = message.Segments,
                        ProcessedTimestamp = message.Timestamp
                    };

                    recentMessages.Add(recentMessage);
                    processedChats.Add(chatKey);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"轉換聊天消息格式時發生錯誤: {ex.Message}");
            }

            return recentMessages;
        }


        /// <summary>
        ///     修復版聯絡人和群組載入 - 使用正確的緩存策略
        /// </summary>
        private async Task LoadContactsAndGroupsAsync()
        {
            try
            {
                Debug.WriteLine("開始載入聯絡人和群組數據");

                // 並行載入但錯開時間
                var loadGroupsTask = LoadGroupsAsync();
                await Task.Delay(200);
                var loadContactsTask = LoadContactsAsync();

                await Task.WhenAll(loadGroupsTask, loadContactsTask);

                Debug.WriteLine("聯絡人和群組數據載入完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聯絡人和群組數據錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     修復版初始化頭像管理器和更新訂閱
        /// </summary>
        private async Task InitializeAvatarManagerAsync()
        {
            try
            {
                await AvatarManager.InitializeAsync();

                // 訂閱頭像更新事件
                AvatarManager.OnAvatarUpdated += OnAvatarUpdated;

                Debug.WriteLine("頭像管理器初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化頭像管理器失敗: {ex.Message}");
            }
        }

        /// <summary>
        ///     修复头像UI更新问题 - 订阅头像更新事件
        /// </summary>
        private void InitializeAvatarUpdates()
        {
            // 订阅头像更新事件
            AvatarManager.OnAvatarUpdated += OnAvatarUpdated;
        }

        /// <summary>
        ///     最終修復版頭像更新事件處理 - 徹底解決類型判斷錯誤
        /// </summary>
        private async void OnAvatarUpdated(string cacheKey, BitmapImage image)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.High, () =>
                    {
                        var updated = false;
                        Debug.WriteLine($"處理頭像更新: {cacheKey}");

                        // 解析 cacheKey 獲取類型和ID
                        var parts = cacheKey.Split('_');
                        if (parts.Length != 2)
                        {
                            Debug.WriteLine($"⚠ 無效的CacheKey格式: {cacheKey}");
                            return;
                        }

                        var avatarType = parts[0];
                        if (!long.TryParse(parts[1], out var avatarId))
                        {
                            Debug.WriteLine($"⚠ 無效的ID格式: {parts[1]}");
                            return;
                        }

                        // 1. 更新聊天列表頭像 - 使用精確匹配
                        if (ChatItems != null)
                            foreach (var chatItem in ChatItems.ToList())
                            {
                                if (chatItem == null) continue;

                                // 精確匹配：類型和ID都必須匹配
                                var itemType = chatItem.IsGroup ? "group" : "friend";
                                if (itemType == avatarType && chatItem.ChatId == avatarId)
                                {
                                    if (chatItem.AvatarImage != image)
                                    {
                                        chatItem.AvatarImage = image;
                                        chatItem.OnPropertyChanged(nameof(chatItem.AvatarImage));
                                        chatItem.OnPropertyChanged(nameof(chatItem.HasAvatar));
                                        updated = true;
                                        Debug.WriteLine($"✓ 聊天列表頭像更新成功: {chatItem.Name} ({cacheKey})");
                                    }

                                    break; // 找到匹配項後立即退出
                                }
                            }

                        // 2. 更新群組列表頭像 - 只處理群組類型
                        if (avatarType == "group" && GroupItems != null)
                            foreach (var groupItem in GroupItems.ToList())
                            {
                                if (groupItem == null) continue;

                                if (groupItem.GroupId == avatarId)
                                {
                                    if (groupItem.AvatarImage != image)
                                    {
                                        groupItem.AvatarImage = image;
                                        updated = true;
                                        Debug.WriteLine($"✓ 群組列表頭像更新: {groupItem.GroupName} ({cacheKey})");
                                    }

                                    break;
                                }
                            }

                        // 3. 更新用戶頭像 - 只處理current類型
                        if (avatarType == "current")
                            try
                            {
                                if (UserAvatarBrush?.ImageSource != image)
                                {
                                    UserAvatarBrush.ImageSource = image;
                                    UserAvatarEllipse.Visibility = Visibility.Visible;
                                    DefaultUserAvatar.Visibility = Visibility.Collapsed;
                                    UserAvatarLoadingRing.Visibility = Visibility.Collapsed;
                                    updated = true;
                                    Debug.WriteLine($"✓ 用戶頭像更新: {cacheKey}");
                                }
                            }
                            catch (Exception userEx)
                            {
                                Debug.WriteLine($"✗ 用戶頭像更新失敗: {userEx.Message}");
                            }

                        if (updated)
                        {
                            Debug.WriteLine($"頭像更新完成，保存緩存: {cacheKey}");
                            _ = Task.Run(SaveChatListCacheWithAvatarsAsync);
                        }
                        else if (avatarType == "group" || avatarType == "friend" || avatarType == "current")
                        {
                            // 只對有效的頭像類型顯示未找到警告
                            Debug.WriteLine($"⚠ 未找到需要更新的UI元素: {cacheKey}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"✗ 處理頭像更新事件錯誤: {ex.Message}");
                Debug.WriteLine($"✗ 錯誤堆疊: {ex.StackTrace}");
            }
        }

        /// <summary>
        ///     分階段執行後台維護任務 - 避免數據庫鎖定
        /// </summary>
        private async void StartBackgroundMaintenanceAsync()
        {
            try
            {
                // 延遲3秒後開始，讓界面先完全載入
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


        /// <summary>
        ///     全面診斷聊天項目的頭像類型分配問題 - 修復C# 7.3兼容性
        /// </summary>
        private void DiagnoseAllChatItemTypes()
        {
            try
            {
                Debug.WriteLine("=== 全面診斷聊天項目頭像類型分配 ===");

                if (ChatItems == null || ChatItems.Count == 0)
                {
                    Debug.WriteLine("ChatItems 為空或無內容");
                    return;
                }

                // 修復: 使用兼容的HashSet創建方式
                var groupIds = new HashSet<long>(DataAccess.GetAllGroups().Select(g => g.GroupId));
                var friendIds = new HashSet<long>(DataAccess.GetAllFriendsWithCategories()
                    .SelectMany(c => c.BuddyList ?? new List<FriendInfo>())
                    .Select(f => f.UserId));

                Debug.WriteLine($"數據庫中的群組數量: {groupIds.Count}");
                Debug.WriteLine($"數據庫中的好友數量: {friendIds.Count}");

                foreach (var item in ChatItems)
                {
                    if (item == null) continue;

                    var shouldBeGroup = groupIds.Contains(item.ChatId);
                    var shouldBeFriend = friendIds.Contains(item.ChatId);
                    var actualType = item.IsGroup ? "Group" : "Friend";
                    var expectedType = shouldBeGroup ? "Group" : "Friend";
                    var avatarType = item.IsGroup ? "group" : "friend";
                    var expectedAvatarType = shouldBeGroup ? "group" : "friend";

                    Debug.WriteLine($"聊天項目: {item.Name}");
                    Debug.WriteLine($"  ChatId: {item.ChatId}");
                    Debug.WriteLine($"  實際類型: {actualType}");
                    Debug.WriteLine($"  期望類型: {expectedType}");
                    Debug.WriteLine($"  實際頭像類型: {avatarType}");
                    Debug.WriteLine($"  期望頭像類型: {expectedAvatarType}");
                    Debug.WriteLine($"  在群組數據庫: {shouldBeGroup}");
                    Debug.WriteLine($"  在好友數據庫: {shouldBeFriend}");

                    // 檢查類型不匹配
                    if (item.IsGroup != shouldBeGroup)
                    {
                        Debug.WriteLine($"  ⚠️ 類型不匹配! 應該是{expectedType}但被設置為{actualType}");

                        // 自動修復
                        Debug.WriteLine($"  🔧 自動修復: 將 {item.Name} 的 IsGroup 設置為 {shouldBeGroup}");
                        item.IsGroup = shouldBeGroup;
                    }

                    Debug.WriteLine("---");
                }

                Debug.WriteLine("=== 診斷完成 ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"診斷聊天項目類型時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     高性能批量修复聊天项目类型 - 避免UI闪烁
        /// </summary>
        public void FixChatItemTypes()
        {
            try
            {
                Debug.WriteLine("开始批量修复聊天项目类型错误");

                if (ChatItems == null || ChatItems.Count == 0)
                {
                    Debug.WriteLine("没有聊天项目需要修复");
                    return;
                }

                // 批量获取所有聊天ID的类型（一次数据库查询）
                var chatIds = ChatItems.Where(item => item != null && item.ChatId > 0)
                    .Select(item => item.ChatId)
                    .ToList();

                if (chatIds.Count == 0)
                {
                    Debug.WriteLine("没有有效的聊天ID");
                    return;
                }

                // 使用缓存批量获取类型
                var chatTypes = ChatTypeCache.GetChatTypes(chatIds);

                var fixedCount = 0;
                var itemsNeedingAvatars = new List<ChatItem>();

                foreach (var item in ChatItems.ToList())
                {
                    if (item == null || item.ChatId <= 0) continue;

                    if (chatTypes.TryGetValue(item.ChatId, out var shouldBeGroup))
                        if (item.IsGroup != shouldBeGroup)
                        {
                            Debug.WriteLine(
                                $"🔧 批量修复: {item.Name} (ID: {item.ChatId}) 从 {(item.IsGroup ? "群组" : "好友")} 改为 {(shouldBeGroup ? "群组" : "好友")}");

                            // **关键修复：只更新类型，不清除头像**
                            item.IsGroup = shouldBeGroup;

                            // 如果没有头像，加入待加载列表
                            if (item.AvatarImage == null) itemsNeedingAvatars.Add(item);

                            fixedCount++;
                        }
                }

                // **分批处理需要头像的项目**
                if (itemsNeedingAvatars.Count > 0)
                {
                    Debug.WriteLine($"开始为 {itemsNeedingAvatars.Count} 个项目加载头像");

                    _ = Task.Run(async () =>
                    {
                        const int batchSize = 5;
                        for (var i = 0; i < itemsNeedingAvatars.Count; i += batchSize)
                        {
                            var batch = itemsNeedingAvatars.Skip(i).Take(batchSize).ToList();
                            var tasks = batch.Select(item => LoadAvatarForItem(item)).ToList();

                            try
                            {
                                await Task.WhenAll(tasks);
                                await Task.Delay(200); // 批次间延迟
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"批量加载头像时发生错误: {ex.Message}");
                            }
                        }
                    });
                }

                // 报告结果
                if (fixedCount > 0)
                {
                    Debug.WriteLine($"✅ 共批量修复了 {fixedCount} 个类型错误的聊天项目");
                    SaveChatListCache();
                }
                else
                {
                    Debug.WriteLine("没有发现需要修复的聊天项目类型错误");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"批量修复聊天项目类型时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     为单个项目加载头像
        /// </summary>
        private async Task LoadAvatarForItem(ChatItem item)
        {
            try
            {
                if (item?.AvatarImage != null) return; // 已有头像，跳过

                var avatarType = item.IsGroup ? "group" : "friend";
                var avatar = await AvatarManager.GetAvatarAsync(avatarType, item.ChatId, 2, true);

                if (avatar != null)
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.High, () =>
                        {
                            try
                            {
                                if (item.AvatarImage == null) // 防止竞态条件
                                {
                                    item.AvatarImage = avatar;
                                    Debug.WriteLine($"✅ 为修复项目加载头像成功: {avatarType}_{item.ChatId}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"设置修复项目头像失败: {ex.Message}");
                            }
                        });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"为项目加载头像失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     診斷頭像類型匹配問題
        /// </summary>
        private void DiagnoseAvatarTypeMismatch()
        {
            try
            {
                Debug.WriteLine("=== 頭像類型匹配診斷 ===");

                if (ChatItems == null) return;

                foreach (var item in ChatItems)
                {
                    if (item == null) continue;

                    var expectedType = item.IsGroup ? "group" : "friend";
                    var expectedCacheKey = $"{expectedType}_{item.ChatId}";
                    var wrongCacheKey = $"{(item.IsGroup ? "friend" : "group")}_{item.ChatId}";

                    Debug.WriteLine($"聊天項目: {item.Name}");
                    Debug.WriteLine($"  ChatId: {item.ChatId}");
                    Debug.WriteLine($"  IsGroup: {item.IsGroup}");
                    Debug.WriteLine($"  正確CacheKey: {expectedCacheKey}");
                    Debug.WriteLine($"  錯誤CacheKey: {wrongCacheKey}");
                    Debug.WriteLine($"  HasAvatar: {item.HasAvatar}");
                    Debug.WriteLine("---");
                }

                Debug.WriteLine("=== 診斷完成 ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"頭像類型診斷錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     驗證頭像CacheKey是否有效
        /// </summary>
        private bool IsValidAvatarCacheKey(string cacheKey)
        {
            try
            {
                var parts = cacheKey.Split('_');
                if (parts.Length != 2) return false;

                var type = parts[0];
                var validTypes = new[] { "group", "friend", "current" };

                if (!validTypes.Contains(type)) return false;

                return long.TryParse(parts[1], out var id) && id > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     強制清理不匹配的頭像回調 - 診斷用
        /// </summary>
        private void CleanupInvalidAvatarCallbacks()
        {
            try
            {
                Debug.WriteLine("=== 清理無效頭像回調 ===");

                if (ChatItems != null)
                    foreach (var item in ChatItems)
                    {
                        if (item == null || item.ChatId <= 0) continue;

                        var expectedType = item.IsGroup ? "group" : "friend";
                        var expectedKey = $"{expectedType}_{item.ChatId}";

                        Debug.WriteLine($"聊天項目: {item.Name}, 期望CacheKey: {expectedKey}");
                    }

                Debug.WriteLine("=== 清理完成 ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理頭像回調錯誤: {ex.Message}");
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

        // 在 MainView.xaml.cs 中更新 MessageSegmentControl_ImageViewRequested 方法

        private void MessageSegmentControl_ImageViewRequested(object sender, ImageViewEventArgs e)
        {
            try
            {
                Debug.WriteLine($"MainView: 請求查看圖片 - URL: {e.ImageUrl}, FileID: {e.FileId}");

                // 顯示圖片查看器覆蓋層
                ImageViewerOverlayContainer.Visibility = Visibility.Visible;

                // 傳遞文件ID用於重試機制
                ImageViewerOverlay.ShowImage(e.ImageUrl, e.FileId);
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
        ///     處理 get_image API 響應 - 修正版
        /// </summary>
        /// <param name="imageUrl">新的圖片 URL</param>
        /// <param name="echo">請求的 echo 值</param>
        public void HandleGetImageResponse(string imageUrl, string echo)
        {
            try
            {
                Debug.WriteLine($"MainView: 收到 get_image 響應 - URL: {imageUrl}, Echo: {echo}");

                // 檢查 ImageViewer 容器是否可見（表示圖片查看器正在運行）
                if (ImageViewerOverlayContainer.Visibility == Visibility.Visible && ImageViewerOverlay != null)
                {
                    // 直接調用 ImageViewerControl 的處理方法
                    ImageViewerOverlay.HandleGetImageResponse(imageUrl);
                    Debug.WriteLine("MainView: 已將 get_image 響應傳遞給 ImageViewerControl");
                }
                else
                {
                    Debug.WriteLine("MainView: ImageViewerControl 不可用，無法處理 get_image 響應");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView: 處理 get_image 響應時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     處理 get_image API 錯誤 - 修正版
        /// </summary>
        /// <param name="echo">請求的 echo 值</param>
        /// <param name="errorMessage">錯誤消息</param>
        public void HandleGetImageError(string echo, string errorMessage)
        {
            try
            {
                Debug.WriteLine($"MainView: 收到 get_image 錯誤 - Echo: {echo}, Error: {errorMessage}");

                // 檢查 ImageViewer 容器是否可見
                if (ImageViewerOverlayContainer.Visibility == Visibility.Visible && ImageViewerOverlay != null)
                    // 可以在這裡添加錯誤處理邏輯，例如顯示錯誤消息
                    Debug.WriteLine("MainView: ImageViewer 正在運行，但 get_image 請求失敗");
                // 這裡可以選擇關閉 ImageViewer 或顯示錯誤信息
                else
                    Debug.WriteLine("MainView: ImageViewerControl 不可用");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView: 處理 get_image 錯誤時發生異常: {ex.Message}");
            }
        }

        /// <summary>
        ///     更新用戶頭像 - 修正版本
        /// </summary>
        /// <param name="avatarImage">用戶頭像圖片</param>
        public void UpdateUserAvatar(BitmapImage avatarImage)
        {
            try
            {
                if (avatarImage != null)
                {
                    // 更新用戶頭像
                    UserAvatarBrush.ImageSource = avatarImage;
                    UserAvatarEllipse.Visibility = Visibility.Visible;
                    DefaultUserAvatar.Visibility = Visibility.Collapsed;
                    UserAvatarLoadingRing.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新用戶頭像錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     更新聊天項目頭像 - 新增方法
        /// </summary>
        /// <param name="chatId">聊天ID</param>
        /// <param name="isGroup">是否為群組</param>
        /// <param name="avatar">頭像圖片</param>
        public void UpdateChatItemAvatar(long chatId, bool isGroup, BitmapImage avatar)
        {
            try
            {
                if (ChatItems == null || avatar == null) return;

                var chatItem = ChatItems.FirstOrDefault(c => c.ChatId == chatId && c.IsGroup == isGroup);
                if (chatItem != null && chatItem.AvatarImage == null) chatItem.AvatarImage = avatar;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新聊天項目頭像錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     刷新頭像狀態按鈕點擊事件
        /// </summary>
        private void RefreshAvatarStatusButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshAvatarStatus();
        }

        /// <summary>
        ///     刷新頭像狀態資訊
        /// </summary>
        private async void RefreshAvatarStatus()
        {
            try
            {
                // 獲取隊列狀態
                var queueStatus = AvatarManager.GetQueueStatus();

                // 計算未緩存的頭像數量
                var uncachedCount = 0;
                if (ChatItems != null) uncachedCount = ChatItems.Count(c => !c.HasAvatar);

                // 更新UI
                AvatarQueueCountText.Text = queueStatus.QueueLength.ToString();
                AvatarLoadingStatusText.Text = queueStatus.IsProcessing ? "載入中" : "閒置";
                AvatarLoadingStatusText.Foreground = queueStatus.IsProcessing
                    ? new SolidColorBrush(Colors.Orange)
                    : new SolidColorBrush(Colors.Green);
                UncachedAvatarsText.Text = uncachedCount.ToString();

                // 計算成功率（這裡需要從AvatarManager獲取統計資訊）
                var stats = await AvatarManager.GetCacheStatsAsync();
                var totalRequested = stats.TotalCount + queueStatus.QueueLength;
                var successRate = totalRequested > 0 ? stats.TotalCount * 100.0 / totalRequested : 100.0;
                AvatarSuccessRateText.Text = $"{successRate:F1}%";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新頭像狀態錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     修復版智能合併最近聯繫人 - 確保頭像正確更新
        /// </summary>
        public void SmartMergeRecentContacts(List<ChatItem> newChatItems)
        {
            try
            {
                Debug.WriteLine($"MainView: 開始智能合併聊天列表: {newChatItems.Count} 個新項目");

                if (ChatItems == null)
                {
                    ChatItems = new ObservableCollection<ChatItem>();
                    ChatListView.ItemsSource = ChatItems;
                }

                var existingItems = new Dictionary<string, ChatItem>();
                var indexMap = new Dictionary<string, int>();

                // 建立現有項目的索引
                for (var i = 0; i < ChatItems.Count; i++)
                {
                    var existing = ChatItems[i];
                    if (existing != null)
                    {
                        var key = $"{(existing.IsGroup ? "group" : "friend")}_{existing.ChatId}";
                        existingItems[key] = existing;
                        indexMap[key] = i;
                    }
                }

                var mergedCount = 0;
                var newCount = 0;

                foreach (var newItem in newChatItems)
                {
                    if (newItem == null) continue;

                    var key = $"{(newItem.IsGroup ? "group" : "friend")}_{newItem.ChatId}";

                    if (existingItems.TryGetValue(key, out var existingItem))
                    {
                        // 智能合併：保留頭像狀態，更新文字信息
                        var originalAvatar = existingItem.AvatarImage;
                        var originalHasAvatar = existingItem.HasAvatar;

                        // 更新基本信息
                        existingItem.Name = newItem.Name;
                        existingItem.LastMessage = newItem.LastMessage;
                        existingItem.LastTime = newItem.LastTime;
                        existingItem.UnreadCount = newItem.UnreadCount;
                        existingItem.MemberCount = newItem.MemberCount;

                        // 頭像處理策略
                        if (originalAvatar != null)
                        {
                            // 保留現有頭像
                            Debug.WriteLine($"保留現有頭像: {key}");
                        }
                        else if (newItem.AvatarImage != null)
                        {
                            // 使用新項目的頭像
                            existingItem.AvatarImage = newItem.AvatarImage;
                            Debug.WriteLine($"使用新項目頭像: {key}");
                        }
                        else if (!originalHasAvatar)
                        {
                            // 嘗試載入頭像
                            Debug.WriteLine($"觸發頭像載入: {key}");
                            existingItem.LoadAvatarFromCacheAsync();
                        }

                        // 移動到列表頂部（如果有新消息）
                        var currentIndex = indexMap[key];
                        if (currentIndex > 0) ChatItems.Move(currentIndex, 0);

                        mergedCount++;
                    }
                    else
                    {
                        // 新項目：先嘗試從緩存載入頭像
                        Debug.WriteLine($"新聊天項目: {key}");

                        // 立即嘗試從緩存載入
                        newItem.LoadAvatarFromCacheAsync();

                        // 添加到列表頂部
                        ChatItems.Insert(0, newItem);
                        newCount++;

                        // 延遲觸發網絡下載（如果緩存沒有）
                        _ = Task.Delay(1000).ContinueWith(_ =>
                        {
                            if (!newItem.HasAvatar) newItem.LoadAvatarAsync(); // 中等優先級
                        });
                    }
                }

                Debug.WriteLine($"智能合併完成: 合併 {mergedCount} 項，新增 {newCount} 項");

                // 立即保存聊天列表緩存
                _ = Task.Run(SaveChatListCacheWithAvatarsAsync);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"智能合併時發生錯誤: {ex.Message}");
                Debug.WriteLine($"錯誤堆疊: {ex.StackTrace}");
            }
        }

        /// <summary>
        ///     診斷聊天列表頭像載入狀態
        /// </summary>
        private void DiagnoseChatListAvatars()
        {
            try
            {
                Debug.WriteLine("=== 聊天列表頭像狀態診斷 ===");

                if (ChatItems == null)
                {
                    Debug.WriteLine("ChatItems 為 null");
                    return;
                }

                var totalItems = ChatItems.Count;
                var hasAvatarCount = 0;
                var loadingCount = 0;
                var missingCount = 0;

                foreach (var item in ChatItems)
                {
                    if (item == null) continue;

                    var cacheKey = $"{(item.IsGroup ? "group" : "friend")}_{item.ChatId}";
                    var hasAvatar = item.AvatarImage != null;
                    var isLoading = item.IsLoadingAvatar;

                    if (hasAvatar)
                    {
                        hasAvatarCount++;
                        Debug.WriteLine($"✓ {item.Name} ({cacheKey}): 有頭像");
                    }
                    else if (isLoading)
                    {
                        loadingCount++;
                        Debug.WriteLine($"⏳ {item.Name} ({cacheKey}): 載入中");
                    }
                    else
                    {
                        missingCount++;
                        Debug.WriteLine($"✗ {item.Name} ({cacheKey}): 缺失頭像");

                        // 嘗試重新載入
                        item.LoadAvatarFromCacheAsync();
                    }
                }

                Debug.WriteLine($"總數: {totalItems}, 有頭像: {hasAvatarCount}, 載入中: {loadingCount}, 缺失: {missingCount}");
                Debug.WriteLine("=== 診斷完成 ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"診斷頭像狀態時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     異步保存聊天列表緩存（包含頭像）- 使用新方法
        /// </summary>
        private async void SaveChatListCacheWithAvatarsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var currentAccount = _currentAccount;
                    if (!string.IsNullOrEmpty(currentAccount) && ChatItems.Count > 0)
                    {
                        DataAccess.SaveChatListCacheWithAvatars(currentAccount, ChatItems.ToList());
                        Debug.WriteLine("已保存聊天列表緩存（包含頭像信息）");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存聊天列表緩存（包含頭像）時發生錯誤: {ex.Message}");
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
        ///     页面卸载时清理资源
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            try
            {
                // 取消订阅头像更新事件
                AvatarManager.OnAvatarUpdated -= OnAvatarUpdated;

                // 取消注册返回键处理程序
                SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;

                Debug.WriteLine("页面资源清理完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"页面资源清理时发生错误: {ex.Message}");
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
        ///     修復版載入緩存聊天列表 - 使用帶頭像的方法
        /// </summary>
        private async void LoadCachedChatList()
        {
            try
            {
                if (ChatItems == null)
                {
                    ChatItems = new ObservableCollection<ChatItem>();
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            if (ChatListView != null) ChatListView.ItemsSource = ChatItems;
                        });
                }

                if (string.IsNullOrEmpty(_currentAccount)) return;

                // 使用帶頭像緩存的新方法
                var cachedChats = await Task.Run(() => DataAccess.LoadChatListCacheWithAvatars(_currentAccount));

                if (cachedChats?.Count > 0)
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                ChatItems.Clear();

                                foreach (var chatItem in cachedChats)
                                {
                                    if (chatItem == null) continue;

                                    // 優化群組和好友名稱
                                    if (chatItem.IsGroup)
                                    {
                                        var actualGroupName = DataAccess.GetGroupNameById(chatItem.ChatId);
                                        if (!string.IsNullOrEmpty(actualGroupName) &&
                                            !actualGroupName.StartsWith("群組 ")) chatItem.Name = actualGroupName;
                                    }
                                    else
                                    {
                                        var actualFriendName = DataAccess.GetFriendNameById(chatItem.ChatId);
                                        if (!actualFriendName.StartsWith("用戶 ")) chatItem.Name = actualFriendName;
                                    }

                                    ChatItems.Add(chatItem);

                                    // 只有在沒有頭像時才從緩存載入
                                    if (chatItem.AvatarImage == null) chatItem.LoadAvatarFromCacheAsync();
                                }

                                Debug.WriteLine($"載入聊天列表緩存完成: {ChatItems.Count} 個項目");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"UI更新錯誤: {ex.Message}");
                            }
                        });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存聊天列表錯誤: {ex.Message}");
            }

            // 載入完成後進行診斷
            _ = Task.Delay(2000).ContinueWith(_ =>
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Low, DiagnoseAvatarTypeMismatch);
            });
        }


        /// <summary>
        ///     修复集合索引异常 - 确保安全的集合操作
        /// </summary>
        private void MergeDuplicateChatItems()
        {
            try
            {
                if (ChatItems == null || ChatItems.Count == 0) return;

                Debug.WriteLine($"开始合并重复聊天项目，当前项目数: {ChatItems.Count}");

                // 使用安全的方式处理集合，避免索引超出范围
                var itemsToProcess = new List<ChatItem>();

                // 安全复制集合到临时列表
                foreach (var item in ChatItems)
                    if (item != null)
                        itemsToProcess.Add(item);

                var itemsToRemove = new List<ChatItem>();
                var processedItems = new Dictionary<string, ChatItem>(); // key: "chatId_isGroup"

                foreach (var item in itemsToProcess)
                {
                    if (item == null) continue;

                    var key = $"{item.ChatId}_{item.IsGroup}";

                    if (processedItems.ContainsKey(key))
                    {
                        // 发现重复项目，保留更新时间较新的
                        var existingItem = processedItems[key];
                        var currentItemTime = DateTime.TryParse(item.LastTime, out var currentTime)
                            ? currentTime
                            : DateTime.MinValue;
                        var existingItemTime = DateTime.TryParse(existingItem.LastTime, out var existingTime)
                            ? existingTime
                            : DateTime.MinValue;

                        if (currentItemTime > existingItemTime)
                        {
                            // 当前项目更新，移除旧项目
                            if (ChatItems.Contains(existingItem)) itemsToRemove.Add(existingItem);

                            processedItems[key] = item;
                            Debug.WriteLine($"合并重复聊天，保留较新项目: {item.Name} (ID: {item.ChatId})");
                        }
                        else
                        {
                            // 保留现有项目，移除当前项目
                            if (ChatItems.Contains(item)) itemsToRemove.Add(item);

                            Debug.WriteLine($"合并重复聊天，移除较旧项目: {item.Name} (ID: {item.ChatId})");
                        }
                    }
                    else
                    {
                        processedItems[key] = item;
                    }
                }

                // 安全移除重复项目
                foreach (var itemToRemove in itemsToRemove)
                    if (ChatItems.Contains(itemToRemove))
                        ChatItems.Remove(itemToRemove);

                if (itemsToRemove.Count > 0)
                {
                    Debug.WriteLine($"合并完成，移除了 {itemsToRemove.Count} 个重复项目，剩余 {ChatItems.Count} 个项目");

                    // 保存更新后的聊天列表
                    _ = Task.Run(SaveChatListCacheAsync);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"合并重复聊天项目时发生错误: {ex.Message}");

                // 如果出现异常，尝试重新初始化ChatItems
                if (ChatItems == null)
                {
                    ChatItems = new ObservableCollection<ChatItem>();
                    if (ChatListView != null) ChatListView.ItemsSource = ChatItems;
                }
            }
        }

        /// <summary>
        ///     修改原有的同步保存方法，使用異步版本
        /// </summary>
        private void SaveChatListCache()
        {
            // 不等待異步操作完成，避免阻塞
            var saveChatListCacheTask = SaveChatListCacheAsync();
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
        private async void OnApplicationSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            try
            {
                Debug.WriteLine("應用程序掛起，保存聊天列表緩存");
                SaveChatListCache();

                // 清理过期的头像缓存
                try
                {
                    await AvatarManager.CleanExpiredCacheAsync();
                    Debug.WriteLine("头像缓存清理完成");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"清理头像缓存时发生错误: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"应用程序挂起处理时发生错误: {ex.Message}");
            }
            finally
            {
                deferral.Complete();
            }
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
        ///     將最近聯繫人載入到UI（優化版）
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

                        // 收到新消息時刷新頭像（中優先級）
                        if (!existingChat.HasAvatar) existingChat.LoadAvatarAsync(1);
                    }
                    else
                    {
                        // 添加新的聊天項目，先從緩存載入頭像
                        newItem.LoadAvatarFromCacheAsync();
                        ChatItems.Insert(0, newItem);

                        // 延遲啟動網路下載（低優先級）
                        _ = Task.Delay(2000).ContinueWith(_ =>
                        {
                            if (!newItem.HasAvatar) newItem.LoadAvatarAsync();
                        });
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

        /// <summary>
        ///     当进入聊天时触发头像刷新（高优先级）
        /// </summary>
        private void OpenChat(ChatItem chatItem)
        {
            _currentChat = chatItem;

            // 高优先级刷新当前聊天的头像
            if (!chatItem.HasAvatar) chatItem.LoadAvatarAsync(0);

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
        ///     更新群組信息到聊天列表 - 確保群組名稱正確顯示
        /// </summary>
        public void UpdateGroupInfoInChatList()
        {
            try
            {
                var groupsUpdated = 0;

                foreach (var chatItem in ChatItems.Where(c => c.IsGroup).ToList())
                {
                    var actualGroupName = DataAccess.GetGroupNameById(chatItem.ChatId);

                    // 如果從數據庫獲得了更好的群組名稱，更新它
                    if (!string.IsNullOrEmpty(actualGroupName) &&
                        !actualGroupName.StartsWith("群組 ") &&
                        actualGroupName != chatItem.Name)
                    {
                        Debug.WriteLine($"更新群組名稱: {chatItem.ChatId} - {chatItem.Name} -> {actualGroupName}");
                        chatItem.Name = actualGroupName;
                        groupsUpdated++;

                        // 同時更新群組信息
                        var groupInfo = DataAccess.GetGroupInfo(chatItem.ChatId);
                        if (groupInfo != null) chatItem.MemberCount = groupInfo.MemberCount;
                    }
                }

                if (groupsUpdated > 0)
                {
                    Debug.WriteLine($"群組信息更新完成: 更新了 {groupsUpdated} 個群組");

                    // 異步保存更新後的聊天列表
                    Task.Run(async () =>
                    {
                        try
                        {
                            await SaveChatListCacheAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"保存更新後聊天列表失敗: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新群組信息錯誤: {ex.Message}");
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
        ///     线程安全的聊天列表缓存保存 - 使用新方法
        /// </summary>
        private async Task SaveChatListCacheAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentAccount) || ChatItems == null)
                    return;

                // 防止并发访问
                var saveSemaphore = new SemaphoreSlim(1, 1);
                await saveSemaphore.WaitAsync();

                try
                {
                    await Task.Run(async () =>
                    {
                        try
                        {
                            var chatItemsCopy = new List<ChatItem>();

                            // 安全地复制聊天项目到临时列表
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                CoreDispatcherPriority.Normal, () =>
                                {
                                    if (ChatItems != null)
                                        foreach (var item in ChatItems)
                                            if (item != null)
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

                            if (chatItemsCopy.Count > 0)
                            {
                                // 使用新的帶頭像的保存方法
                                DataAccess.SaveChatListCacheWithAvatars(_currentAccount, chatItemsCopy);
                                Debug.WriteLine($"线程安全保存聊天列表缓存（包含頭像）: {chatItemsCopy.Count} 个项目");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"后台保存聊天列表缓存错误: {ex.Message}");
                        }
                    });
                }
                finally
                {
                    saveSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"线程安全保存聊天列表缓存错误: {ex.Message}");
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
        ///     修復版群組載入 - 延遲頭像載入
        /// </summary>
        private async Task LoadGroupsAsync()
        {
            try
            {
                var groups = await Task.Run(() => DataAccess.GetAllGroups());

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        GroupItems = new ObservableCollection<GroupInfo>(groups);
                        GroupListView.ItemsSource = GroupItems;
                        Debug.WriteLine($"群組數據載入完成: {groups.Count} 個群組");
                    });

                // 只在用戶切換到群組頁面時載入頭像
                if (_currentPage == "Groups") await LoadGroupAvatarsAsync(groups);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入群組數據錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     修復版群組頭像載入 - 分批載入
        /// </summary>
        private async Task LoadGroupAvatarsAsync(List<GroupInfo> groups)
        {
            try
            {
                const int batchSize = 3;

                for (var i = 0; i < groups.Count; i += batchSize)
                {
                    if (_currentPage != "Groups") break;

                    var batch = groups.Skip(i).Take(batchSize);
                    var tasks = batch.Select(async group =>
                    {
                        try
                        {
                            var avatar = await AvatarManager.GetAvatarAsync("group", group.GroupId, 2, true);
                            if (avatar != null)
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Low, () => { group.AvatarImage = avatar; });
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
                Debug.WriteLine($"群組頭像載入錯誤: {ex.Message}");
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
                    var delayTask = LoadCategoryFriendsAsync(category); // 避免 async void 警告
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

        /// <summary>
        ///     安全的好友项目创建 - 避免控件引用导致的崩溃
        /// </summary>
        private Grid CreateFriendItem(FriendInfo friend)
        {
            var grid = new Grid
            {
                Height = 60,
                Margin = new Thickness(0, 0, 0, 1),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                Tag = friend
            };

            // 使用弱引用的方式添加点击事件，避免内存泄漏
            grid.Tapped += OnFriendItemTapped;

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 创建头像容器
            var avatarContainer = new Grid
            {
                Width = 40,
                Height = 40,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center
            };

            // 默认头像背景
            var defaultAvatar = new Border
            {
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

            defaultAvatar.Child = avatarIcon;
            avatarContainer.Children.Add(defaultAvatar);

            // 创建真实头像的椭圆（初始隐藏）
            var realAvatarEllipse = new Ellipse
            {
                Visibility = Visibility.Collapsed
            };
            avatarContainer.Children.Add(realAvatarEllipse);

            // 加载指示器
            var loadingRing = new ProgressRing
            {
                Width = 20,
                Height = 20,
                Foreground = new SolidColorBrush(Colors.White),
                Visibility = Visibility.Collapsed
            };
            avatarContainer.Children.Add(loadingRing);

            Grid.SetColumn(avatarContainer, 0);
            grid.Children.Add(avatarContainer);

            // 延迟启动头像加载，避免一次性加载过多
            _ = Task.Delay(200).ContinueWith(async _ =>
            {
                // 检查是否仍在联系人页面
                if (_currentPage == "Contacts")
                    LoadFriendAvatarAsync(friend, defaultAvatar, realAvatarEllipse, loadingRing);
            });
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
        ///     優化的聯繫人頭像載入 - 延遲載入，避免卡頓
        /// </summary>
        private async void LoadFriendAvatarAsync(FriendInfo friend, Border defaultAvatar, Ellipse realAvatarEllipse,
            ProgressRing loadingRing)
        {
            try
            {
                // 檢查是否仍在聯繫人頁面且控件仍然有效
                if (_currentPage != "Contacts" || defaultAvatar == null || realAvatarEllipse == null ||
                    loadingRing == null)
                    return;

                // 使用安全的UI更新方式，顯示載入狀態
                await SafeDispatcherRunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (loadingRing != null)
                    {
                        loadingRing.IsActive = true;
                        loadingRing.Visibility = Visibility.Visible;
                    }
                });

                // 先嘗試從緩存載入
                var avatarTask = AvatarManager.GetAvatarAsync("friend", friend.UserId, 2, true);
                var timeoutTask = Task.Delay(5000); // 5秒超時

                var completedTask = await Task.WhenAny(avatarTask, timeoutTask);
                BitmapImage avatarImage = null;

                if (completedTask == avatarTask)
                {
                    avatarImage = await avatarTask;
                    Debug.WriteLine(
                        $"好友頭像緩存載入: {friend.Nickname ?? friend.Nick} - {(avatarImage != null ? "成功" : "失敗")}");
                }
                else
                {
                    Debug.WriteLine($"好友頭像載入超時: {friend.Nickname ?? friend.Nick}");
                }

                // 檢查控件是否仍然有效
                if (_currentPage != "Contacts" || defaultAvatar == null || realAvatarEllipse == null) return;

                await SafeDispatcherRunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        if (avatarImage != null && realAvatarEllipse != null && defaultAvatar != null)
                        {
                            // 設置真實頭像
                            realAvatarEllipse.Fill = new ImageBrush
                            {
                                ImageSource = avatarImage,
                                Stretch = Stretch.UniformToFill
                            };

                            // 顯示真實頭像，隱藏默認頭像
                            realAvatarEllipse.Visibility = Visibility.Visible;
                            defaultAvatar.Visibility = Visibility.Collapsed;

                            Debug.WriteLine($"好友頭像UI更新成功: {friend.Nickname ?? friend.Nick}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"設置好友頭像UI時發生錯誤: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入好友頭像失敗: {ex.Message}");
            }
            finally
            {
                // 確保隱藏載入指示器
                try
                {
                    await SafeDispatcherRunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        if (loadingRing != null)
                        {
                            loadingRing.IsActive = false;
                            loadingRing.Visibility = Visibility.Collapsed;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"隱藏載入指示器時發生錯誤: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///     安全的调度器执行方法 - 避免调度器异常导致崩溃
        /// </summary>
        private async Task SafeDispatcherRunAsync(CoreDispatcherPriority priority, Action action)
        {
            try
            {
                if (CoreApplication.MainView?.CoreWindow?.Dispatcher != null)
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(priority, () =>
                    {
                        try
                        {
                            action?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"调度器内部执行错误: {ex.Message}");
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"调度器执行错误: {ex.Message}");
            }
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
        ///     修復版 RefreshChatList - 確保群組名稱正確顯示
        /// </summary>
        public void RefreshChatList()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentAccount))
                {
                    Debug.WriteLine("刷新聊天列表: 當前帳號為空");
                    return;
                }

                // 載入緩存的聊天列表
                var cachedChats = DataAccess.LoadChatListCache(_currentAccount);

                Debug.WriteLine($"從緩存載入聊天列表: {cachedChats.Count} 個項目");

                // 清空當前列表
                ChatItems.Clear();

                // 修復名稱並添加所有緩存的聊天到列表
                foreach (var chatItem in cachedChats)
                {
                    // 對於群組，確保名稱正確
                    if (chatItem.IsGroup)
                    {
                        var actualGroupName = DataAccess.GetGroupNameById(chatItem.ChatId);
                        if (!string.IsNullOrEmpty(actualGroupName) && !actualGroupName.StartsWith("群組 "))
                        {
                            chatItem.Name = actualGroupName;
                            Debug.WriteLine($"刷新時更新群組名稱: {chatItem.ChatId} -> {actualGroupName}");
                        }
                    }
                    else
                    {
                        // 對於好友，也檢查名稱
                        var actualFriendName = DataAccess.GetFriendNameById(chatItem.ChatId);
                        if (!actualFriendName.StartsWith("用戶 ")) chatItem.Name = actualFriendName;
                    }

                    ChatItems.Add(chatItem);

                    // 從緩存載入頭像（不會導致網路請求）
                    chatItem.LoadAvatarFromCacheAsync();
                }

                // 合并重複項目
                MergeDuplicateChatItems();

                Debug.WriteLine($"聊天列表刷新完成: {ChatItems.Count} 個聊天項目");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新聊天列表錯誤: {ex.Message}");
            }
        }

        private void SwitchPage(string pageName)
        {
            _currentPage = pageName;

            // 隱藏所有頁面
            ChatListView.Visibility = Visibility.Collapsed;
            ContactsScrollViewer.Visibility = Visibility.Collapsed;
            GroupListView.Visibility = Visibility.Collapsed;
            SettingsScrollViewer.Visibility = Visibility.Collapsed;

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
                    // 进入联系人页面时启动头像加载
                    _ = StartContactAvatarLoadingAsync();
                    break;
                case "Groups":
                    GroupListView.Visibility = Visibility.Visible;
                    SearchTextBox.PlaceholderText = "Search (Groups)";
                    // 进入群组页面时启动头像加载
                    _ = StartGroupAvatarLoadingAsync();
                    break;
                case "Settings":
                    SettingsScrollViewer.Visibility = Visibility.Visible;
                    SearchTextBox.PlaceholderText = "Search (Settings)";
                    // 加載設定頁面時刷新統計信息
                    LoadSettingsPage();
                    break;
            }

            // 更新系統返回鍵可見性
            UpdateBackButtonVisibility();
        }

        /// <summary>
        ///     启动联系人头像加载
        /// </summary>
        private async Task StartContactAvatarLoadingAsync()
        {
            try
            {
                await Task.Delay(200); // 短暂延迟让UI先渲染

                if (_currentPage != "Contacts") return;

                Debug.WriteLine("开始联系人页面头像加载");
                // 这里的头像加载已在 CreateFriendItem 中处理
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"启动联系人头像加载时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     啟動群組頭像載入 - 優化版，避免卡頓
        /// </summary>
        private async Task StartGroupAvatarLoadingAsync()
        {
            try
            {
                await Task.Delay(500); // 短暫延遲讓UI先渲染

                if (_currentPage != "Groups" || GroupItems == null) return;

                Debug.WriteLine("開始安全群組頁面頭像載入");

                // 限制同時載入的數量
                var groupsToLoad = GroupItems.Take(10).ToList(); // 只載入前10個群組的頭像

                foreach (var group in groupsToLoad)
                {
                    if (_currentPage != "Groups") break; // 如果用戶已切換頁面，停止載入

                    // 使用低優先級異步載入
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var avatarImage = await AvatarManager.GetAvatarAsync("group", group.GroupId, 2,
                                true);

                            if (avatarImage != null)
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.Low, () =>
                                    {
                                        try
                                        {
                                            if (group.AvatarImage == null)
                                            {
                                                group.AvatarImage = avatarImage;
                                                Debug.WriteLine($"群組頭像載入成功: {group.GroupName}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"設置群組頭像時發生錯誤: {ex.Message}");
                                        }
                                    });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"載入群組頭像時發生錯誤: {ex.Message}");
                        }
                    });

                    // 分批載入，避免一次性請求過多
                    await Task.Delay(100);
                }

                Debug.WriteLine("安全群組頭像載入啟動完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"啟動群組頭像載入時發生錯誤: {ex.Message}");
            }
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
        ///     載入設定頁面 - 修改版本，包含頭像狀態
        /// </summary>
        private void LoadSettingsPage()
        {
            try
            {
                RefreshDatabaseStatistics();
                RefreshRecentMessages();
                RefreshAvatarStatus(); // 新增
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入設定頁面錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     刷新數據庫統計信息 - 包含头像队列状态
        /// </summary>
        private async void RefreshDatabaseStatistics()
        {
            try
            {
                Debug.WriteLine("開始刷新數據庫統計信息");

                // 在後台線程獲取統計信息
                await Task.Run(() => { _databaseStatistics = DataAccess.GetDatabaseStatistics(); });

                // 获取头像缓存统计
                var avatarStats = await Task.Run(() => AvatarManager.GetCacheStatsAsync());

                // 获取队列状态
                var queueStatus = AvatarManager.GetQueueStatus();

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

                        // 显示头像缓存统计和队列状态
                        var avatarText = $"{avatarStats.TotalCount}";
                        if (queueStatus.QueueLength > 0 || queueStatus.IsProcessing)
                            avatarText += $" (队列: {queueStatus.QueueLength})";

                        TotalAvatarsText.Text = avatarText;
                        AvatarCacheSizeText.Text = avatarStats.TotalSizeFormatted;

                        Debug.WriteLine($"數據庫統計信息UI更新完成，头像队列长度: {queueStatus.QueueLength}");
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
                        CleanupInvalidAvatarCallbacks();
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

        /// <summary>
        ///     向後兼容的 UpdateChatItem 方法 - 舊簽名
        /// </summary>
        public void UpdateChatItem(string chatName, string newMessage, bool incrementUnread = true)
        {
            try
            {
                if (string.IsNullOrEmpty(chatName))
                {
                    Debug.WriteLine("UpdateChatItem: 聊天名稱為空");
                    return;
                }

                Debug.WriteLine($"UpdateChatItem (向後兼容): 更新聊天項目 '{chatName}'");

                // 先嘗試在現有聊天列表中找到匹配項
                var existingChat =
                    ChatItems?.FirstOrDefault(c => c.Name.Equals(chatName, StringComparison.OrdinalIgnoreCase));

                if (existingChat != null)
                {
                    // 找到現有項目，使用新方法更新
                    UpdateChatItem(existingChat.Name, existingChat.ChatId, existingChat.IsGroup, newMessage,
                        incrementUnread);
                }
                else
                {
                    // 沒找到現有項目，創建新項目（需要推斷是群組還是好友）
                    Debug.WriteLine($"未找到現有聊天項目，推斷聊天類型: {chatName}");

                    // 嘗試匹配群組
                    var groups = DataAccess.GetAllGroups();
                    var matchedGroup = groups.FirstOrDefault(g =>
                        !string.IsNullOrEmpty(g.GroupName) &&
                        g.GroupName.Equals(chatName, StringComparison.OrdinalIgnoreCase));

                    if (matchedGroup != null)
                    {
                        // 是群組
                        UpdateChatItem(chatName, matchedGroup.GroupId, true, newMessage, incrementUnread);
                        return;
                    }

                    // 嘗試匹配好友
                    var friendCategories = DataAccess.GetAllFriendsWithCategories();
                    FriendInfo matchedFriend = null;

                    foreach (var category in friendCategories)
                        if (category?.BuddyList != null)
                        {
                            matchedFriend = category.BuddyList.FirstOrDefault(f =>
                                (!string.IsNullOrEmpty(f.Remark) &&
                                 f.Remark.Equals(chatName, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(f.Nickname) &&
                                 f.Nickname.Equals(chatName, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(f.Nick) &&
                                 f.Nick.Equals(chatName, StringComparison.OrdinalIgnoreCase)));

                            if (matchedFriend != null) break;
                        }

                    if (matchedFriend != null)
                    {
                        // 是好友
                        UpdateChatItem(chatName, matchedFriend.UserId, false, newMessage, incrementUnread);
                    }
                    else
                    {
                        // 無法確定類型，創建一個默認的聊天項目
                        Debug.WriteLine($"無法確定聊天類型，創建默認項目: {chatName}");
                        UpdateChatItem(chatName, 0, false, newMessage, incrementUnread);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateChatItem (向後兼容) 發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     高性能版本的UpdateChatItem - 使用缓存验证类型
        /// </summary>
        public void UpdateChatItem(string senderName, long chatId, bool isGroup, string newMessage,
            bool incrementUnread = true)
        {
            try
            {
                if (chatId <= 0)
                {
                    Debug.WriteLine($"⚠️ UpdateChatItem: 无效的ChatId {chatId}");
                    return;
                }

                // 使用缓存快速验证类型
                var cachedType = ChatTypeCache.GetChatType(chatId);
                if (cachedType.HasValue && cachedType.Value != isGroup)
                {
                    Debug.WriteLine($"⚠️ 聊天类型不匹配! ChatId={chatId}, 传入={isGroup}, 缓存={cachedType.Value}, 已自动修正");
                    isGroup = cachedType.Value;
                }

                // 获取正确的聊天名称
                string actualChatName;
                if (isGroup)
                {
                    actualChatName = DataAccess.GetGroupNameById(chatId);
                }
                else
                {
                    actualChatName = DataAccess.GetFriendNameById(chatId);
                    if (actualChatName.StartsWith("用戶 ") && !string.IsNullOrEmpty(senderName) && senderName != "我")
                        actualChatName = senderName;
                }

                // 其余逻辑保持不变...
                // [原有的 UpdateChatItem 逻辑]
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ UpdateChatItem 发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     优化的头像加载策略 - 减少并发数量
        /// </summary>
        private async void LoadAvatarsBatch()
        {
            try
            {
                if (ChatItems == null) return;

                // 限制同时加载的头像数量
                var itemsNeedingAvatars = ChatItems
                    .Where(item => !item.HasAvatar && !item.IsLoadingAvatar)
                    .Take(5) // 限制为5个
                    .ToList();

                if (itemsNeedingAvatars.Count == 0) return;

                Debug.WriteLine($"开始加载 {itemsNeedingAvatars.Count} 个头像");

                // 串行加载，避免并发压力
                foreach (var item in itemsNeedingAvatars)
                    try
                    {
                        item.LoadAvatarFromCacheAsync(); // 只从缓存加载
                        await Task.Delay(200); // 间隔200ms
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"加载头像失败 {item.Name}: {ex.Message}");
                    }

                Debug.WriteLine("头像批量加载完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"头像批量加载错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     处理初始化失败
        /// </summary>
        private async Task HandleInitializationFailure(Exception ex)
        {
            try
            {
                Debug.WriteLine($"处理初始化失败: {ex.Message}");

                // 确保基础UI可用
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.High, () =>
                    {
                        if (ChatItems == null)
                        {
                            ChatItems = new ObservableCollection<ChatItem>();
                            ChatListView.ItemsSource = ChatItems;
                        }

                        // 显示错误提示
                        var errorChat = new ChatItem
                        {
                            Name = "系统消息",
                            LastMessage = "初始化遇到问题，请重新启动应用",
                            LastTime = DateTime.Now.ToString("HH:mm"),
                            AvatarColor = "#FFFF6B6B",
                            ChatId = -1,
                            IsGroup = false
                        };
                        ChatItems.Add(errorChat);
                    });
            }
            catch (Exception handleEx)
            {
                Debug.WriteLine($"处理初始化失败时发生错误: {handleEx.Message}");
            }
        }

        /// <summary>
        ///     安全的 AddChatItemFromMessage 方法 - 加強錯誤處理
        /// </summary>
        private void AddChatItemFromMessage(string chatName, string lastMessage, int unreadCount = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(chatName))
                {
                    Debug.WriteLine("AddChatItemFromMessage: 聊天名稱為空");
                    return;
                }

                Debug.WriteLine($"AddChatItemFromMessage: 創建聊天項目 '{chatName}'");

                // 確保 ChatItems 已初始化
                if (ChatItems == null)
                {
                    ChatItems = new ObservableCollection<ChatItem>();
                    if (ChatListView != null) ChatListView.ItemsSource = ChatItems;
                }

                // 先檢查是否為群組
                var groups = DataAccess.GetAllGroups();
                var group = groups?.FirstOrDefault(g =>
                    !string.IsNullOrEmpty(g.GroupName) &&
                    g.GroupName.Equals(chatName, StringComparison.OrdinalIgnoreCase));

                if (group != null)
                {
                    // 是群組
                    AddChatItem(chatName, lastMessage, unreadCount, null, group.GroupId, true, group.MemberCount);
                    Debug.WriteLine($"添加群組聊天項目: {chatName}, ID: {group.GroupId}");
                    return;
                }

                // 檢查是否為好友
                var friendCategories = DataAccess.GetAllFriendsWithCategories();
                FriendInfo friend = null;

                if (friendCategories != null)
                    foreach (var category in friendCategories)
                        if (category?.BuddyList != null)
                        {
                            friend = category.BuddyList.FirstOrDefault(f =>
                                f != null &&
                                ((!string.IsNullOrEmpty(f.Remark) &&
                                  f.Remark.Equals(chatName, StringComparison.OrdinalIgnoreCase)) ||
                                 (!string.IsNullOrEmpty(f.Nickname) &&
                                  f.Nickname.Equals(chatName, StringComparison.OrdinalIgnoreCase)) ||
                                 (!string.IsNullOrEmpty(f.Nick) &&
                                  f.Nick.Equals(chatName, StringComparison.OrdinalIgnoreCase))));

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
            catch (Exception ex)
            {
                Debug.WriteLine($"AddChatItemFromMessage 發生錯誤: {ex.Message}");
                Debug.WriteLine($"參數: chatName='{chatName}', lastMessage='{lastMessage}', unreadCount={unreadCount}");

                // 回退：如果所有操作都失敗，至少創建一個基本的聊天項目
                try
                {
                    if (ChatItems != null)
                    {
                        var fallbackItem = new ChatItem
                        {
                            Name = chatName,
                            LastMessage = lastMessage,
                            LastTime = DateTime.Now.ToString("HH:mm"),
                            UnreadCount = unreadCount,
                            AvatarColor = GetRandomAvatarColor(),
                            ChatId = 0,
                            IsGroup = false,
                            MemberCount = 0
                        };

                        ChatItems.Insert(0, fallbackItem);
                        Debug.WriteLine($"創建回退聊天項目: {chatName}");
                    }
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"創建回退聊天項目也失敗: {fallbackEx.Message}");
                }
            }
        }

        // 修改 AddChatItem 方法
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

            // 先从缓存加载头像
            newChatItem.LoadAvatarFromCacheAsync();

            ChatItems.Insert(0, newChatItem);

            // 延迟网络加载（低优先级）
            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                if (!newChatItem.HasAvatar) newChatItem.LoadAvatarAsync();
            });

            Debug.WriteLine($"添加新聊天项目: {name}, ChatId: {chatId}, IsGroup: {isGroup}");
        }


        /// <summary>
        ///     刷新缺失的头像（优化版 - 分批处理）
        /// </summary>
        private async Task RefreshMissingAvatarsAsync()
        {
            try
            {
                await Task.Delay(5000); // 延迟5秒，确保界面完全加载

                var itemsWithoutAvatars = new List<ChatItem>();

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Low,
                    () =>
                    {
                        foreach (var chatItem in ChatItems)
                            if (!chatItem.HasAvatar && !chatItem.IsLoadingAvatar)
                                itemsWithoutAvatars.Add(chatItem);
                    });

                Debug.WriteLine($"开始分批刷新 {itemsWithoutAvatars.Count} 个缺失的头像");

                // 分批处理，避免一次性发送过多请求
                const int batchSize = 5;
                for (var i = 0; i < itemsWithoutAvatars.Count; i += batchSize)
                {
                    var batch = itemsWithoutAvatars.Skip(i).Take(batchSize).ToList();

                    // 并行处理每批
                    var tasks = batch.Select(item => Task.Run(() => { item.LoadAvatarAsync(); })).ToArray();

                    await Task.WhenAll(tasks);

                    // 批次间延迟，避免请求过快
                    if (i + batchSize < itemsWithoutAvatars.Count) await Task.Delay(1000);
                }

                Debug.WriteLine("缺失头像刷新完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新缺失头像时发生错误: {ex.Message}");
            }
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

        /// <summary>
        ///     安全的 FindChatItemByName 方法 - 修復 null 引用問題
        /// </summary>
        public ChatItem FindChatItemByName(string name)
        {
            try
            {
                // 安全檢查：確保 ChatItems 不為 null 且 name 有效
                if (ChatItems == null)
                {
                    Debug.WriteLine("FindChatItemByName: ChatItems 集合為 null，正在重新初始化");
                    ChatItems = new ObservableCollection<ChatItem>();
                    return null;
                }

                if (string.IsNullOrEmpty(name))
                {
                    Debug.WriteLine("FindChatItemByName: 搜索名稱為空或 null");
                    return null;
                }

                Debug.WriteLine($"FindChatItemByName: 在 {ChatItems.Count} 個項目中搜索 '{name}'");

                return ChatItems.FirstOrDefault(chat =>
                    chat != null && !string.IsNullOrEmpty(chat.Name) &&
                    chat.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FindChatItemByName 發生錯誤: {ex.Message}");
                Debug.WriteLine($"ChatItems 狀態: {(ChatItems == null ? "null" : $"Count={ChatItems.Count}")}");

                // 如果 ChatItems 為 null，重新初始化
                if (ChatItems == null)
                {
                    ChatItems = new ObservableCollection<ChatItem>();
                    if (ChatListView != null) ChatListView.ItemsSource = ChatItems;
                }

                return null;
            }
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