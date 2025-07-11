using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NapcatUWP.Models;
using NapcatUWP.Tools;
using Newtonsoft.Json;
using System.Linq;
using NapcatUWP.Controls;

namespace NapcatUWP.Tools
{
    /// <summary>
    /// 安全的數據訪問類 - 使用 DatabaseManager 避免並發問題
    /// </summary>
    public static class SafeDataAccess
    {
        /// <summary>
        /// 安全保存消息到數據庫
        /// </summary>
        public static async Task SaveMessageAsync(long messageId, long chatId, bool isGroup, string content,
            string messageType, long senderId, string senderName, bool isFromMe, DateTime timestamp,
            List<MessageSegment> segments = null)
        {
            try
            {
                await DatabaseManager.ExecuteWriteAsync(db =>
                {
                    var segmentsJson = "";
                    if (segments != null && segments.Count > 0)
                    {
                        try
                        {
                            segmentsJson = JsonConvert.SerializeObject(segments);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"序列化消息段時發生錯誤: {ex.Message}");
                        }
                    }

                    var insertCommand = new SqliteCommand(@"
                        INSERT OR IGNORE INTO Messages 
                        (MessageId, ChatId, IsGroup, Content, MessageType, SenderId, SenderName, IsFromMe, Timestamp, SegmentsJson) 
                        VALUES (@messageId, @chatId, @isGroup, @content, @messageType, @senderId, @senderName, @isFromMe, @timestamp, @segmentsJson)",
                        db);

                    insertCommand.Parameters.AddWithValue("@messageId", messageId);
                    insertCommand.Parameters.AddWithValue("@chatId", chatId);
                    insertCommand.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);
                    insertCommand.Parameters.AddWithValue("@content", content ?? "");
                    insertCommand.Parameters.AddWithValue("@messageType", messageType ?? "text");
                    insertCommand.Parameters.AddWithValue("@senderId", senderId);
                    insertCommand.Parameters.AddWithValue("@senderName", senderName ?? "");
                    insertCommand.Parameters.AddWithValue("@isFromMe", isFromMe ? 1 : 0);
                    insertCommand.Parameters.AddWithValue("@timestamp", timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                    insertCommand.Parameters.AddWithValue("@segmentsJson", segmentsJson);

                    var rowsAffected = insertCommand.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        Debug.WriteLine($"保存新消息: MessageId={messageId}, ChatId={chatId}, Content={content}");
                    }

                    return rowsAffected;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"安全保存消息錯誤: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 安全獲取聊天消息
        /// </summary>
        public static async Task<List<ChatMessage>> GetChatMessagesAsync(long chatId, bool isGroup, int limit = 50)
        {
            try
            {
                return await DatabaseManager.ExecuteReadAsync(db =>
                {
                    var messages = new List<ChatMessage>();

                    var selectCommand = new SqliteCommand(@"
                        SELECT * FROM Messages 
                        WHERE ChatId = @chatId AND IsGroup = @isGroup 
                        ORDER BY Timestamp ASC 
                        LIMIT @limit", db);

                    selectCommand.Parameters.AddWithValue("@chatId", chatId);
                    selectCommand.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);
                    selectCommand.Parameters.AddWithValue("@limit", limit);

                    using (var query = selectCommand.ExecuteReader())
                    {
                        while (query.Read())
                        {
                            var timestampStr = Convert.ToString(query["Timestamp"]);
                            DateTime.TryParse(timestampStr, out var originalTimestamp);

                            var timestamp = DataAccess.ProcessTimestamp(originalTimestamp);

                            var segments = new List<MessageSegment>();
                            try
                            {
                                var segmentsJsonOrdinal = query.GetOrdinal("SegmentsJson");
                                if (!query.IsDBNull(segmentsJsonOrdinal))
                                {
                                    var segmentsJson = Convert.ToString(query["SegmentsJson"]);
                                    if (!string.IsNullOrEmpty(segmentsJson))
                                    {
                                        var rawSegments =
                                            JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JArray>(segmentsJson);
                                        if (rawSegments != null)
                                        {
                                            segments = MessageSegmentParser.ParseMessageArray(rawSegments,
                                                isGroup ? chatId : 0);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"反序列化消息段時發生錯誤: {ex.Message}");
                                var content = Convert.ToString(query["Content"]) ?? "";
                                if (!string.IsNullOrEmpty(content))
                                    segments = new List<MessageSegment> { new TextSegment(content) };
                            }

                            if (segments.Count == 0)
                            {
                                var content = Convert.ToString(query["Content"]) ?? "";
                                if (!string.IsNullOrEmpty(content))
                                    segments = new List<MessageSegment> { new TextSegment(content) };
                            }

                            var senderId = Convert.ToInt64(query["SenderId"]);
                            var isFromMe = Convert.ToInt32(query["IsFromMe"]) == 1;
                            var senderName = Convert.ToString(query["SenderName"]) ?? "";

                            if (!isFromMe)
                            {
                                if (isGroup)
                                {
                                    var memberInfo = DataAccess.GetGroupMember(chatId, senderId);
                                    if (memberInfo != null)
                                    {
                                        senderName = memberInfo.GetDisplayName();
                                    }
                                }
                                else
                                {
                                    var friendName = DataAccess.GetFriendNameById(senderId);
                                    if (!friendName.StartsWith("用戶 "))
                                    {
                                        senderName = friendName;
                                    }
                                }
                            }

                            var chatMessage = new ChatMessage
                            {
                                Content = Convert.ToString(query["Content"]) ?? "",
                                MessageType = Convert.ToString(query["MessageType"]) ?? "text",
                                SenderId = senderId,
                                SenderName = senderName,
                                IsFromMe = isFromMe,
                                Timestamp = timestamp,
                                Segments = segments
                            };

                            messages.Add(chatMessage);
                        }
                    }

                    return messages;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"安全獲取聊天消息錯誤: {ex.Message}");
                return new List<ChatMessage>();
            }
        }

        /// <summary>
        /// 安全保存群組信息
        /// </summary>
        public static async Task SaveGroupsAsync(List<GroupInfo> groups)
        {
            try
            {
                await DatabaseManager.ExecuteWriteAsync(db =>
                {
                    using (var transaction = db.BeginTransaction())
                    {
                        try
                        {
                            // 清空現有數據
                            var deleteCommand = new SqliteCommand("DELETE FROM Groups", db, transaction);
                            deleteCommand.ExecuteNonQuery();

                            // 插入新數據
                            var insertCommand = new SqliteCommand(@"
                                INSERT INTO Groups (GroupId, GroupName, GroupRemark, MemberCount, MaxMemberCount, GroupAllShut, LastUpdated) 
                                VALUES (@groupId, @groupName, @groupRemark, @memberCount, @maxMemberCount, @groupAllShut, @lastUpdated)",
                                db, transaction);

                            foreach (var group in groups)
                            {
                                insertCommand.Parameters.Clear();
                                insertCommand.Parameters.AddWithValue("@groupId", group.GroupId);
                                insertCommand.Parameters.AddWithValue("@groupName", group.GroupName ?? "");
                                insertCommand.Parameters.AddWithValue("@groupRemark", group.GroupRemark ?? "");
                                insertCommand.Parameters.AddWithValue("@memberCount", group.MemberCount);
                                insertCommand.Parameters.AddWithValue("@maxMemberCount", group.MaxMemberCount);
                                insertCommand.Parameters.AddWithValue("@groupAllShut", group.GroupAllShut ? 1 : 0);
                                insertCommand.Parameters.AddWithValue("@lastUpdated",
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                                insertCommand.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            Debug.WriteLine($"安全保存 {groups.Count} 個群組到數據庫");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }

                    return groups.Count;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"安全保存群組數據錯誤: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 安全獲取所有群組
        /// </summary>
        public static async Task<List<GroupInfo>> GetAllGroupsAsync()
        {
            try
            {
                return await DatabaseManager.ExecuteReadAsync(db =>
                {
                    var groups = new List<GroupInfo>();

                    var selectCommand = new SqliteCommand("SELECT * FROM Groups ORDER BY GroupName", db);

                    using (var query = selectCommand.ExecuteReader())
                    {
                        while (query.Read())
                        {
                            groups.Add(new GroupInfo
                            {
                                GroupId = Convert.ToInt64(query["GroupId"]),
                                GroupName = Convert.ToString(query["GroupName"]) ?? "",
                                GroupRemark = query.IsDBNull(query.GetOrdinal("GroupRemark"))
                                    ? ""
                                    : Convert.ToString(query["GroupRemark"]),
                                MemberCount = Convert.ToInt32(query["MemberCount"]),
                                MaxMemberCount = Convert.ToInt32(query["MaxMemberCount"]),
                                GroupAllShut = Convert.ToInt32(query["GroupAllShut"]) == 1
                            });
                        }
                    }

                    return groups;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"安全獲取群組數據錯誤: {ex.Message}");
                return new List<GroupInfo>();
            }
        }

        /// <summary>
        /// 批量數據庫操作示例
        /// </summary>
        public static async Task BatchUpdateUserInfoAsync()
        {
            try
            {
                await DatabaseManager.ExecuteWriteAsync(db =>
                {
                    using (var transaction = db.BeginTransaction())
                    {
                        try
                        {
                            // 獲取需要更新的消息
                            var selectCommand = new SqliteCommand(@"
                                SELECT DISTINCT ChatId, SenderId, IsGroup, SenderName 
                                FROM Messages 
                                WHERE IsFromMe = 0 AND SenderName LIKE '用戶 %'", db, transaction);

                            var messagesToUpdate =
                                new List<(long ChatId, long SenderId, bool IsGroup, string OldSenderName)>();

                            using (var query = selectCommand.ExecuteReader())
                            {
                                while (query.Read())
                                {
                                    messagesToUpdate.Add((
                                        Convert.ToInt64(query["ChatId"]),
                                        Convert.ToInt64(query["SenderId"]),
                                        Convert.ToInt32(query["IsGroup"]) == 1,
                                        Convert.ToString(query["SenderName"]) ?? ""
                                    ));
                                }
                            }

                            // 批量更新
                            var updateCommand = new SqliteCommand(@"
                                UPDATE Messages 
                                SET SenderName = @newSenderName 
                                WHERE ChatId = @chatId AND SenderId = @senderId AND IsGroup = @isGroup AND IsFromMe = 0",
                                db, transaction);

                            var updatedCount = 0;
                            foreach (var (chatId, senderId, isGroup, oldSenderName) in messagesToUpdate)
                            {
                                string newSenderName = "";

                                if (isGroup)
                                {
                                    var memberInfo = DataAccess.GetGroupMember(chatId, senderId);
                                    if (memberInfo != null)
                                    {
                                        newSenderName = memberInfo.GetDisplayName();
                                    }
                                }
                                else
                                {
                                    var friendName = DataAccess.GetFriendNameById(senderId);
                                    if (!friendName.StartsWith("用戶 "))
                                    {
                                        newSenderName = friendName;
                                    }
                                }

                                if (!string.IsNullOrEmpty(newSenderName) && newSenderName != oldSenderName)
                                {
                                    updateCommand.Parameters.Clear();
                                    updateCommand.Parameters.AddWithValue("@newSenderName", newSenderName);
                                    updateCommand.Parameters.AddWithValue("@chatId", chatId);
                                    updateCommand.Parameters.AddWithValue("@senderId", senderId);
                                    updateCommand.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);

                                    var affectedRows = updateCommand.ExecuteNonQuery();
                                    updatedCount += affectedRows;
                                }
                            }

                            transaction.Commit();
                            Debug.WriteLine($"批量更新用戶信息完成，更新了 {updatedCount} 條記錄");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }

                    return 0;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"批量更新用戶信息錯誤: {ex.Message}");
                throw;
            }
        }
    }
}