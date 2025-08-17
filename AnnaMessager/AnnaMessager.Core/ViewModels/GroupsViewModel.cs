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
    public class GroupsViewModel : MvxViewModel
    {
        private readonly IOneBotService _oneBotService;
        private readonly ICacheManager _cacheManager;
        private ObservableCollection<GroupItem> _groups;
        private bool _isRefreshing;
        private string _searchText;

        public GroupsViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;
            Groups = new ObservableCollection<GroupItem>();

            OpenChatCommand = new MvxCommand<GroupItem>(OpenChat);
            RefreshCommand = new MvxCommand(async () => await RefreshAsync());
            SearchCommand = new MvxCommand<string>(OnSearch);
            _cacheManager = Mvx.Resolve<ICacheManager>();

            Debug.WriteLine("[GroupsViewModel] 建構子執行");
        }

        public ObservableCollection<GroupItem> Groups
        {
            get => _groups;
            set
            {
                SetProperty(ref _groups, value);
                RaisePropertyChanged(() => IsEmpty);
            }
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

        // UI 顯示屬性
        public bool IsEmpty => !IsRefreshing && (Groups == null || Groups.Count == 0);

        public ICommand OpenChatCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }

        public override async Task Initialize()
        {
            await base.Initialize();
            Debug.WriteLine("[GroupsViewModel] Initialize 開始");
            await LoadCachedGroupsAsync();
            await LoadGroupsAsync();
            Debug.WriteLine("[GroupsViewModel] Initialize 結束, Groups.Count=" + (Groups?.Count ?? 0));
        }

        private async Task LoadCachedGroupsAsync()
        {
            try
            {
                var cached = await _cacheManager.LoadCachedGroupsAsync();
                if (cached != null && cached.Count > 0)
                {
                    Groups.Clear();
                    foreach (var g in cached) Groups.Add(g);
                    RaisePropertyChanged(() => IsEmpty);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入群組緩存失敗: {ex.Message}");
            }
        }

        private async Task LoadGroupsAsync()
        {
            try
            {
                IsRefreshing = true;

                // 先保留現有(緩存)列表，增量合併
                var existing = Groups.ToList().ToDictionary(g => g.GroupId, g => g);
                var groups = await _oneBotService.GetGroupListAsync();
                if (groups?.Status == "ok" && groups.Data != null)
                {
                    foreach (var group in groups.Data)
                    {
                        if (!existing.TryGetValue(group.GroupId, out var groupItem))
                        {
                            groupItem = new GroupItem
                            {
                                GroupId = group.GroupId,
                                GroupName = group.GroupName,
                                MemberCount = group.MemberCount,
                                IsOwner = false,
                                IsAdmin = false,
                                AvatarUrl = $"https://p.qlogo.cn/gh/{group.GroupId}/{group.GroupId}/640/"
                            };
                            Groups.Add(groupItem);
                        }
                        else
                        {
                            groupItem.GroupName = group.GroupName;
                            groupItem.MemberCount = group.MemberCount;
                        }
                        await _cacheManager.CacheGroupItemAsync(groupItem);
                    }
                }
                RaisePropertyChanged(() => IsEmpty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入群組失敗: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void OpenChat(GroupItem group)
        {
            if (group == null) return;

            ShowViewModel<ChatViewModel>(new
            {
                chatId = group.GroupId,
                isGroup = true,
                chatName = group.GroupName
            });

            // 通過 Mvx 解析主 ViewModel
            try
            {
                var mainViewModel = Mvx.Resolve<MainViewModel>();
                mainViewModel?.ChatListViewModel.AddOrUpdateChat(group.GroupId, true, group.GroupName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"添加到聊天列表失敗: {ex.Message}");
            }
        }

        private async Task RefreshAsync()
        {
            await LoadGroupsAsync();
        }

        private void OnSearch(string searchText)
        {
            SearchText = searchText;
            try
            {
                if (string.IsNullOrEmpty(searchText))
                {
                    Task.Run(async () => await LoadCachedGroupsAsync());
                    return;
                }
                // 目前簡單過濾: 重建列表
                Task.Run(async () =>
                {
                    var cached = await _cacheManager.LoadCachedGroupsAsync();
                    var filtered = cached.FindAll(g => (g.GroupName ?? "").Contains(searchText));
                    InvokeOnMainThread(() =>
                    {
                        Groups.Clear();
                        foreach (var g in filtered) Groups.Add(g);
                        RaisePropertyChanged(() => IsEmpty);
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"群組搜尋失敗: {ex.Message}");
            }
        }
    }
}