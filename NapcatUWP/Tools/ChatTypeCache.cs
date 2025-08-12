using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NapcatUWP.Controls;
using NapcatUWP.Models;

namespace NapcatUWP.Tools
{
    /// <summary>
    ///     聊天类型缓存管理器 - 避免重复数据库查询
    /// </summary>
    public static class ChatTypeCache
    {
        private static HashSet<long> _groupIds;
        private static HashSet<long> _friendIds;
        private static DateTime _lastRefresh = DateTime.MinValue;
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5); // 5分钟缓存
        private static readonly object _lockObject = new object();

        /// <summary>
        ///     获取聊天类型（带缓存）
        /// </summary>
        /// <param name="chatId">聊天ID</param>
        /// <returns>true=群组，false=好友，null=未知</returns>
        public static bool? GetChatType(long chatId)
        {
            if (chatId <= 0) return null;

            RefreshCacheIfNeeded();

            lock (_lockObject)
            {
                if (_groupIds?.Contains(chatId) == true)
                    return true;

                if (_friendIds?.Contains(chatId) == true)
                    return false;

                return null; // 未找到
            }
        }

        /// <summary>
        ///     批量获取聊天类型
        /// </summary>
        public static Dictionary<long, bool> GetChatTypes(IEnumerable<long> chatIds)
        {
            var result = new Dictionary<long, bool>();

            RefreshCacheIfNeeded();

            lock (_lockObject)
            {
                if (_groupIds == null || _friendIds == null)
                    return result;

                foreach (var chatId in chatIds)
                {
                    if (chatId <= 0) continue;

                    if (_groupIds.Contains(chatId))
                        result[chatId] = true;
                    else if (_friendIds.Contains(chatId))
                        result[chatId] = false;
                    // 未找到的不添加到结果中
                }
            }

            return result;
        }

        /// <summary>
        ///     强制刷新缓存
        /// </summary>
        public static void ForceRefresh()
        {
            lock (_lockObject)
            {
                _lastRefresh = DateTime.MinValue;
                RefreshCacheIfNeeded();
            }
        }

        /// <summary>
        ///     根据需要刷新缓存
        /// </summary>
        private static void RefreshCacheIfNeeded()
        {
            lock (_lockObject)
            {
                if (DateTime.Now - _lastRefresh < CACHE_DURATION &&
                    _groupIds != null && _friendIds != null)
                    return; // 缓存仍然有效

                try
                {
                    Debug.WriteLine("刷新聊天类型缓存...");

                    // 获取群组ID
                    var groups = DataAccess.GetAllGroups();
                    _groupIds = new HashSet<long>(groups.Select(g => g.GroupId));

                    // 获取好友ID
                    var friendCategories = DataAccess.GetAllFriendsWithCategories();
                    _friendIds = new HashSet<long>(
                        friendCategories.SelectMany(c => c.BuddyList ?? new List<FriendInfo>())
                            .Select(f => f.UserId)
                    );

                    _lastRefresh = DateTime.Now;

                    Debug.WriteLine($"缓存刷新完成: {_groupIds.Count} 个群组, {_friendIds.Count} 个好友");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"刷新聊天类型缓存失败: {ex.Message}");
                    // 出错时保持旧缓存
                }
            }
        }

        /// <summary>
        ///     添加新的聊天类型到缓存
        /// </summary>
        public static void AddChatType(long chatId, bool isGroup)
        {
            if (chatId <= 0) return;

            lock (_lockObject)
            {
                if (_groupIds == null || _friendIds == null)
                {
                    RefreshCacheIfNeeded();
                    return;
                }

                if (isGroup)
                {
                    _groupIds.Add(chatId);
                    _friendIds.Remove(chatId); // 确保不在好友列表中
                }
                else
                {
                    _friendIds.Add(chatId);
                    _groupIds.Remove(chatId); // 确保不在群组列表中
                }
            }
        }

        /// <summary>
        ///     移除聊天类型缓存
        /// </summary>
        public static void RemoveChatType(long chatId)
        {
            if (chatId <= 0) return;

            lock (_lockObject)
            {
                _groupIds?.Remove(chatId);
                _friendIds?.Remove(chatId);
            }
        }

        /// <summary>
        ///     获取缓存统计信息
        /// </summary>
        public static (int GroupCount, int FriendCount, DateTime LastRefresh) GetCacheStats()
        {
            lock (_lockObject)
            {
                return (_groupIds?.Count ?? 0, _friendIds?.Count ?? 0, _lastRefresh);
            }
        }
    }
}