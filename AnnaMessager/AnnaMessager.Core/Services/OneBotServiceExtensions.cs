using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     OneBotService 擴展方法 - 提供方法別名以保持向後兼容性
    /// </summary>
    public static class OneBotServiceExtensions
    {
        /// <summary>
        ///     SendPrivateMsgAsync 方法別名
        /// </summary>
        public static async Task<OneBotResponse<object>> SendPrivateMsgAsync(this IOneBotService service, long userId,
            string message, bool autoEscape = false)
        {
            return await service.SendPrivateMessageAsync(userId, message, autoEscape);
        }

        /// <summary>
        ///     SendGroupMsgAsync 方法別名
        /// </summary>
        public static async Task<OneBotResponse<object>> SendGroupMsgAsync(this IOneBotService service, long groupId,
            string message, bool autoEscape = false)
        {
            return await service.SendGroupMessageAsync(groupId, message, autoEscape);
        }

        /// <summary>
        ///     獲取聊天歷史消息的便捷方法
        /// </summary>
        public static async Task<List<MessageHistoryItem>> GetChatHistoryAsync(this IOneBotService service,
            long chatId, bool isGroup, int count = 20)
        {
            try
            {
                if (isGroup)
                {
                    var response = await service.GetGroupMsgHistoryAsync(chatId, 0, count);
                    return response.Status == "ok"
                        ? response.Data?.Messages ?? new List<MessageHistoryItem>()
                        : new List<MessageHistoryItem>();
                }
                else
                {
                    var response = await service.GetFriendMsgHistoryAsync(chatId, 0, count);
                    return response.Status == "ok"
                        ? response.Data?.Messages ?? new List<MessageHistoryItem>()
                        : new List<MessageHistoryItem>();
                }
            }
            catch (Exception)
            {
                return new List<MessageHistoryItem>();
            }
        }

        /// <summary>
        ///     檢查用戶是否在線
        /// </summary>
        public static async Task<bool> IsUserOnlineAsync(this IOneBotService service, long userId)
        {
            try
            {
                var response = await service.GetStrangerInfoAsync(userId);
                return response.Status == "ok";
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     批量獲取群成員信息
        /// </summary>
        public static async Task<Dictionary<long, GroupMemberInfo>> GetGroupMembersBatchAsync(
            this IOneBotService service, long groupId)
        {
            try
            {
                var response = await service.GetGroupMemberListAsync(groupId);
                if (response.Status == "ok" && response.Data != null)
                    return response.Data.ToDictionary(m => m.UserId, m => m);
                return new Dictionary<long, GroupMemberInfo>();
            }
            catch (Exception)
            {
                return new Dictionary<long, GroupMemberInfo>();
            }
        }
    }
}