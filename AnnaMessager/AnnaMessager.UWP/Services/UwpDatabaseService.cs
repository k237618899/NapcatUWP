using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using Microsoft.Data.Sqlite;

namespace AnnaMessager.UWP.Services
{
    /// <summary>
    ///     UWP 平台的 SQLite 數據庫服務實現
    /// </summary>
    public class UwpDatabaseService : IPlatformDatabaseService
    {
        private const string DatabaseName = "AnnaMessager.db";
        private readonly string _databasePath;

        public UwpDatabaseService()
        {
            _databasePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, DatabaseName);
            Debug.WriteLine($"數據庫路徑: {_databasePath}");
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                Debug.WriteLine("開始初始化數據庫...");
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    Debug.WriteLine("數據庫連接成功");

                    // 創建賬號表
                    await ExecuteAsync(connection, @"
                        CREATE TABLE IF NOT EXISTS Accounts (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Account TEXT NOT NULL,
                            AccessToken TEXT,
                            Nickname TEXT,
                            Avatar TEXT,
                            LastLoginTime TEXT NOT NULL,
                            IsDefault INTEGER NOT NULL DEFAULT 0,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL,
                            UNIQUE(Account)
                        )");

                    // 創建伺服器表
                    await ExecuteAsync(connection, @"
                        CREATE TABLE IF NOT EXISTS Servers (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            ServerUrl TEXT NOT NULL,
                            ConnectionTimeout INTEGER NOT NULL DEFAULT 30,
                            EnableSsl INTEGER NOT NULL DEFAULT 0,
                            AutoReconnect INTEGER NOT NULL DEFAULT 1,
                            IsDefault INTEGER NOT NULL DEFAULT 0,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        )");

                    // 創建聊天緩存表
                    await ExecuteAsync(connection, @"
                        CREATE TABLE IF NOT EXISTS ChatCache (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ChatId INTEGER NOT NULL,
                            IsGroup INTEGER NOT NULL,
                            Name TEXT,
                            Avatar TEXT,
                            LastMessage TEXT,
                            LastMessageTime TEXT,
                            UnreadCount INTEGER DEFAULT 0,
                            LastMessageType TEXT,
                            ChatData TEXT,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL,
                            UNIQUE(ChatId, IsGroup)
                        )");

                    // 創建聯絡人緩存表 (擴充分類欄位)
                    await ExecuteAsync(connection, @"
                        CREATE TABLE IF NOT EXISTS ContactCache (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            UserId INTEGER NOT NULL UNIQUE,
                            Nickname TEXT,
                            Remark TEXT,
                            Avatar TEXT,
                            Status TEXT,
                            CategoryId INTEGER DEFAULT 0,
                            CategoryName TEXT,
                            ContactData TEXT,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        )");

                    // 創建群組緩存表
                    await ExecuteAsync(connection, @"
                        CREATE TABLE IF NOT EXISTS GroupCache (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            GroupId INTEGER NOT NULL UNIQUE,
                            GroupName TEXT,
                            GroupAvatar TEXT,
                            MemberCount INTEGER DEFAULT 0,
                            GroupData TEXT,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        )");

                    // 創建設定表
                    await ExecuteAsync(connection, @"
                        CREATE TABLE IF NOT EXISTS AppSettings (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Key TEXT NOT NULL UNIQUE,
                            Value TEXT,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        )");

                    // 創建消息緩存表
                    await ExecuteAsync(connection, @"
                        CREATE TABLE IF NOT EXISTS MessageCache (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ChatId INTEGER NOT NULL,
                            IsGroup INTEGER NOT NULL,
                            MessageId INTEGER NOT NULL,
                            MessageType TEXT,
                            Content TEXT,
                            SenderId INTEGER,
                            SenderName TEXT,
                            Time TEXT,
                            IsFromSelf INTEGER,
                            SendStatus TEXT,
                            ImageUrl TEXT,
                            FaceId TEXT,
                            AudioFile TEXT,
                            VideoFile TEXT,
                            ForwardId TEXT,
                            ExtraData TEXT,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL,
                            UNIQUE(ChatId, IsGroup, MessageId)
                        )");

                    // 自動升級：ContactCache 新增分類欄位
                    try
                    {
                        using (var pragma = new SqliteCommand("PRAGMA table_info(ContactCache);", connection))
                        using (var reader = await pragma.ExecuteReaderAsync())
                        {
                            var hasCategoryId = false;
                            var hasCategoryName = false;
                            while (await reader.ReadAsync())
                            {
                                var colName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                                if (colName == "CategoryId") hasCategoryId = true;
                                if (colName == "CategoryName") hasCategoryName = true;
                            }
                            if (!hasCategoryId)
                            {
                                using (var alter = new SqliteCommand("ALTER TABLE ContactCache ADD COLUMN CategoryId INTEGER DEFAULT 0;", connection))
                                {
                                    await alter.ExecuteNonQueryAsync();
                                    Debug.WriteLine("資料庫升級: ContactCache.CategoryId 已新增");
                                }
                            }
                            if (!hasCategoryName)
                            {
                                using (var alter2 = new SqliteCommand("ALTER TABLE ContactCache ADD COLUMN CategoryName TEXT;", connection))
                                {
                                    await alter2.ExecuteNonQueryAsync();
                                    Debug.WriteLine("資料庫升級: ContactCache.CategoryName 已新增");
                                }
                            }
                        }
                    }
                    catch (Exception upEx)
                    {
                        Debug.WriteLine($"資料庫升級檢查失敗: {upEx.Message}");
                    }

                    // 創建好友分類緩存表
                    await ExecuteAsync(connection, @"
                        CREATE TABLE IF NOT EXISTS CategoryCache (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            CategoryId INTEGER NOT NULL UNIQUE,
                            CategoryName TEXT,
                            SortOrder INTEGER DEFAULT 0,
                            TotalCount INTEGER DEFAULT 0,
                            CreatedAt TEXT NOT NULL,
                            UpdatedAt TEXT NOT NULL
                        )");

                    Debug.WriteLine("數據庫表創建完成");
                }

                Debug.WriteLine("數據庫初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"數據庫初始化失敗: {ex.Message}");
                Debug.WriteLine($"錯誤詳情: {ex.StackTrace}");
                throw;
            }
        }

        #region 設定管理

        public async Task<List<AppSettingsEntity>> GetAllSettingsAsync()
        {
            var settings = new List<AppSettingsEntity>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand("SELECT * FROM AppSettings ORDER BY Key", connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) settings.Add(MapToAppSettings(reader));
                    }
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取所有設定失敗: {ex.Message}");
            }

            return settings;
        }

        public async Task<AppSettingsEntity> GetSettingAsync(string key)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand("SELECT * FROM AppSettings WHERE Key = @key", connection))
                    {
                        command.Parameters.AddWithValue("@key", key);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync()) return MapToAppSettings(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取設定失敗: {ex.Message}");
            }

            return null;
        }

        public async Task<int> SaveSettingAsync(AppSettingsEntity setting)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // 先嘗試更新
                    using (var update = new SqliteCommand(@"UPDATE AppSettings SET Value=@value, UpdatedAt=@updatedAt WHERE Key=@key", connection))
                    {
                        update.Parameters.AddWithValue("@value", setting.Value ?? "");
                        update.Parameters.AddWithValue("@updatedAt", now);
                        update.Parameters.AddWithValue("@key", setting.Key ?? "");
                        var rows = await update.ExecuteNonQueryAsync();
                        if (rows > 0) return rows; // 返回受影響行數
                    }

                    // 沒有更新則插入
                    using (var insert = new SqliteCommand(@"INSERT INTO AppSettings (Key, Value, CreatedAt, UpdatedAt) VALUES (@key,@value,@createdAt,@updatedAt); SELECT last_insert_rowid();", connection))
                    {
                        insert.Parameters.AddWithValue("@key", setting.Key ?? "");
                        insert.Parameters.AddWithValue("@value", setting.Value ?? "");
                        insert.Parameters.AddWithValue("@createdAt", now);
                        insert.Parameters.AddWithValue("@updatedAt", now);
                        var idObj = await insert.ExecuteScalarAsync();
                        return Convert.ToInt32(idObj);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存設定失敗: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteSettingAsync(string key)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM AppSettings WHERE Key=@key", connection))
                    {
                        cmd.Parameters.AddWithValue("@key", key);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除設定失敗: {ex.Message}");
                throw;
            }
        }

        public async Task ClearAllSettingsAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM AppSettings", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除所有設定失敗: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 賬號管理

        public async Task<List<AccountEntity>> GetAllAccountsAsync()
        {
            var accounts = new List<AccountEntity>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command =
                           new SqliteCommand("SELECT * FROM Accounts ORDER BY IsDefault DESC, LastLoginTime DESC",
                               connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) accounts.Add(MapToAccount(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取所有賬號失敗: {ex.Message}");
            }

            return accounts;
        }

        public async Task<AccountEntity> GetAccountAsync(string account)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command =
                           new SqliteCommand("SELECT * FROM Accounts WHERE Account = @account", connection))
                    {
                        command.Parameters.AddWithValue("@account", account);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync()) return MapToAccount(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取賬號失敗: {ex.Message}");
            }

            return null;
        }

        public async Task<AccountEntity> GetDefaultAccountAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command =
                           new SqliteCommand("SELECT * FROM Accounts WHERE IsDefault = 1 LIMIT 1", connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync()) return MapToAccount(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取默認賬號失敗: {ex.Message}");
            }

            return null;
        }

