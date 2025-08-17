using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;
using MvvmCross.Platform.Core;
using Newtonsoft.Json.Linq; // 新增用於解析狀態

namespace AnnaMessager.Core.ViewModels
{
    public class ContactGroup : MvxNotifyPropertyChanged
    {
        public string CategoryName { get; set; }
        public ObservableCollection<ContactItem> Items { get; set; } = new ObservableCollection<ContactItem>();
        private bool _isExpanded;
        private int _totalCount;
        private int _loadedCount;
        public bool IsExpanded { get => _isExpanded; set { if (SetProperty(ref _isExpanded, value)) { RaisePropertyChanged(() => Display); RaisePropertyChanged(() => LoadedCountText); RaisePropertyChanged(() => IsLoading); } } }
        public int TotalCount { get => _totalCount; set { if (SetProperty(ref _totalCount, value)) { RaisePropertyChanged(() => Display); RaisePropertyChanged(() => LoadedCountText); RaisePropertyChanged(() => IsLoading); } } }
        public int LoadedCount { get => _loadedCount; set { if (SetProperty(ref _loadedCount, value)) { RaisePropertyChanged(() => Display); RaisePropertyChanged(() => LoadedCountText); RaisePropertyChanged(() => IsLoading); } } }
        // 展開時顯示 已載入/總數；未展開只顯示總數
        public string Display => TotalCount > 0 ? $"{CategoryName} {TotalCount}" : CategoryName;
        public string LoadedCountText => IsExpanded && TotalCount > 0 ? $"{LoadedCount}/{TotalCount}" : (TotalCount > 0 ? TotalCount.ToString() : "0");
        public bool IsLoading => IsExpanded && LoadedCount < TotalCount;
    }

    // 狀態緩存資料結構 (避免使用 C# 7 ValueTuple 以兼容現有設定)
    internal class StatusCacheEntry
    {
        public DateTime Timestamp { get; set; }
        public UserStatus Status { get; set; }
        public string Text { get; set; }
    }

    public class ContactsViewModel : MvxViewModel
    {
        private readonly IOneBotService _oneBotService;
        private readonly ICacheManager _cacheManager;
        private readonly IPlatformDatabaseService _db; // 用於保存分類及聯絡人
        private ObservableCollection<ContactItem> _contacts;
        private bool _isRefreshing;
        private string _searchText;
        private ObservableCollection<ContactGroup> _groupedContacts = new ObservableCollection<ContactGroup>();
        private readonly ObservableCollection<FriendCategoryMeta> _categories = new ObservableCollection<FriendCategoryMeta>();
        private List<FriendCategoryItem> _rawCategories = new List<FriendCategoryItem>(); // 保存 API 原始分類與好友資料供延遲載入
        private readonly Dictionary<long, StatusCacheEntry> _statusCache = new Dictionary<long, StatusCacheEntry>();
        private readonly TimeSpan _statusCacheDuration = TimeSpan.FromMinutes(5);

        public ObservableCollection<FriendCategoryMeta> Categories => _categories;

        public ContactsViewModel(IOneBotService oneBotService)
        {
            _oneBotService = oneBotService;
            Contacts = new ObservableCollection<ContactItem>();
            OpenChatCommand = new MvxCommand<ContactItem>(OpenChat);
            RefreshCommand = new MvxCommand(async () => await RefreshAsync());
            SearchCommand = new MvxCommand<string>(OnSearch);
            _cacheManager = Mvx.Resolve<ICacheManager>();
            try { _db = Mvx.Resolve<IPlatformDatabaseService>(); } catch { }
            Debug.WriteLine("[ContactsViewModel] 建構子執行");
        }

