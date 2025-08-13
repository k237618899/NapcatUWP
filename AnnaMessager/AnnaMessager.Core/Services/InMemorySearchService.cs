using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     內存搜索服務實現 - 支持即時搜索，PCL 兼容
    /// </summary>
    public class InMemorySearchService : ISearchService
    {
        public async Task<List<ChatItem>> SearchChatsAsync(List<ChatItem> source, string searchText)
        {
            try
            {
                if (source == null || source.Count == 0)
                    return new List<ChatItem>();

                if (string.IsNullOrEmpty(searchText?.Trim()))
                    return new List<ChatItem>(source);

                // 使用 Task.Run 保持異步特性，但不阻塞 UI
                return await Task.Run(() =>
                {
                    var normalizedSearch = searchText.Trim().ToLowerInvariant();

                    return source.Where(chat =>
                            chat.Name?.ToLowerInvariant().Contains(normalizedSearch) == true ||
                            chat.LastMessage?.ToLowerInvariant().Contains(normalizedSearch) == true
                        ).OrderByDescending(c => c.IsPinned)
                        .ThenByDescending(c => c.LastTime)
                        .ToList();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索聊天列表失敗: {ex.Message}");
                return new List<ChatItem>();
            }
        }

        public async Task<List<ContactItem>> SearchContactsAsync(List<ContactItem> source, string searchText)
        {
            try
            {
                if (source == null || source.Count == 0)
                    return new List<ContactItem>();

                if (string.IsNullOrEmpty(searchText?.Trim()))
                    return new List<ContactItem>(source);

                return await Task.Run(() =>
                {
                    var normalizedSearch = searchText.Trim().ToLowerInvariant();

                    return source.Where(contact =>
                            contact.Nickname?.ToLowerInvariant().Contains(normalizedSearch) == true ||
                            contact.Remark?.ToLowerInvariant().Contains(normalizedSearch) == true ||
                            contact.DisplayName?.ToLowerInvariant().Contains(normalizedSearch) == true
                        ).OrderBy(c => c.DisplayName)
                        .ToList();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索聯絡人列表失敗: {ex.Message}");
                return new List<ContactItem>();
            }
        }

        public async Task<List<GroupItem>> SearchGroupsAsync(List<GroupItem> source, string searchText)
        {
            try
            {
                if (source == null || source.Count == 0)
                    return new List<GroupItem>();

                if (string.IsNullOrEmpty(searchText?.Trim()))
                    return new List<GroupItem>(source);

                return await Task.Run(() =>
                {
                    var normalizedSearch = searchText.Trim().ToLowerInvariant();

                    return source.Where(group =>
                            group.GroupName?.ToLowerInvariant().Contains(normalizedSearch) == true
                        ).OrderBy(g => g.GroupName)
                        .ToList();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索群組列表失敗: {ex.Message}");
                return new List<GroupItem>();
            }
        }

        public async Task<List<ChatItem>> FilterChatsAsync(List<ChatItem> source, ChatFilter filter)
        {
            try
            {
                if (source == null || source.Count == 0)
                    return new List<ChatItem>();

                if (filter == null)
                    return new List<ChatItem>(source);

                return await Task.Run(() =>
                {
                    var query = source.AsEnumerable();

                    if (filter.IsGroup.HasValue)
                        query = query.Where(c => c.IsGroup == filter.IsGroup.Value);

                    if (filter.IsPinned.HasValue)
                        query = query.Where(c => c.IsPinned == filter.IsPinned.Value);

                    if (filter.IsMuted.HasValue)
                        query = query.Where(c => c.IsMuted == filter.IsMuted.Value);

                    if (filter.HasUnreadMessages.HasValue)
                        query = query.Where(c => c.HasUnreadMessages == filter.HasUnreadMessages.Value);

                    if (filter.FromDate.HasValue)
                        query = query.Where(c => c.LastTime >= filter.FromDate.Value);

                    if (filter.ToDate.HasValue)
                        query = query.Where(c => c.LastTime <= filter.ToDate.Value);

                    return query.OrderByDescending(c => c.IsPinned)
                        .ThenByDescending(c => c.LastTime)
                        .ToList();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"過濾聊天列表失敗: {ex.Message}");
                return new List<ChatItem>();
            }
        }
    }
}