        public async Task<int> SaveAccountAsync(AccountEntity account)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    if (account.Id == 0)
                        // 新增
                        using (var command = new SqliteCommand(@"
                            INSERT INTO Accounts (Account, AccessToken, Nickname, Avatar, LastLoginTime, IsDefault, CreatedAt, UpdatedAt)
                            VALUES (@account, @accessToken, @nickname, @avatar, @lastLoginTime, @isDefault, @createdAt, @updatedAt);
                            SELECT last_insert_rowid();", connection))
                        {
                            AddAccountParameters(command, account, now, now);
                            var result = await command.ExecuteScalarAsync();
                            return Convert.ToInt32(result);
                        }

                    // 更新
                    using (var command = new SqliteCommand(@"
                            UPDATE Accounts SET Account = @account, AccessToken = @accessToken, Nickname = @nickname, 
                                   Avatar = @avatar, LastLoginTime = @lastLoginTime, IsDefault = @isDefault, UpdatedAt = @updatedAt
                            WHERE Id = @id", connection))
                    {
                        AddAccountParameters(command, account, account.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), now);
                        command.Parameters.AddWithValue("@id", account.Id);
                        await command.ExecuteNonQueryAsync();
                        return account.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存賬號失敗: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteAccountAsync(int accountId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand("DELETE FROM Accounts WHERE Id = @id", connection))
                    {
                        command.Parameters.AddWithValue("@id", accountId);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除賬號失敗: {ex.Message}");
                throw;
            }
        }

        public async Task SetDefaultAccountAsync(int accountId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 清除所有默認標記
                        using (var command =
                               new SqliteCommand("UPDATE Accounts SET IsDefault = 0", connection, transaction))
                        {
                            await command.ExecuteNonQueryAsync();
                        }

                        // 設置新的默認賬號
                        using (var command = new SqliteCommand("UPDATE Accounts SET IsDefault = 1 WHERE Id = @id",
                                   connection, transaction))
                        {
                            command.Parameters.AddWithValue("@id", accountId);
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設置默認賬號失敗: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 伺服器管理

        public async Task<List<ServerEntity>> GetAllServersAsync()
        {
            var servers = new List<ServerEntity>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand("SELECT * FROM Servers ORDER BY IsDefault DESC, Name",
                               connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) servers.Add(MapToServer(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取所有伺服器失敗: {ex.Message}");
            }

            return servers;
        }

        public async Task<ServerEntity> GetServerAsync(int serverId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand("SELECT * FROM Servers WHERE Id = @id", connection))
                    {
                        command.Parameters.AddWithValue("@id", serverId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync()) return MapToServer(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取伺服器失敗: {ex.Message}");
            }

            return null;
        }

        public async Task<ServerEntity> GetDefaultServerAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command =
                           new SqliteCommand("SELECT * FROM Servers WHERE IsDefault = 1 LIMIT 1", connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync()) return MapToServer(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取默認伺服器失敗: {ex.Message}");
            }

            return null;
        }

        public async Task<int> SaveServerAsync(ServerEntity server)
        {
            try
            {
                Debug.WriteLine($"開始保存伺服器: {server.Name}, URL: {server.ServerUrl}");
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    int resultId;
                    if (server.Id == 0)
                        // 新增
                        using (var command = new SqliteCommand(@"
                            INSERT INTO Servers (Name, ServerUrl, ConnectionTimeout, EnableSsl, AutoReconnect, IsDefault, CreatedAt, UpdatedAt)
                            VALUES (@name, @serverUrl, @connectionTimeout, @enableSsl, @autoReconnect, @isDefault, @createdAt, @updatedAt);
                            SELECT last_insert_rowid();", connection))
                        {
                            AddServerParameters(command, server, now, now);
                            var result = await command.ExecuteScalarAsync();
                            resultId = Convert.ToInt32(result);
                            Debug.WriteLine($"插入新伺服器，ID: {resultId}");
                        }
                    else
                        // 更新
                        using (var command = new SqliteCommand(@"
                            UPDATE Servers SET Name = @name, ServerUrl = @serverUrl, ConnectionTimeout = @connectionTimeout,
                                   EnableSsl = @enableSsl, AutoReconnect = @autoReconnect, IsDefault = @isDefault, UpdatedAt = @updatedAt
                            WHERE Id = @id", connection))
                        {
                            AddServerParameters(command, server, server.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), now);
                            command.Parameters.AddWithValue("@id", server.Id);
                            var rowsAffected = await command.ExecuteNonQueryAsync();
                            Debug.WriteLine($"更新伺服器，影響行數: {rowsAffected}");
                            resultId = server.Id;
                        }

                    return resultId;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存伺服器失敗: {ex.Message}");
                Debug.WriteLine($"錯誤詳情: {ex.StackTrace}");
                throw;
            }
        }

        public async Task DeleteServerAsync(int serverId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand("DELETE FROM Servers WHERE Id = @id", connection))
                    {
                        command.Parameters.AddWithValue("@id", serverId);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除伺服器失敗: {ex.Message}");
                throw;
            }
        }

        public async Task SetDefaultServerAsync(int serverId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 清除所有默認標記
                        using (var command =
                               new SqliteCommand("UPDATE Servers SET IsDefault = 0", connection, transaction))
                        {
                            await command.ExecuteNonQueryAsync();
                        }

                        // 設置新的默認伺服器
                        using (var command = new SqliteCommand("UPDATE Servers SET IsDefault = 1 WHERE Id = @id",
                                   connection, transaction))
                        {
                            command.Parameters.AddWithValue("@id", serverId);
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設置默認伺服器失敗: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 聊天緩存

        public async Task<List<ChatCacheEntity>> GetAllChatCacheAsync()
        {
            var list = new List<ChatCacheEntity>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("SELECT * FROM ChatCache ORDER BY UpdatedAt DESC", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) list.Add(MapToChatCache(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取聊天緩存失敗: {ex.Message}");
            }
            return list;
        }

        public async Task<ChatCacheEntity> GetChatCacheAsync(long chatId, bool isGroup)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("SELECT * FROM ChatCache WHERE ChatId=@chatId AND IsGroup=@isGroup", connection))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
                        cmd.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync()) return MapToChatCache(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取聊天緩存失敗: {ex.Message}");
            }
            return null;
        }

        public async Task<int> SaveChatCacheAsync(ChatCacheEntity chatCache)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    using (var update = new SqliteCommand(@"UPDATE ChatCache SET Name=@name, Avatar=@avatar, LastMessage=@lastMessage, LastMessageTime=@lastMessageTime, UnreadCount=@unreadCount, LastMessageType=@lastMessageType, ChatData=@chatData, UpdatedAt=@updatedAt WHERE ChatId=@chatId AND IsGroup=@isGroup", connection))
                    {
                        AddChatCacheParams(update, chatCache, now, false);
                        var rows = await update.ExecuteNonQueryAsync();
                        if (rows > 0) return rows;
                    }

                    using (var insert = new SqliteCommand(@"INSERT INTO ChatCache (ChatId, IsGroup, Name, Avatar, LastMessage, LastMessageTime, UnreadCount, LastMessageType, ChatData, CreatedAt, UpdatedAt) VALUES (@chatId,@isGroup,@name,@avatar,@lastMessage,@lastMessageTime,@unreadCount,@lastMessageType,@chatData,@createdAt,@updatedAt); SELECT last_insert_rowid();", connection))
                    {
                        AddChatCacheParams(insert, chatCache, now, true);
                        var idObj = await insert.ExecuteScalarAsync();
                        return Convert.ToInt32(idObj);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存聊天緩存失敗: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteChatCacheAsync(long chatId, bool isGroup)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM ChatCache WHERE ChatId=@chatId AND IsGroup=@isGroup", connection))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
                        cmd.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除聊天緩存失敗: {ex.Message}");
            }
        }

        public async Task ClearChatCacheAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM ChatCache", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除聊天緩存失敗: {ex.Message}");
            }
        }

        #endregion

        #region 聯絡人緩存

        public async Task<List<ContactCacheEntity>> GetAllContactCacheAsync()
        {
            var list = new List<ContactCacheEntity>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("SELECT * FROM ContactCache ORDER BY UpdatedAt DESC", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) list.Add(MapToContactCache(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取聯絡人緩存失敗: {ex.Message}");
            }
            return list;
        }

        public async Task<ContactCacheEntity> GetContactCacheAsync(long userId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("SELECT * FROM ContactCache WHERE UserId=@userId", connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync()) return MapToContactCache(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取聯絡人緩存失敗: {ex.Message}");
            }
            return null;
        }

        public async Task<int> SaveContactCacheAsync(ContactCacheEntity contact)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    using (var update = new SqliteCommand(@"UPDATE ContactCache SET Nickname=@nickname, Remark=@remark, Avatar=@avatar, Status=@status, CategoryId=@categoryId, CategoryName=@categoryName, ContactData=@contactData, UpdatedAt=@updatedAt WHERE UserId=@userId", connection))
                    {
                        AddContactCacheParams(update, contact, now, false);
                        var rows = await update.ExecuteNonQueryAsync();
                        if (rows > 0) return rows;
                    }

                    using (var insert = new SqliteCommand(@"INSERT INTO ContactCache (UserId, Nickname, Remark, Avatar, Status, CategoryId, CategoryName, ContactData, CreatedAt, UpdatedAt) VALUES (@userId,@nickname,@remark,@avatar,@status,@categoryId,@categoryName,@contactData,@createdAt,@updatedAt); SELECT last_insert_rowid();", connection))
                    {
                        AddContactCacheParams(insert, contact, now, true);
                        var idObj = await insert.ExecuteScalarAsync();
                        return Convert.ToInt32(idObj);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存聯絡人緩存失敗: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteContactCacheAsync(long userId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM ContactCache WHERE UserId=@userId", connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除聯絡人緩存失敗: {ex.Message}");
            }
        }

        public async Task ClearContactCacheAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM ContactCache", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除聯絡人緩存失敗: {ex.Message}");
            }
        }

