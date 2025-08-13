using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     搜索和過濾服務接口
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        ///     搜索聊天列表
        /// </summary>
        Task<List<ChatItem>> SearchChatsAsync(List<ChatItem> source, string searchText);

        /// <summary>
        ///     搜索聯絡人列表
        /// </summary>
        Task<List<ContactItem>> SearchContactsAsync(List<ContactItem> source, string searchText);

        /// <summary>
        ///     搜索群組列表
        /// </summary>
        Task<List<GroupItem>> SearchGroupsAsync(List<GroupItem> source, string searchText);

        /// <summary>
        ///     過濾聊天列表
        /// </summary>
        Task<List<ChatItem>> FilterChatsAsync(List<ChatItem> source, ChatFilter filter);
    }

    /// <summary>
    ///     聊天過濾條件
    /// </summary>
    public class ChatFilter
    {
        public bool? IsGroup { get; set; }
        public bool? IsPinned { get; set; }
        public bool? IsMuted { get; set; }
        public bool? HasUnreadMessages { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}