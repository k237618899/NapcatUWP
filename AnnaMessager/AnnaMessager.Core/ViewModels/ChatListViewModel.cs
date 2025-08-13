using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;

namespace AnnaMessager.Core.ViewModels
{
    public class ChatListViewModel : MvxViewModel
    {
        private readonly ICacheManager _cacheManager;
        private readonly INotificationService _notificationService;
        private readonly IOneBotService _oneBotService;
        private readonly ISearchService _searchService;
        private readonly ISettingsService _settingsService;
        private AppSettings _appSettings;
        private ObservableCollection<ChatItem> _chatList;
        private ObservableCollection<ChatItem> _filteredChatList;
        private bool _isRefreshing;
        private string _searchText;
        private bool _showOnlyPinned;
        private bool _showOnlyUnread;

        public ChatListViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;
            _cacheManager = Mvx.Resolve<ICacheManager>();
            _searchService = Mvx.Resolve<ISearchService>();
            _notificationService = Mvx.Resolve<INotificationService>();
            _settingsService = Mvx.Resolve<ISettingsService>();

            ChatList = new ObservableCollection<ChatItem>();
            FilteredChatList = new ObservableCollection<ChatItem>();

            OpenChatCommand = new MvxCommand<ChatItem>(OpenChat);
            RefreshCommand = new MvxCommand(async () => await RefreshAsync());
            SearchCommand = new MvxCommand<string>(async searchText => await OnSearchAsync(searchText));
            DeleteChatCommand = new MvxCommand<ChatItem>(async chatItem => await DeleteChatAsync(chatItem));
            PinChatCommand = new MvxCommand<ChatItem>(async chatItem => await TogglePinChatAsync(chatItem));
            MuteChatCommand = new MvxCommand<ChatItem>(async chatItem => await ToggleMuteChatAsync(chatItem));
            ToggleUnreadFilterCommand = new MvxCommand(async () => await ToggleUnreadFilterAsync());
            TogglePinnedFilterCommand = new MvxCommand(async () => await TogglePinnedFilterAsync());

            _oneBotService.MessageReceived += OnMessageReceived;
        }

        public ObservableCollection<ChatItem> ChatList
        {
            get => _chatList;
            set => SetProperty(ref _chatList, value);
        }