        #endregion

        #region 群組緩存

        public async Task<List<GroupCacheEntity>> GetAllGroupCacheAsync()
        {
            var list = new List<GroupCacheEntity>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("SELECT * FROM GroupCache ORDER BY UpdatedAt DESC", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) list.Add(MapToGroupCache(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取群組緩存失敗: {ex.Message}");
            }
            return list;
        }

        public async Task<GroupCacheEntity> GetGroupCacheAsync(long groupId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("SELECT * FROM GroupCache WHERE GroupId=@groupId", connection))
                    {
                        cmd.Parameters.AddWithValue("@groupId", groupId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync()) return MapToGroupCache(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取群組緩存失敗: {ex.Message}");
            }
            return null;
        }

        public async Task<int> SaveGroupCacheAsync(GroupCacheEntity group)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    using (var update = new SqliteCommand(@"UPDATE GroupCache SET GroupName=@groupName, GroupAvatar=@groupAvatar, MemberCount=@memberCount, GroupData=@groupData, UpdatedAt=@updatedAt WHERE GroupId=@groupId", connection))
                    {
                        AddGroupCacheParams(update, group, now, false);
                        var rows = await update.ExecuteNonQueryAsync();
                        if (rows > 0) return rows;
                    }

                    using (var insert = new SqliteCommand(@"INSERT INTO GroupCache (GroupId, GroupName, GroupAvatar, MemberCount, GroupData, CreatedAt, UpdatedAt) VALUES (@groupId,@groupName,@groupAvatar,@memberCount,@groupData,@createdAt,@updatedAt); SELECT last_insert_rowid();", connection))
                    {
                        AddGroupCacheParams(insert, group, now, true);
                        var idObj = await insert.ExecuteScalarAsync();
                        return Convert.ToInt32(idObj);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存群組緩存失敗: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteGroupCacheAsync(long groupId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM GroupCache WHERE GroupId=@groupId", connection))
                    {
                        cmd.Parameters.AddWithValue("@groupId", groupId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除群組緩存失敗: {ex.Message}");
                throw;
            }
        }

        public async Task ClearGroupCacheAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM GroupCache", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除群組緩存失敗: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 緩存統計

        public async Task<long> GetDatabaseSizeAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var databaseFile = await localFolder.TryGetItemAsync(DatabaseName) as StorageFile;
                if (databaseFile != null)
                {
                    var properties = await databaseFile.GetBasicPropertiesAsync();
                    return (long)properties.Size;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取數據庫大小失敗: {ex.Message}");
            }

            return 0;
        }

        public async Task<int> GetCachedItemsCountAsync(string tableName)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand($"SELECT COUNT(*) FROM {tableName}", connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取緩存項目數量失敗: {ex.Message}");
            }

            return 0;
        }

        public async Task ClearExpiredCacheAsync(DateTime expireTime)
        {
            try
            {
                var expire = expireTime.ToString("yyyy-MM-dd HH:mm:ss");
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    foreach (var table in new[] { "ChatCache", "ContactCache", "GroupCache" })
                    using (var cmd = new SqliteCommand($"DELETE FROM {table} WHERE UpdatedAt < @expire", connection))
                    {
                        cmd.Parameters.AddWithValue("@expire", expire);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除過期緩存失敗: {ex.Message}");
            }
        }

        #endregion

        #region 輔助方法

        private static async Task ExecuteAsync(SqliteConnection connection, string sql)
        {
            using (var command = new SqliteCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        private static AccountEntity MapToAccount(SqliteDataReader reader)
        {
            return new AccountEntity
            {
                Id = reader.GetInt32(0), // Id
                Account = reader.IsDBNull(1) ? "" : reader.GetString(1), // Account
                AccessToken = reader.IsDBNull(2) ? "" : reader.GetString(2), // AccessToken
                Nickname = reader.IsDBNull(3) ? "" : reader.GetString(3), // Nickname
                Avatar = reader.IsDBNull(4) ? "" : reader.GetString(4), // Avatar
                LastLoginTime =
                    DateTime.TryParse(reader.GetString(5), out var lastLogin)
                        ? lastLogin
                        : DateTime.Now, // LastLoginTime
                IsDefault = reader.GetInt32(6) == 1, // IsDefault
                CreatedAt = DateTime.TryParse(reader.GetString(7), out var created)
                    ? created
                    : DateTime.Now, // CreatedAt
                UpdatedAt = DateTime.TryParse(reader.GetString(8), out var updated)
                    ? updated
                    : DateTime.Now // UpdatedAt
            };
        }

        private static ServerEntity MapToServer(SqliteDataReader reader)
        {
            return new ServerEntity
            {
                Id = reader.GetInt32(0), // Id
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1), // Name
                ServerUrl = reader.IsDBNull(2) ? "" : reader.GetString(2), // ServerUrl
                ConnectionTimeout = reader.GetInt32(3), // ConnectionTimeout
                EnableSsl = reader.GetInt32(4) == 1, // EnableSsl
                AutoReconnect = reader.GetInt32(5) == 1, // AutoReconnect
                IsDefault = reader.GetInt32(6) == 1, // IsDefault
                CreatedAt = DateTime.TryParse(reader.GetString(7), out var created)
                    ? created
                    : DateTime.Now, // CreatedAt
                UpdatedAt = DateTime.TryParse(reader.GetString(8), out var updated)
                    ? updated
                    : DateTime.Now // UpdatedAt
            };
        }

        private static AppSettingsEntity MapToAppSettings(SqliteDataReader reader)
        {
            return new AppSettingsEntity
            {
                Id = reader.GetInt32(0), // Id
                Key = reader.IsDBNull(1) ? "" : reader.GetString(1), // Key
                Value = reader.IsDBNull(2) ? "" : reader.GetString(2), // Value
                CreatedAt = DateTime.TryParse(reader.GetString(3), out var created)
                    ? created
                    : DateTime.Now, // CreatedAt
                UpdatedAt = DateTime.TryParse(reader.GetString(4), out var updated)
                    ? updated
                    : DateTime.Now // UpdatedAt
            };
        }

        private static ChatCacheEntity MapToChatCache(SqliteDataReader reader)
        {
            return new ChatCacheEntity
            {
                Id = reader.GetInt32(0),
                ChatId = reader.GetInt64(1),
                IsGroup = reader.GetInt32(2) == 1,
                Name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Avatar = reader.IsDBNull(4) ? "" : reader.GetString(4),
                LastMessage = reader.IsDBNull(5) ? "" : reader.GetString(5),
                LastMessageTime = DateTime.TryParse(reader.GetString(6), out var lm) ? lm : DateTime.Now,
                UnreadCount = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                LastMessageType = reader.IsDBNull(8) ? "" : reader.GetString(8),
                ChatData = reader.IsDBNull(9) ? "" : reader.GetString(9),
                CreatedAt = DateTime.TryParse(reader.GetString(10), out var c) ? c : DateTime.Now,
                UpdatedAt = DateTime.TryParse(reader.GetString(11), out var u) ? u : DateTime.Now
            };
        }

        private static ContactCacheEntity MapToContactCache(SqliteDataReader reader)
        {
            // 動態取得欄位索引（解決舊資料庫 ALTER TABLE 追加欄位順序與新建資料庫不同的問題）
            try
            {
                var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    if (!index.ContainsKey(name)) index[name] = i;
                }

                Func<string, string> getString = col =>
                {
                    int idx; if (index.TryGetValue(col, out idx) && idx >= 0 && idx < reader.FieldCount && !reader.IsDBNull(idx)) return reader.GetString(idx); return "";
                };
                Func<string, long> getLong = col =>
                {
                    int idx; if (index.TryGetValue(col, out idx) && idx >= 0 && idx < reader.FieldCount && !reader.IsDBNull(idx)) return reader.GetInt64(idx); return 0L;
                };
                Func<string, int> getInt = col =>
                {
                    int idx; if (index.TryGetValue(col, out idx) && idx >= 0 && idx < reader.FieldCount && !reader.IsDBNull(idx)) return reader.GetInt32(idx); return 0;
                };

                DateTime parseDate(string s)
                {
                    DateTime dt; if (DateTime.TryParse(s, out dt)) return dt; return DateTime.Now;
                }

                var entity = new ContactCacheEntity
                {
                    Id = getInt("Id"),
                    UserId = getLong("UserId"),
                    Nickname = getString("Nickname"),
                    Remark = getString("Remark"),
                    Avatar = getString("Avatar"),
                    Status = getString("Status"),
                    // 可能存在兩種 schema:
                    // 新: CategoryId, CategoryName, ContactData, CreatedAt, UpdatedAt
                    // 舊升級: ContactData, CreatedAt, UpdatedAt, CategoryId, CategoryName
                    // 因此直接用欄位名取得即可
                    CategoryId = getInt("CategoryId"),
                    CategoryName = getString("CategoryName"),
                    ContactData = getString("ContactData"),
                    CreatedAt = parseDate(getString("CreatedAt")),
                    UpdatedAt = parseDate(getString("UpdatedAt"))
                };

                // 若 ContactData 看起來不是 JSON（不以 { 開頭），視為無效資料，避免後續反序列化錯誤
                if (!string.IsNullOrEmpty(entity.ContactData) && entity.ContactData.TrimStart().StartsWith("{") == false)
                {
                    Debug.WriteLine($"[UwpDatabaseService] 偵測到非 JSON ContactData，已忽略。UserId={entity.UserId}, Raw='{entity.ContactData}'");
                    entity.ContactData = string.Empty;
                }

                return entity;
            }
            catch (Exception mapEx)
            {
                Debug.WriteLine($"MapToContactCache 動態映射失敗: {mapEx.Message}");
            }

            // 回退：舊固定索引方式（盡量不再使用）
            try
            {
                return new ContactCacheEntity
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                    Nickname = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Remark = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Avatar = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Status = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    CategoryId = 0,
                    CategoryName = "",
                    ContactData = "",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MapToContactCache 回退映射失敗: {ex.Message}");
                return new ContactCacheEntity();
            }
        }

        private static GroupCacheEntity MapToGroupCache(SqliteDataReader reader)
        {
            return new GroupCacheEntity
            {
                Id = reader.GetInt32(0),
                GroupId = reader.GetInt64(1),
                GroupName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                GroupAvatar = reader.IsDBNull(3) ? "" : reader.GetString(3),
                MemberCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                GroupData = reader.IsDBNull(5) ? "" : reader.GetString(5),
                CreatedAt = DateTime.TryParse(reader.GetString(6), out var c) ? c : DateTime.Now,
                UpdatedAt = DateTime.TryParse(reader.GetString(7), out var u) ? u : DateTime.Now
            };
        }

        private static void AddAccountParameters(SqliteCommand command, AccountEntity account, string createdAt,
            string updatedAt)
        {
            command.Parameters.AddWithValue("@account", account.Account ?? "");
            command.Parameters.AddWithValue("@accessToken", account.AccessToken ?? "");
            command.Parameters.AddWithValue("@nickname", account.Nickname ?? "");
            command.Parameters.AddWithValue("@avatar", account.Avatar ?? "");
            command.Parameters.AddWithValue("@lastLoginTime", account.LastLoginTime.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@isDefault", account.IsDefault ? 1 : 0);
            command.Parameters.AddWithValue("@createdAt", createdAt);
            command.Parameters.AddWithValue("@updatedAt", updatedAt);
        }

        private static void AddServerParameters(SqliteCommand command, ServerEntity server, string createdAt,
            string updatedAt)
        {
            command.Parameters.AddWithValue("@name", server.Name ?? "");
            command.Parameters.AddWithValue("@serverUrl", server.ServerUrl ?? "");
            command.Parameters.AddWithValue("@connectionTimeout", server.ConnectionTimeout);
            command.Parameters.AddWithValue("@enableSsl", server.EnableSsl ? 1 : 0);
            command.Parameters.AddWithValue("@autoReconnect", server.AutoReconnect ? 1 : 0);
            command.Parameters.AddWithValue("@isDefault", server.IsDefault ? 1 : 0);
            command.Parameters.AddWithValue("@createdAt", createdAt);
            command.Parameters.AddWithValue("@updatedAt", updatedAt);
        }

        private static void AddChatCacheParams(SqliteCommand cmd, ChatCacheEntity chat, string now, bool includeCreate)
        {
            cmd.Parameters.AddWithValue("@chatId", chat.ChatId);
            cmd.Parameters.AddWithValue("@isGroup", chat.IsGroup ? 1 : 0);
            cmd.Parameters.AddWithValue("@name", chat.Name ?? "");
            cmd.Parameters.AddWithValue("@avatar", chat.Avatar ?? "");
            cmd.Parameters.AddWithValue("@lastMessage", chat.LastMessage ?? "");
            cmd.Parameters.AddWithValue("@lastMessageTime", chat.LastMessageTime.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@unreadCount", chat.UnreadCount);
            cmd.Parameters.AddWithValue("@lastMessageType", chat.LastMessageType ?? "");
            cmd.Parameters.AddWithValue("@chatData", chat.ChatData ?? "");
            if (includeCreate) cmd.Parameters.AddWithValue("@createdAt", now);
            cmd.Parameters.AddWithValue("@updatedAt", now);
        }

        private static void AddContactCacheParams(SqliteCommand cmd, ContactCacheEntity c, string now, bool includeCreate)
        {
            cmd.Parameters.AddWithValue("@userId", c.UserId);
            cmd.Parameters.AddWithValue("@nickname", c.Nickname ?? "");
            cmd.Parameters.AddWithValue("@remark", c.Remark ?? "");
            cmd.Parameters.AddWithValue("@avatar", c.Avatar ?? "");
            cmd.Parameters.AddWithValue("@status", c.Status ?? "");
            cmd.Parameters.AddWithValue("@categoryId", c.CategoryId);
            cmd.Parameters.AddWithValue("@categoryName", c.CategoryName ?? "");
            cmd.Parameters.AddWithValue("@contactData", c.ContactData ?? "");
            if (includeCreate) cmd.Parameters.AddWithValue("@createdAt", now);
            cmd.Parameters.AddWithValue("@updatedAt", now);
        }

        private static void AddGroupCacheParams(SqliteCommand cmd, GroupCacheEntity g, string now, bool includeCreate)
        {
            cmd.Parameters.AddWithValue("@groupId", g.GroupId);
            cmd.Parameters.AddWithValue("@groupName", g.GroupName ?? "");
            cmd.Parameters.AddWithValue("@groupAvatar", g.GroupAvatar ?? "");
            cmd.Parameters.AddWithValue("@memberCount", g.MemberCount);
            cmd.Parameters.AddWithValue("@groupData", g.GroupData ?? "");
            if (includeCreate) cmd.Parameters.AddWithValue("@createdAt", now);
            cmd.Parameters.AddWithValue("@updatedAt", now);
        }

        #endregion

        #region 好友分類緩存
        public async Task<List<CategoryCacheEntity>> GetAllCategoryCacheAsync()
        {
            var list = new List<CategoryCacheEntity>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("SELECT * FROM CategoryCache ORDER BY SortOrder, CategoryName", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new CategoryCacheEntity
                            {
                                Id = reader.GetInt32(0),
                                CategoryId = reader.GetInt64(1),
                                CategoryName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                SortOrder = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                TotalCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                CreatedAt = DateTime.TryParse(reader.GetString(5), out var c) ? c : DateTime.Now,
                                UpdatedAt = DateTime.TryParse(reader.GetString(6), out var u) ? u : DateTime.Now
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"獲取分類緩存失敗: {ex.Message}"); }
            return list;
        }

        public async Task<int> SaveCategoryCacheAsync(CategoryCacheEntity cat)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    using (var update = new SqliteCommand(@"UPDATE CategoryCache SET CategoryName=@name, SortOrder=@sort, TotalCount=@total, UpdatedAt=@updated WHERE CategoryId=@id", connection))
                    {
                        update.Parameters.AddWithValue("@name", cat.CategoryName ?? "");
                        update.Parameters.AddWithValue("@sort", cat.SortOrder);
                        update.Parameters.AddWithValue("@total", cat.TotalCount);
                        update.Parameters.AddWithValue("@updated", now);
                        update.Parameters.AddWithValue("@id", cat.CategoryId);
                        var rows = await update.ExecuteNonQueryAsync();
                        if (rows > 0) return rows;
                    }
                    using (var insert = new SqliteCommand(@"INSERT INTO CategoryCache (CategoryId, CategoryName, SortOrder, TotalCount, CreatedAt, UpdatedAt) VALUES (@id,@name,@sort,@total,@created,@updated); SELECT last_insert_rowid();", connection))
                    {
                        insert.Parameters.AddWithValue("@id", cat.CategoryId);
                        insert.Parameters.AddWithValue("@name", cat.CategoryName ?? "");
                        insert.Parameters.AddWithValue("@sort", cat.SortOrder);
                        insert.Parameters.AddWithValue("@total", cat.TotalCount);
                        insert.Parameters.AddWithValue("@created", now);
                        insert.Parameters.AddWithValue("@updated", now);
                        var idObj = await insert.ExecuteScalarAsync();
                        return Convert.ToInt32(idObj);
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"保存分類緩存失敗: {ex.Message}"); throw; }
        }

        public async Task ClearCategoryCacheAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand("DELETE FROM CategoryCache", connection))
                    { await cmd.ExecuteNonQueryAsync(); }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"清除分類緩存失敗: {ex.Message}"); }
        }
        #endregion
    }
}