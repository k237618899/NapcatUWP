using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        }

        public ObservableCollection<GroupItem> Groups
        {
            get => _groups;
            set => SetProperty(ref _groups, value);
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

        public ICommand OpenChatCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }

        public override async Task Initialize()
        {
            await base.Initialize();
            await LoadGroupsAsync();
        }

        private async Task LoadGroupsAsync()
        {
            try
            {
                IsRefreshing = true;

                var groups = await _oneBotService.GetGroupListAsync();
                if (groups?.Status == "ok" && groups.Data != null)
                {
                    Groups.Clear();
                    foreach (var group in groups.Data)
                        Groups.Add(new GroupItem
                        {
                            GroupId = group.GroupId,
                            GroupName = group.GroupName,
                            MemberCount = group.MemberCount
                        });
                }
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
            var mainViewModel = Mvx.Resolve<MainViewModel>();
            mainViewModel?.ChatListViewModel.AddOrUpdateChat(group.GroupId, true, group.GroupName);
        }

        private async Task RefreshAsync()
        {
            await LoadGroupsAsync();
        }

        private void OnSearch(string searchText)
        {
            SearchText = searchText;
            // TODO: 實作搜尋功能
        }
    }
}