        public ObservableCollection<ContactItem> Contacts
        {
            get => _contacts;
            set
            {
                SetProperty(ref _contacts, value);
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

        public ObservableCollection<ContactGroup> GroupedContacts
        {
            get => _groupedContacts;
            set => SetProperty(ref _groupedContacts, value);
        }

        // UI 顯示屬性
        public bool IsEmpty => !IsRefreshing && (Contacts == null || Contacts.Count == 0);

        public ICommand OpenChatCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }

        public override async Task Initialize()
        {
            await base.Initialize();
            Debug.WriteLine("初始化 ContactsViewModel");
            await LoadCachedContactsAsync();
            await LoadContactsAsync();
        }

        private async Task LoadCachedContactsAsync()
        {
            try
            {
                var cached = await _cacheManager.LoadCachedContactsAsync();
                if (cached != null && cached.Count > 0)
                {
                    Contacts.Clear();
                    foreach (var c in cached) Contacts.Add(c);
                    BuildGroupsOnlyFromContacts();
                    RaisePropertyChanged(() => IsEmpty);
                    Debug.WriteLine($"[ContactsViewModel] 已從緩存載入聯絡人 {cached.Count} 筆");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聯絡人緩存失敗: {ex.Message}");
            }
        }

        private async Task LoadContactsAsync()
        {
            try
            {
                IsRefreshing = true;
                var cats = await _oneBotService.GetFriendsWithCategoryAsync();
                if (cats?.Status == "ok" && cats.Data != null)
                {
                    _rawCategories = cats.Data.ToList(); // 保存原始資料供懶載入
                    Contacts.Clear();
                    Categories.Clear();
                    GroupedContacts.Clear();

                    var orderedCats = _rawCategories
                        .OrderBy(c => c.FinalSortOrder)
                        .ThenBy(c => c.CategoryName)
                        .ToList();

                    foreach (var cat in orderedCats)
                    {
                        var meta = new FriendCategoryMeta
                        {
                            CategoryId = cat.CategoryId,
                            CategoryName = string.IsNullOrEmpty(cat.CategoryName) ? "未分類" : cat.CategoryName,
                            RawSort = cat.SortOrderRaw,
                            TotalCount = cat.Friends?.Count ?? 0,
                            IsExpanded = metaDefaultExpanded(cat.CategoryName)
                        };
                        Categories.Add(meta);
                        GroupedContacts.Add(new ContactGroup
                        {
                            CategoryName = meta.CategoryName,
                            TotalCount = meta.TotalCount,
                            LoadedCount = 0,
                            IsExpanded = meta.IsExpanded
                        });

                        // 保存分類資訊 (異步)
                        if (_db != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _db.SaveCategoryCacheAsync(new CategoryCacheEntity
                                    {
                                        CategoryId = meta.CategoryId,
                                        CategoryName = meta.CategoryName,
                                        SortOrder = meta.RawSort,
                                        TotalCount = meta.TotalCount,
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    });
                                }
                                catch (Exception dbex) { Debug.WriteLine($"保存分類緩存失敗: {dbex.Message}"); }
                            });
                        }
                    }

                    // 初次僅載入預設展開的分類 (例如 特别关心)
                    foreach (var m in Categories.Where(c => c.IsExpanded))
                    {
                        _ = LoadCategoryAsync(m); // 背景載入
                    }
                }
                else
                {
                    Debug.WriteLine("[ContactsViewModel] GetFriendsWithCategoryAsync 無有效資料");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聯絡人失敗: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
                RaisePropertyChanged(() => IsEmpty);
            }
        }

        private async Task LoadCategoryAsync(FriendCategoryMeta meta)
        {
            if (meta == null) return;
            if (meta.LoadedCount >= meta.TotalCount && meta.TotalCount != 0) return; // 已載入完成
            try
            {
                var cat = _rawCategories.FirstOrDefault(c => c.CategoryId == meta.CategoryId);
                if (cat == null)
                {
                    Debug.WriteLine($"[ContactsViewModel] 找不到分類資料 (CategoryId={meta.CategoryId})");
                    return;
                }
                var friends = cat.Friends ?? new List<FriendInfo>();
                int batch = 100;
                for (int i = meta.LoadedCount; i < friends.Count; i += batch)
                {
                    var slice = friends.Skip(i).Take(batch).ToList();
                    var sliceContacts = slice.Select(f => new ContactItem
                    {
                        UserId = f.UserId,
                        Nickname = f.Nickname,
                        Remark = f.Remark,
                        CategoryName = meta.CategoryName,
                        CategoryId = (int)meta.CategoryId,
                        IsOnline = false,
                        Status = UserStatus.Offline,
                        AvatarUrl = $"https://q1.qlogo.cn/g?b=qq&nk={f.UserId}&s=640"
                    }).ToList();

                    IMvxMainThreadDispatcher dispatcher = null; try { dispatcher = Mvx.Resolve<IMvxMainThreadDispatcher>(); } catch { }
                    var addAction = new Action(() =>
                    {
                        foreach (var c in sliceContacts)
                        {
                            if (string.IsNullOrEmpty(c.Nickname)) c.Nickname = c.UserId.ToString();
                            if (!Contacts.Any(x => x.UserId == c.UserId))
                            {
                                Contacts.Add(c);
                                _ = _cacheManager.CacheContactItemAsync(c); // 異步緩存
                                _ = LoadContactStatusAsync(c); // 懶載入在線狀態
                            }
                        }
                        meta.LoadedCount = Math.Min(meta.TotalCount, meta.LoadedCount + sliceContacts.Count);
                        UpdateGroup(meta.CategoryName, meta);
                        RaisePropertyChanged(() => IsEmpty);
                    });
                    if (dispatcher != null) dispatcher.RequestMainThreadAction(addAction); else addAction();
                    await Task.Delay(60);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"分類好友載入失敗: {ex.Message}");
            }
        }

