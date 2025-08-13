using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    public interface IOneBotService
    {
        bool IsConnected { get; }

        // 事件定義 - 確保這些事件存在
        event EventHandler<MessageEventArgs> MessageReceived;
        event EventHandler<NoticeEventArgs> NoticeReceived;
        event EventHandler<RequestEventArgs> RequestReceived;
        event EventHandler<MetaEventArgs> MetaEventReceived;
        event EventHandler<bool> ConnectionStatusChanged;

        // 連接管理
        Task<bool> ConnectAsync(string serverUrl, string token);
        Task DisconnectAsync();

        // 消息 API - 添加缺少的方法
        Task<OneBotResponse<object>> SendPrivateMessageAsync(long userId, string message, bool autoEscape = false);
        Task<OneBotResponse<object>> SendGroupMessageAsync(long groupId, string message, bool autoEscape = false);

        // 添加 ChatViewModel 中使用的方法別名
        Task<OneBotResponse<object>> SendPrivateMsgAsync(long userId, string message, bool autoEscape = false);
        Task<OneBotResponse<object>> SendGroupMsgAsync(long groupId, string message, bool autoEscape = false);

        Task<OneBotResponse<object>> SendMessageAsync(long chatId, string message, bool isGroup = false,
            bool autoEscape = false);

        Task<OneBotResponse<object>> DeleteMessageAsync(long messageId);
        Task<OneBotResponse<object>> GetMessageAsync(long messageId);
        Task<OneBotResponse<MessageHistoryData>> GetForwardMessageAsync(long id);
        Task<OneBotResponse<object>> SendLikeAsync(long userId, int times = 1);

        // 群管理 API
        Task<OneBotResponse<object>> SetGroupKickAsync(long groupId, long userId, bool rejectAddRequest = false);
        Task<OneBotResponse<object>> SetGroupBanAsync(long groupId, long userId, int duration = 30 * 60);

        Task<OneBotResponse<object>> SetGroupAnonymousBanAsync(long groupId, AnonymousInfo anonymous,
            int duration = 30 * 60);

        Task<OneBotResponse<object>> SetGroupWholeBanAsync(long groupId, bool enable = true);
        Task<OneBotResponse<object>> SetGroupAdminAsync(long groupId, long userId, bool enable = true);
        Task<OneBotResponse<object>> SetGroupAnonymousAsync(long groupId, bool enable = true);
        Task<OneBotResponse<object>> SetGroupCardAsync(long groupId, long userId, string card);
        Task<OneBotResponse<object>> SetGroupNameAsync(long groupId, string groupName);
        Task<OneBotResponse<object>> SetGroupLeaveAsync(long groupId, bool isDismiss = false);

        Task<OneBotResponse<object>> SetGroupSpecialTitleAsync(long groupId, long userId, string specialTitle,
            int duration = -1);

        // 請求處理 API
        Task<OneBotResponse<object>> SetFriendAddRequestAsync(string flag, bool approve = true, string remark = "");

        Task<OneBotResponse<object>> SetGroupAddRequestAsync(string flag, string subType, bool approve = true,
            string reason = "");

        // 獲取信息 API
        Task<OneBotResponse<LoginInfo>> GetLoginInfoAsync();
        Task<OneBotResponse<StrangerInfo>> GetStrangerInfoAsync(long userId, bool noCache = false);
        Task<OneBotResponse<List<FriendInfo>>> GetFriendListAsync();
        Task<OneBotResponse<List<GroupInfo>>> GetGroupListAsync();
        Task<OneBotResponse<GroupInfo>> GetGroupInfoAsync(long groupId, bool noCache = false);
        Task<OneBotResponse<List<GroupMemberInfo>>> GetGroupMemberListAsync(long groupId);
        Task<OneBotResponse<GroupMemberInfo>> GetGroupMemberInfoAsync(long groupId, long userId, bool noCache = false);

        // 文件 API
        Task<OneBotResponse<FileInfo>> GetImageAsync(string file);
        Task<OneBotResponse<FileInfo>> GetRecordAsync(string file, string outFormat);
        Task<OneBotResponse<FileInfo>> GetFileAsync(string fileId);
        Task<OneBotResponse<object>> CanSendImageAsync();
        Task<OneBotResponse<object>> CanSendRecordAsync();

        // 歷史消息 API (NapCat 擴展)
        Task<OneBotResponse<MessageHistoryResponse>> GetFriendMsgHistoryAsync(long userId, int messageSeq = 0,
            int count = 20);

        Task<OneBotResponse<MessageHistoryResponse>> GetGroupMsgHistoryAsync(long groupId, int messageSeq = 0,
            int count = 20);

        // 精華消息 API
        Task<OneBotResponse<List<EssenceMessage>>> GetEssenceMsgListAsync(long groupId);
        Task<OneBotResponse<object>> SetEssenceMsgAsync(long messageId);
        Task<OneBotResponse<object>> DeleteEssenceMsgAsync(long messageId);

        // 群文件 API
        Task<OneBotResponse<List<GroupFileInfo>>> GetGroupFileSystemInfoAsync(long groupId);
        Task<OneBotResponse<List<GroupFolderInfo>>> GetGroupRootFilesAsync(long groupId);
        Task<OneBotResponse<List<GroupFileInfo>>> GetGroupFilesByFolderAsync(long groupId, string folderId);
        Task<OneBotResponse<object>> UploadGroupFileAsync(long groupId, string file, string name, string folder = "/");
        Task<OneBotResponse<object>> DeleteGroupFileAsync(long groupId, string fileId, int busid);
        Task<OneBotResponse<object>> CreateGroupFileFolderAsync(long groupId, string name, string parentId = "/");
        Task<OneBotResponse<object>> DeleteGroupFolderAsync(long groupId, string folderId);
        Task<OneBotResponse<string>> GetGroupFileUrlAsync(long groupId, string fileId, int busid);

        // 其他 API
        Task<OneBotResponse<object>> MarkMsgAsReadAsync(long messageId);
        Task<OneBotResponse<object>> GetCookiesAsync(string domain = "");
        Task<OneBotResponse<object>> GetCsrfTokenAsync();
        Task<OneBotResponse<object>> GetCredentialsAsync(string domain = "");
        Task<OneBotResponse<object>> GetStatusAsync();
        Task<OneBotResponse<object>> GetVersionInfoAsync();
        Task<OneBotResponse<object>> SetRestartAsync(int delay = 0);
        Task<OneBotResponse<object>> CleanCacheAsync();

        // 歷史消息統一方法
        Task<OneBotResponse<MessageHistoryData>> GetMessageHistoryAsync(long chatId, bool isGroup = false);
    }
}