        public ObservableCollection<ChatItem> FilteredChatList
        {
            get => _filteredChatList;
            set => SetProperty(ref _filteredChatList, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool ShowOnlyUnread
        {
            get => _showOnlyUnread;
            set => SetProperty(ref _showOnlyUnread, value);
        }

        public bool ShowOnlyPinned
        {
            get => _showOnlyPinned;
            set => SetProperty(ref _showOnlyPinned, value);
        }

        public ICommand OpenChatCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand DeleteChatCommand { get; }
        public ICommand PinChatCommand { get; }
        public ICommand MuteChatCommand { get; }
        public ICommand ToggleUnreadFilterCommand { get; }
        public ICommand TogglePinnedFilterCommand { get; }

        public override async Task Initialize()
        {
            await base.Initialize();

            // 載入設定
            await LoadSettingsAsync();

            // 先載入緩存數據，再刷新遠程數據
            await LoadCachedDataAsync();
            await LoadChatsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                _appSettings = await _settingsService.LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入設定失敗: {ex.Message}");
                _appSettings = new AppSettings { EnableNotifications = true };
            }
        }

        private async Task LoadCachedDataAsync()
        {
            try
            {
                var cachedChats = await _cacheManager.LoadCachedChatsAsync();
                if (cachedChats?.Count > 0)
                {
                    ChatList.Clear();
                    foreach (var chat in cachedChats) ChatList.Add(chat);
                    await ApplyFiltersAsync();
                    Debug.WriteLine($"載入緩存聊天數據: {cachedChats.Count} 項");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入緩存聊天數據失敗: {ex.Message}");
            }
        }

        private async Task LoadChatsAsync()
        {
            try
            {
                IsRefreshing = true;

                // 載入好友列表
                var friends = await _oneBotService.GetFriendListAsync();
                if (friends?.Status == "ok" && friends.Data != null)
                    foreach (var friend in friends.Data)
                    {
                        var existingChat = ChatList.FirstOrDefault(c => c.ChatId == friend.UserId && !c.IsGroup);
                        if (existingChat == null)
                        {
                            var newChat = new ChatItem
                            {
                                ChatId = friend.UserId,
                                Name = !string.IsNullOrEmpty(friend.Remark) ? friend.Remark : friend.Nickname,
                                IsGroup = false,
                                LastTime = DateTime.Now.AddDays(-1),
                                LastMessage = "點擊開始聊天"
                            };

                            ChatList.Add(newChat);
                            // 緩存新項目
                            await _cacheManager.CacheChatItemAsync(newChat);
                        }
                        else
                        {
                            // 更新現有項目
                            existingChat.Name = !string.IsNullOrEmpty(friend.Remark) ? friend.Remark : friend.Nickname;
                            await _cacheManager.CacheChatItemAsync(existingChat);
                        }
                    }

                // 載入群組列表
                var groups = await _oneBotService.GetGroupListAsync();
                if (groups?.Status == "ok" && groups.Data != null)
                    foreach (var group in groups.Data)
                    {
                        var existingChat = ChatList.FirstOrDefault(c => c.ChatId == group.GroupId && c.IsGroup);
                        if (existingChat == null)
                        {
                            var newChat = new ChatItem
                            {
                                ChatId = group.GroupId,
                                Name = group.GroupName,
                                IsGroup = true,
                                LastTime = DateTime.Now.AddDays(-1),
                                LastMessage = "點擊開始聊天"
                            };

                            ChatList.Add(newChat);
                            // 緩存新項目
                            await _cacheManager.CacheChatItemAsync(newChat);
                        }
                        else
                        {
                            // 更新現有項目
                            existingChat.Name = group.GroupName;
                            await _cacheManager.CacheChatItemAsync(existingChat);
                        }
                    }

                // 批量緩存所有聊天項目
                await _cacheManager.CacheChatItemsAsync(ChatList.ToList());

                await ApplyFiltersAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聊天列表失敗: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task OnSearchAsync(string searchText)
        {
            SearchText = searchText;
            await ApplyFiltersAsync();
        }

        private async Task ApplyFiltersAsync()
        {
            try
            {
                var sourceList = ChatList.ToList();

                // 應用搜索
                if (!string.IsNullOrEmpty(SearchText))
                    sourceList = await _searchService.SearchChatsAsync(sourceList, SearchText);

                // 應用過濾條件
                var filter = new ChatFilter
                {
                    HasUnreadMessages = ShowOnlyUnread ? true : (bool?)null,
                    IsPinned = ShowOnlyPinned ? true : (bool?)null
                };

                if (ShowOnlyUnread || ShowOnlyPinned)
                    sourceList = await _searchService.FilterChatsAsync(sourceList, filter);

                // 更新顯示列表
                FilteredChatList.Clear();
                foreach (var item in sourceList) FilteredChatList.Add(item);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"應用過濾器失敗: {ex.Message}");
            }
        }

        private async Task ToggleUnreadFilterAsync()
        {
            ShowOnlyUnread = !ShowOnlyUnread;
            await ApplyFiltersAsync();
        }

        private async Task TogglePinnedFilterAsync()
        {
            ShowOnlyPinned = !ShowOnlyPinned;
            await ApplyFiltersAsync();
        }

        private void OpenChat(ChatItem chatItem)
        {
            if (chatItem == null) return;

            // 清除該聊天的通知
            Task.Run(async () =>
            {
                try
                {
                    await _notificationService.ClearChatNotificationsAsync(chatItem.ChatId, chatItem.IsGroup);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"清除聊天通知失敗: {ex.Message}");
                }
            });

            // 清除未讀數量
            chatItem.UnreadCount = 0;

            ShowViewModel<ChatViewModel>(new
            {
                chatId = chatItem.ChatId,
                isGroup = chatItem.IsGroup,
                chatName = chatItem.Name
            });
        }

        private async Task RefreshAsync()
        {
            await LoadChatsAsync();
        }

        private async Task DeleteChatAsync(ChatItem chatItem)
        {
            if (chatItem != null)
            {
                ChatList.Remove(chatItem);
                FilteredChatList.Remove(chatItem);
                await _cacheManager.DeleteChatCacheAsync(chatItem.ChatId, chatItem.IsGroup);
            }
        }

        private async Task TogglePinChatAsync(ChatItem chatItem)
        {
            if (chatItem != null)
            {
                chatItem.IsPinned = !chatItem.IsPinned;
                await _cacheManager.CacheChatItemAsync(chatItem);
                await ApplyFiltersAsync();
            }
        }

        private async Task ToggleMuteChatAsync(ChatItem chatItem)
        {
            if (chatItem != null)
            {
                chatItem.IsMuted = !chatItem.IsMuted;
                await _cacheManager.CacheChatItemAsync(chatItem);
            }
        }

        private async void OnMessageReceived(object sender, MessageEventArgs e)
        {
            if (e?.Message == null) return;

            try
            {
                var isGroup = e.Message.GroupId.HasValue;
                var chatId = isGroup ? e.Message.GroupId.Value : e.Message.UserId;
                var senderName = e.Message.Sender?.Nickname ?? "未知用戶";

                var existingChat = ChatList.FirstOrDefault(c => c.ChatId == chatId && c.IsGroup == isGroup);
                if (existingChat != null)
                {
                    existingChat.LastMessage = e.Message.Message;
                    existingChat.LastTime = e.Message.DateTime;
                    existingChat.UnreadCount++;

                    // 緩存更新的項目
                    await _cacheManager.CacheChatItemAsync(existingChat);

                    // 顯示通知（如果啟用且聊天未靜音）
                    await ShowMessageNotificationAsync(existingChat, e.Message, senderName);
                }
                else
                {
                    var newChat = new ChatItem
                    {
                        ChatId = chatId,
                        Name = isGroup ? "群聊" : senderName,
                        IsGroup = isGroup,
                        LastMessage = e.Message.Message,
                        LastTime = e.Message.DateTime,
                        UnreadCount = 1
                    };

                    ChatList.Insert(0, newChat);
                    await _cacheManager.CacheChatItemAsync(newChat);

                    // 顯示通知
                    await ShowMessageNotificationAsync(newChat, e.Message, senderName);
                }

                await ApplyFiltersAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理新消息時發生錯誤: {ex.Message}");
            }
        }

        private async Task ShowMessageNotificationAsync(ChatItem chatItem, MessageEvent message, string senderName)
        {
            try
            {
                // 檢查是否啟用通知
                if (_appSettings?.EnableNotifications != true)
                    return;

                // 檢查聊天是否被靜音
                if (chatItem.IsMuted)
                    return;

                // 創建通知信息
                var notificationInfo = new NotificationInfo
                {
                    Title = chatItem.IsGroup ? $"群聊: {chatItem.Name}" : chatItem.Name,
                    Message = message.Message,
                    SenderName = senderName,
                    ChatId = chatItem.ChatId,
                    IsGroup = chatItem.IsGroup,
                    Time = message.DateTime,
                    ChatName = chatItem.Name
                };

                // 顯示通知
                await _notificationService.ShowMessageNotificationAsync(notificationInfo);

                Debug.WriteLine($"已發送通知: {notificationInfo.Title} - {notificationInfo.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"顯示消息通知失敗: {ex.Message}");
            }
        }

        public void AddOrUpdateChat(long chatId, bool isGroup, string name)
        {
            var existingChat = ChatList.FirstOrDefault(c => c.ChatId == chatId && c.IsGroup == isGroup);
            if (existingChat == null)
            {
                var newChat = new ChatItem
                {
                    ChatId = chatId,
                    Name = name,
                    IsGroup = isGroup,
                    LastMessage = "點擊開始聊天",
                    LastTime = DateTime.Now
                };

                ChatList.Insert(0, newChat);

                // 異步緩存新項目
                Task.Run(async () => await _cacheManager.CacheChatItemAsync(newChat));
            }
        }
    }
}