        private async Task LoadContactStatusAsync(ContactItem contact)
        {
            if (contact == null) return;
            try
            {
                StatusCacheEntry cached;
                if (_statusCache.TryGetValue(contact.UserId, out cached))
                {
                    if (DateTime.Now - cached.Timestamp < _statusCacheDuration)
                    {
                        ApplyStatus(contact, cached.Status, cached.Text);
                        return;
                    }
                }

                var resp = await _oneBotService.GetUserStatusAsync(contact.UserId);
                if (resp != null && resp.Status == "ok" && resp.Data != null)
                {
                    var obj = resp.Data as JObject ?? JObject.FromObject(resp.Data);
                    var statusCode = obj.Value<int?>("status") ?? 0;
                    var extStatus = obj.Value<int?>("ext_status") ?? 0;
                    UserStatus userStatus; string text; TranslateStatus(statusCode, extStatus, out userStatus, out text);
                    ApplyStatus(contact, userStatus, text);
                    _statusCache[contact.UserId] = new StatusCacheEntry { Timestamp = DateTime.Now, Status = userStatus, Text = text };
                }
                else
                {
                    if (string.IsNullOrEmpty(contact.OnlineStatusText)) contact.OnlineStatusText = contact.IsOnline ? "在線" : "";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("取得用戶狀態失敗 " + contact.UserId + ": " + ex.Message);
            }
        }

        private void ApplyStatus(ContactItem contact, UserStatus status, string text)
        {
            contact.Status = status;
            contact.IsOnline = status != UserStatus.Offline;
            contact.OnlineStatusText = text;
        }

        private void TranslateStatus(int statusCode, int extStatus, out UserStatus status, out string text)
        {
            switch (statusCode)
            {
                case 0: status = UserStatus.Offline; text = "離線"; return;
                case 10: status = UserStatus.Online; text = "在線"; return;
                case 30: status = UserStatus.Away; text = "離開"; return;
                case 40: status = UserStatus.Busy; text = "忙碌"; return;
                case 50: status = UserStatus.Online; text = "隱身"; return;
                case 60: status = UserStatus.Online; text = "在線"; return;
                case 70: status = UserStatus.Online; text = "在線"; return;
                default:
                    status = statusCode == 0 ? UserStatus.Offline : UserStatus.Online;
                    text = status == UserStatus.Offline ? "離線" : "在線";
                    return;
            }
        }

        private string TranslateStatus(string code)
        {
            switch (code == null ? null : code.ToLower())
            {
                case "online": return "在線";
                case "busy": return "忙碌";
                case "away": return "離開";
                case "offline": return "離線";
                default: return code;
            }
        }

        private void UpdateGroup(string categoryName, FriendCategoryMeta meta)
        {
            var group = GroupedContacts.FirstOrDefault(g => g.CategoryName == categoryName);
            if (group == null)
            {
                group = new ContactGroup { CategoryName = categoryName };
                GroupedContacts.Add(group);
            }
            group.TotalCount = meta.TotalCount;
            group.LoadedCount = meta.LoadedCount;
            group.IsExpanded = meta.IsExpanded;
            if (meta.IsExpanded)
            {
                var snapshot = Contacts.Where(c => (c.CategoryName ?? "未分類") == categoryName)
                                        .OrderByDescending(x => x.IsOnline)
                                        .ThenBy(x => x.DisplayName)
                                        .ToList();
                group.Items.Clear();
                foreach (var item in snapshot) group.Items.Add(item);
            }
            else
            {
                group.Items.Clear();
            }
            RaisePropertyChanged(() => GroupedContacts);
        }

        private void BuildGroupsOnlyFromContacts()
        {
            GroupedContacts.Clear();
            foreach (var grp in Contacts.GroupBy(c => c.CategoryName ?? "未分類"))
            {
                var group = new ContactGroup
                {
                    CategoryName = grp.Key,
                    TotalCount = grp.Count(),
                    LoadedCount = grp.Count(),
                    IsExpanded = true
                };
                foreach (var item in grp.OrderByDescending(x => x.IsOnline).ThenBy(x => x.DisplayName)) group.Items.Add(item);
                GroupedContacts.Add(group);
            }
        }

        private void OnSearch(string searchText)
        {
            try
            {
                SearchText = searchText;
                // 清除先前分段
                foreach (var c in Contacts)
                {
                    c.NamePrefix = c.NameMatch = c.NameSuffix = null;
                }

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // 還原目前展開的分類內容
                    GroupedContacts.Clear();
                    foreach (var meta in Categories)
                    {
                        var group = new ContactGroup
                        {
                            CategoryName = meta.CategoryName,
                            TotalCount = meta.TotalCount,
                            LoadedCount = meta.LoadedCount,
                            IsExpanded = meta.IsExpanded
                        };
                        if (meta.IsExpanded)
                        {
                            var snapshot = Contacts.Where(c => (c.CategoryName ?? "未分類") == meta.CategoryName)
                                                    .OrderByDescending(c => c.IsOnline)
                                                    .ThenBy(c => c.DisplayName).ToList();
                            foreach (var item in snapshot) group.Items.Add(item);
                        }
                        GroupedContacts.Add(group);
                    }
                    RaisePropertyChanged(() => GroupedContacts);
                    return;
                }

                var keyword = searchText.Trim();
                var filtered = Contacts.Where(c =>
                    (!string.IsNullOrEmpty(c.DisplayName) && c.DisplayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c.Nickname) && c.Nickname.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c.Remark) && c.Remark.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderByDescending(c => c.IsOnline)
                    .ThenBy(c => c.DisplayName)
                    .ToList();

                // 分段
                foreach (var item in filtered)
                {
                    var name = item.DisplayName ?? "";
                    var idx = name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        item.NamePrefix = name.Substring(0, idx);
                        item.NameMatch = name.Substring(idx, keyword.Length);
                        item.NameSuffix = idx + keyword.Length < name.Length ? name.Substring(idx + keyword.Length) : "";
                    }
                }

                GroupedContacts.Clear();
                var resultGroup = new ContactGroup
                {
                    CategoryName = "搜尋結果",
                    TotalCount = filtered.Count,
                    LoadedCount = filtered.Count,
                    IsExpanded = true
                };
                foreach (var item in filtered) resultGroup.Items.Add(item);
                GroupedContacts.Add(resultGroup);
                RaisePropertyChanged(() => GroupedContacts);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("搜尋聯絡人失敗: " + ex.Message);
            }
        }

