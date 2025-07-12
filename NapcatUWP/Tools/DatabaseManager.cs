using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Data.Sqlite;

namespace NapcatUWP.Tools
{
    /// <summary>
    ///     数据库管理器 - 解决并发访问和锁定问题 (UWP 15063 优化版本)
    /// </summary>
    public static class DatabaseManager
    {
        private static readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(5, 5); // 允许最多5个并发读取

        // 减少重试次数和延迟，避免长时间阻塞
        private static readonly int _maxRetries = 5;
        private static readonly int _baseRetryDelayMs = 50;

        // 连接池式管理
        private static readonly ConcurrentQueue<DateTime> _recentOperations = new ConcurrentQueue<DateTime>();

        /// <summary>
        ///     获取数据库连接字符串 - UWP 15063 优化版本
        /// </summary>
        private static string GetConnectionString()
        {
            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            // 简化连接字符串，只使用 UWP 15063 支持的参数
            return $"Data Source={dbpath}";
        }

        /// <summary>
        ///     执行数据库读取操作
        /// </summary>
        public static async Task<T> ExecuteReadAsync<T>(Func<SqliteConnection, T> operation)
        {
            return await ExecuteWithRetryAsync(operation, false);
        }

        /// <summary>
        ///     执行数据库写入操作
        /// </summary>
        public static async Task<T> ExecuteWriteAsync<T>(Func<SqliteConnection, T> operation)
        {
            return await ExecuteWithRetryAsync(operation, true);
        }

        /// <summary>
        ///     执行数据库操作（无返回值）
        /// </summary>
        public static async Task ExecuteAsync(Action<SqliteConnection> operation)
        {
            await ExecuteWithRetryAsync<object>(db =>
            {
                operation(db);
                return null;
            }, true);
        }

        /// <summary>
        ///     带重试和优化的数据库操作执行 - UWP 15063 兼容版本
        /// </summary>
        private static async Task<T> ExecuteWithRetryAsync<T>(Func<SqliteConnection, T> operation, bool isWrite)
        {
            var semaphore = isWrite ? _writeSemaphore : _readSemaphore;
            var retryCount = 0;

            // 清理过期的操作记录
            CleanOldOperations();

            while (retryCount < _maxRetries)
                try
                {
                    // 检查系统负载
                    if (_recentOperations.Count > 10) await Task.Delay(10); // 短暂延迟以减少负载

                    await semaphore.WaitAsync();

                    try
                    {
                        using (var connection = new SqliteConnection(GetConnectionString()))
                        {
                            await connection.OpenAsync();

                            // 设置基本的数据库参数
                            await SetConnectionParametersAsync(connection, isWrite);

                            // 记录操作时间
                            _recentOperations.Enqueue(DateTime.Now);

                            return operation(connection);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
                {
                    retryCount++;
                    var delay = _baseRetryDelayMs * (int)Math.Pow(2, Math.Min(retryCount - 1, 3)); // 指数退避

                    Debug.WriteLine($"数据库忙碌，重试 {retryCount}/{_maxRetries}，延迟 {delay}ms: {ex.Message}");

                    if (retryCount >= _maxRetries)
                    {
                        Debug.WriteLine("数据库操作达到最大重试次数，操作失败");
                        throw new InvalidOperationException($"数据库操作超时，重试 {_maxRetries} 次后仍然失败", ex);
                    }

                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"数据库操作发生错误: {ex.Message}");
                    throw;
                }

            throw new InvalidOperationException("数据库操作执行失败");
        }

        /// <summary>
        ///     设置连接参数 - UWP 15063 兼容版本
        /// </summary>
        private static async Task SetConnectionParametersAsync(SqliteConnection connection, bool isWrite)
        {
            try
            {
                // 设置忙碌超时 - 减少超时时间避免长时间阻塞
                using (var cmd = new SqliteCommand("PRAGMA busy_timeout = 2000", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                if (isWrite)
                {
                    // 写操作使用更安全的设置
                    using (var cmd = new SqliteCommand("PRAGMA synchronous = NORMAL", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 尝试使用 WAL 模式，如果失败则使用 DELETE 模式
                    try
                    {
                        using (var cmd = new SqliteCommand("PRAGMA journal_mode = WAL", connection))
                        {
                            var result = await cmd.ExecuteScalarAsync();
                            Debug.WriteLine($"Journal mode set to: {result}");
                        }
                    }
                    catch (Exception)
                    {
                        // WAL 模式可能不被支持，使用默认的 DELETE 模式
                        using (var cmd = new SqliteCommand("PRAGMA journal_mode = DELETE", connection))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                else
                {
                    // 读操作使用更快的设置
                    using (var cmd = new SqliteCommand("PRAGMA synchronous = OFF", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置数据库参数时发生错误: {ex.Message}");
                // 不抛出异常，继续执行操作
            }
        }

        /// <summary>
        ///     清理过期的操作记录
        /// </summary>
        private static void CleanOldOperations()
        {
            var cutoff = DateTime.Now.AddSeconds(-10);
            while (_recentOperations.TryPeek(out var oldest) && oldest < cutoff) _recentOperations.TryDequeue(out _);
        }

        /// <summary>
        ///     批量执行数据库操作 - 优化版本
        /// </summary>
        public static async Task ExecuteBatchAsync(List<Action<SqliteConnection, SqliteTransaction>> operations)
        {
            if (operations == null || operations.Count == 0) return;

            await ExecuteAsync(db =>
            {
                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        foreach (var operation in operations) operation(db, transaction);

                        transaction.Commit();
                        Debug.WriteLine($"批量操作成功执行 {operations.Count} 个操作");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"批量操作失败，回滚事务: {ex.Message}");
                        transaction.Rollback();
                        throw;
                    }
                }
            });
        }

        /// <summary>
        ///     检查数据库连接状态
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
                        return result != null && Convert.ToInt32(result) == 1;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"数据库健康检查失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     优化数据库 - 定期维护
        /// </summary>
        public static async Task OptimizeDatabaseAsync()
        {
            try
            {
                await ExecuteAsync(db =>
                {
                    // 分析查询计划
                    using (var cmd = new SqliteCommand("ANALYZE", db))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // 压缩数据库
                    using (var cmd = new SqliteCommand("VACUUM", db))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    Debug.WriteLine("数据库优化完成");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"数据库优化失败: {ex.Message}");
            }
        }
    }
}