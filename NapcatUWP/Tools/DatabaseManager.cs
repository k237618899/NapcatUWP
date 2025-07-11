using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Windows.Storage;
using System.IO;
using System.Collections.Concurrent;
using NapcatUWP.Models;
using Newtonsoft.Json;

namespace NapcatUWP.Tools
{
    /// <summary>
    /// 数据库管理器 - 解决并发访问和锁定问题
    /// </summary>
    public static class DatabaseManager
    {
        private static readonly object _lockObject = new object();
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private static readonly ConcurrentDictionary<string, object> _operationLocks =
            new ConcurrentDictionary<string, object>();

        // 连接池设定
        private static readonly int _maxRetries = 3;
        private static readonly int _retryDelayMs = 100;

        /// <summary>
        /// 获取数据库连接字符串 - UWP 15063 兼容版本
        /// </summary>
        private static string GetConnectionString()
        {
            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            // 使用 UWP 兼容的连接字符串格式，移除不支持的参数
            return $"Data Source={dbpath};Cache=Shared;";
        }

        /// <summary>
        /// 执行数据库操作（读取）
        /// </summary>
        public static async Task<T> ExecuteReadAsync<T>(Func<SqliteConnection, T> operation)
        {
            return await ExecuteWithRetryAsync(operation, isWrite: false);
        }

        /// <summary>
        /// 执行数据库操作（写入）
        /// </summary>
        public static async Task<T> ExecuteWriteAsync<T>(Func<SqliteConnection, T> operation)
        {
            return await ExecuteWithRetryAsync(operation, isWrite: true);
        }

        /// <summary>
        /// 执行数据库操作（无返回值）
        /// </summary>
        public static async Task ExecuteAsync(Action<SqliteConnection> operation)
        {
            await ExecuteWithRetryAsync<object>(db =>
            {
                operation(db);
                return null;
            }, isWrite: true);
        }

        /// <summary>
        /// 带重试机制的数据库操作执行 - UWP 15063 兼容版本
        /// </summary>
        private static async Task<T> ExecuteWithRetryAsync<T>(Func<SqliteConnection, T> operation, bool isWrite)
        {
            var retryCount = 0;

            while (retryCount < _maxRetries)
            {
                try
                {
                    // 使用信号量限制并发访问
                    await _semaphore.WaitAsync();

                    try
                    {
                        using (var connection = new SqliteConnection(GetConnectionString()))
                        {
                            await connection.OpenAsync();

                            // 设置连接参数 - 使用 PRAGMA 语句而不是连接字符串
                            if (isWrite)
                            {
                                // 设置忙碌超时
                                using (var cmd = new SqliteCommand("PRAGMA busy_timeout = 5000", connection))
                                {
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // 设置同步模式
                                using (var cmd = new SqliteCommand("PRAGMA synchronous = NORMAL", connection))
                                {
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // 尝试设置 WAL 模式（如果支持）
                                try
                                {
                                    using (var cmd = new SqliteCommand("PRAGMA journal_mode = WAL", connection))
                                    {
                                        await cmd.ExecuteNonQueryAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"WAL 模式设置失败，使用默认模式: {ex.Message}");
                                    // 如果 WAL 模式不支持，使用 DELETE 模式
                                    using (var cmd = new SqliteCommand("PRAGMA journal_mode = DELETE", connection))
                                    {
                                        await cmd.ExecuteNonQueryAsync();
                                    }
                                }
                            }

                            return operation(connection);
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
                {
                    retryCount++;
                    Debug.WriteLine($"数据库忙碌，重试 {retryCount}/{_maxRetries}: {ex.Message}");

                    if (retryCount >= _maxRetries)
                    {
                        Debug.WriteLine("数据库操作最大重试次数已达到，拋出异常");
                        throw;
                    }

                    // 等待一段时间后重试
                    await Task.Delay(_retryDelayMs * retryCount);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"数据库操作发生错误: {ex.Message}");
                    throw;
                }
            }

            throw new InvalidOperationException("数据库操作执行失败");
        }

        /// <summary>
        /// 批量执行数据库操作
        /// </summary>
        public static async Task ExecuteBatchAsync(List<Action<SqliteConnection, SqliteTransaction>> operations)
        {
            await ExecuteAsync(db =>
            {
                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        foreach (var operation in operations)
                        {
                            operation(db, transaction);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            });
        }

        /// <summary>
        /// 检查数据库连接状态
        /// </summary>
        public static async Task<bool> CheckDatabaseHealthAsync()
        {
            try
            {
                return await ExecuteReadAsync(db =>
                {
                    using (var cmd = new SqliteCommand("SELECT 1", db))
                    {
                        var result = cmd.ExecuteScalar();
                        return result != null;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"数据库健康检查失败: {ex.Message}");
                return false;
            }
        }
    }
}