        private async Task RefreshAsync() => await LoadContactsAsync();
        private void OpenChat(ContactItem contact)
        {
            if (contact == null) return;
            try
            {
                ShowViewModel<ChatViewModel>(new { chatId = contact.UserId, isGroup = false, chatName = contact.DisplayName });
            }
            catch (Exception navEx) { Debug.WriteLine("導航聊天視圖失敗: " + navEx.Message); }
            try
            {
                MainViewModel mainViewModel; if (MvvmCross.Platform.Mvx.TryResolve(out mainViewModel))
                    mainViewModel?.ChatListViewModel.AddOrUpdateChat(contact.UserId, false, contact.DisplayName);
            }
            catch (Exception ex) { Debug.WriteLine("添加到聊天列表失敗: " + ex.Message); }
        }
        public void ToggleCategory(string categoryName, bool? expandExplicit = null)
        {
            var meta = Categories.FirstOrDefault(c => c.CategoryName == categoryName);
            if (meta == null) return;
            meta.IsExpanded = expandExplicit ?? !meta.IsExpanded;
            UpdateGroup(meta.CategoryName, meta);
            if (meta.IsExpanded && meta.LoadedCount == 0)
            {
                _ = LoadCategoryAsync(meta); // 懶載入
            }
        }

        private bool metaDefaultExpanded(string name) => name == "特别关心"; // 預設展開特別關心

        public class FriendCategoryMeta : MvxNotifyPropertyChanged
        {
            public long CategoryId { get; set; }
            public string CategoryName { get; set; }
            public int RawSort { get; set; }
            public int TotalCount { get; set; }
            private int _loadedCount;
            private bool _isExpanded;
            public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
            public int LoadedCount { get => _loadedCount; set => SetProperty(ref _loadedCount, value); }
            public string Display => TotalCount > 0 ? $"{CategoryName} {TotalCount}" : CategoryName;
            public string LoadedProgress => TotalCount > 0 ? $"{LoadedCount}/{TotalCount}" : "";
        }
    }
}