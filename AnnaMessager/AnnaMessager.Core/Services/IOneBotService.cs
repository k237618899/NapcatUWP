using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    public interface IOneBotService
    {
        event EventHandler<MessageEventArgs> MessageReceived;
        event EventHandler<NoticeEventArgs> NoticeReceived;
        event EventHandler<RequestEventArgs> RequestReceived;
        event EventHandler<MetaEventArgs> MetaEventReceived;
        event EventHandler<bool> ConnectionStatusChanged;
        bool IsConnected { get; }
        string CurrentServerUrl { get; }
        Task<bool> ConnectAsync(string serverUrl, string token);
        Task DisconnectAsync();
        Task<OneBotResponse<object>> SendPrivateMsgAsync(long userId, string message, bool autoEscape = false);
        Task<OneBotResponse<object>> SendGroupMsgAsync(long groupId, string message, bool autoEscape = false);
        Task<OneBotResponse<object>> SendMessageAsync(long chatId, string message, bool isGroup = false, bool autoEscape = false);
        Task<OneBotResponse<object>> DeleteMessageAsync(long messageId);
        Task<OneBotResponse<object>> GetMessageAsync(long messageId);
        Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup = false); // 新增: 統一聊天歷史 API
        Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count); // 可選帶數量
        Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count, int startSeq); // 可選帶游標
        Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count, int startSeq, int direction);
        Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count, int startSeq, int direction, bool includeSelf);
        Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup, int count, int startSeq, int direction, bool includeSelf, bool forceRemote);
        Task<OneBotResponse<List<FriendCategoryItem>>> GetFriendsWithCategoryAsync();
        Task<OneBotResponse<List<RecentContact>>> GetRecentContactAsync(int count = 30);
        Task<OneBotResponse<LoginInfoData>> GetLoginInfoAsync();
        Task<OneBotResponse<StrangerInfo>> GetStrangerInfoAsync(long userId, bool noCache = false);
        Task<OneBotResponse<List<FriendInfo>>> GetFriendListAsync();
        Task<OneBotResponse<List<GroupInfo>>> GetGroupListAsync();
        Task<OneBotResponse<GroupInfo>> GetGroupInfoAsync(long groupId, bool noCache = false);
        Task<OneBotResponse<List<GroupMemberInfo>>> GetGroupMemberListAsync(long groupId);
        Task<OneBotResponse<GroupMemberInfo>> GetGroupMemberInfoAsync(long groupId, long userId, bool noCache = false);
        Task<OneBotResponse<object>> SendPrivateMessageAsync(long userId, string message, bool autoEscape = false);
        Task<OneBotResponse<object>> SendGroupMessageAsync(long groupId, string message, bool autoEscape = false);
        Task<OneBotResponse<MessageHistoryResponse>> GetFriendMsgHistoryAsync(long userId, int messageSeq = 0, int count = 20);
        Task<OneBotResponse<MessageHistoryResponse>> GetGroupMsgHistoryAsync(long groupId, int messageSeq = 0, int count = 20);
        Task<List<MessageHistoryItem>> TryGetRecentMessagesAsync(long id, bool isGroup, int limit); // 新增: 近期消息便捷獲取
        Task<OneBotResponse<object>> GetVersionInfoAsync();
        Task<OneBotResponse<object>> SetRestartAsync(int delay = 0);
        Task<OneBotResponse<object>> CleanCacheAsync();
        Task<OneBotResponse<object>> GetUserStatusAsync(long userId); // NapCat 擴展: nc_get_user_status
    }
}