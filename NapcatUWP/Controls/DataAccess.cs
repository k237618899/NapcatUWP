using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.Storage;
using Microsoft.Data.Sqlite;
using NapcatUWP.Models;
using NapcatUWP.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NapcatUWP.Controls
{
    public class DataAccess
    {
        public static async void InitializeDatabase()
        {
            await ApplicationData.Current.LocalFolder.CreateFileAsync("setting.db",
                CreationCollisionOption.OpenIfExists);
            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (var db = new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                // 創建原有的設置表
                var tableCommand = "CREATE TABLE IF NOT " +
                                   "EXISTS AppSettings (Primary_Key NVARCHAR(2048) PRIMARY KEY, " +
                                   "Text_Entry NVARCHAR(2048) NULL)";

                var createTable = new SqliteCommand(tableCommand, db);
                createTable.ExecuteReader();

                // 創建群組表
                var createGroupsTable = @"
            CREATE TABLE IF NOT EXISTS Groups (
                GroupId INTEGER PRIMARY KEY,
                GroupName TEXT NOT NULL,
                GroupRemark TEXT,
                MemberCount INTEGER,
                MaxMemberCount INTEGER,
                GroupAllShut INTEGER,
                LastUpdated TEXT
            )";

                var createGroupsTableCmd = new SqliteCommand(createGroupsTable, db);
                createGroupsTableCmd.ExecuteNonQuery();
                // 創建群組成員表
                var createGroupMembersTable = @"
                CREATE TABLE IF NOT EXISTS GroupMembers (
                    GroupId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    Nickname TEXT,
                    Card TEXT,
                    Sex TEXT,
                    Age INTEGER,
                    Area TEXT,
                    JoinTime INTEGER,
                    LastSentTime INTEGER,
                    Level TEXT,
                    Role TEXT,
                    Unfriendly INTEGER,
                    Title TEXT,
                    TitleExpireTime INTEGER,
                    CardChangeable INTEGER,
                    ShutUpTimestamp INTEGER,
                    LastUpdated TEXT,
                    PRIMARY KEY (GroupId, UserId)
                )";

                var createGroupMembersTableCmd = new SqliteCommand(createGroupMembersTable, db);
                createGroupMembersTableCmd.ExecuteNonQuery();

                // 創建群組成員索引以提高查詢性能
                var createGroupMemberIndexCmd = new SqliteCommand(@"
                CREATE INDEX IF NOT EXISTS idx_group_members 
                ON GroupMembers(GroupId, UserId)", db);
                createGroupMemberIndexCmd.ExecuteNonQuery();

                // 創建好友分類表
                var createFriendCategoriesTable = @"
            CREATE TABLE IF NOT EXISTS FriendCategories (
                CategoryId INTEGER PRIMARY KEY,
                CategorySortId INTEGER,
                CategoryName TEXT NOT NULL,
                CategoryMbCount INTEGER,
                OnlineCount INTEGER,
                LastUpdated TEXT
            )";

                var createFriendCategoriesTableCmd = new SqliteCommand(createFriendCategoriesTable, db);
                createFriendCategoriesTableCmd.ExecuteNonQuery();

                // 創建好友表
                var createFriendsTable = @"
            CREATE TABLE IF NOT EXISTS Friends (
                UserId INTEGER PRIMARY KEY,
                Qid TEXT,
                LongNick TEXT,
                BirthdayYear INTEGER,
                BirthdayMonth INTEGER,
                BirthdayDay INTEGER,
                Age INTEGER,
                Sex TEXT,
                Email TEXT,
                PhoneNum TEXT,
                CategoryId INTEGER,
                RichTime INTEGER,
                Uid TEXT,
                Uin TEXT,
                Nick TEXT,
                Remark TEXT,
                Nickname TEXT,
                Level INTEGER,
                LastUpdated TEXT
            )";

                var createFriendsTableCmd = new SqliteCommand(createFriendsTable, db);
                createFriendsTableCmd.ExecuteNonQuery();

                // 修改消息表 - 使用服務器的 message_id 作為主鍵
                var createMessagesTable = @"
            CREATE TABLE IF NOT EXISTS Messages (
                MessageId INTEGER PRIMARY KEY,
                ChatId INTEGER NOT NULL,
                IsGroup INTEGER NOT NULL,
                Content TEXT NOT NULL,
                MessageType TEXT NOT NULL DEFAULT 'text',
                SenderId INTEGER NOT NULL,
                SenderName TEXT NOT NULL,
                IsFromMe INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                SegmentsJson TEXT,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            )";

                var createMessagesTableCmd = new SqliteCommand(createMessagesTable, db);
                createMessagesTableCmd.ExecuteNonQuery();

                // 檢查是否需要遷移舊表結構
                try
                {
                    // 檢查是否存在舊的自增主鍵表
                    var checkOldTableCmd = new SqliteCommand(@"
                SELECT name FROM sqlite_master 
                WHERE type='table' AND name='Messages_Old'", db);
                    var oldTableExists = checkOldTableCmd.ExecuteScalar();

                    if (oldTableExists == null)
                    {
                        // 檢查當前表是否使用 AUTOINCREMENT
                        var checkCurrentTableCmd = new SqliteCommand(@"
                    SELECT sql FROM sqlite_master 
                    WHERE type='table' AND name='Messages'", db);
                        var currentTableSql = checkCurrentTableCmd.ExecuteScalar()?.ToString();

                        if (!string.IsNullOrEmpty(currentTableSql) && currentTableSql.Contains("AUTOINCREMENT"))
                        {
                            Debug.WriteLine("檢測到舊的消息表結構，開始遷移...");

                            // 重命名舊表
                            var renameOldTableCmd =
                                new SqliteCommand("ALTER TABLE Messages RENAME TO Messages_Old", db);
                            renameOldTableCmd.ExecuteNonQuery();

                            // 創建新表
                            var createNewTableCmd = new SqliteCommand(createMessagesTable, db);
                            createNewTableCmd.ExecuteNonQuery();

                            Debug.WriteLine("消息表結構遷移完成");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"檢查消息表結構時發生錯誤: {ex.Message}");
                }

                // 檢查並添加 SegmentsJson 列（為了向後兼容）
                try
                {
                    var addColumnCmd = new SqliteCommand(@"
                ALTER TABLE Messages ADD COLUMN SegmentsJson TEXT", db);
                    addColumnCmd.ExecuteNonQuery();
                    Debug.WriteLine("成功添加 SegmentsJson 列到 Messages 表");
                }
                catch (SqliteException ex)
                {
                    // 如果列已存在，會拋出異常，這是正常的
                    if (ex.Message.Contains("duplicate column name"))
                        Debug.WriteLine("SegmentsJson 列已存在");
                    else
                        Debug.WriteLine($"添加 SegmentsJson 列時發生錯誤: {ex.Message}");
                }

                // 創建消息索引以提高查詢性能
                var createMessageIndexCmd = new SqliteCommand(@"
            CREATE INDEX IF NOT EXISTS idx_messages_chat 
            ON Messages(ChatId, IsGroup, Timestamp)", db);
                createMessageIndexCmd.ExecuteNonQuery();

                // 創建聊天列表緩存表
                var createChatListCacheTable = @"
                CREATE TABLE IF NOT EXISTS ChatListCache (
                    CacheId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Account TEXT NOT NULL,
                    ChatId INTEGER NOT NULL,
                    ChatName TEXT NOT NULL,
                    LastMessage TEXT,
                    LastTime TEXT,
                    UnreadCount INTEGER DEFAULT 0,
                    AvatarColor TEXT,
                    IsGroup INTEGER NOT NULL,
                    MemberCount INTEGER DEFAULT 0,
                    LastUpdated TEXT NOT NULL,
                    UNIQUE(Account, ChatId, IsGroup)
                )";

                var createChatListCacheTableCmd = new SqliteCommand(createChatListCacheTable, db);
                createChatListCacheTableCmd.ExecuteNonQuery();
                // 在 InitializeDatabase 方法的最後添加
                FixDatabaseTimestamps();
                Debug.WriteLine("數據庫初始化完成");
            }
        }

        public static void InitInsert()
        {
            Insert("Server", "http://140.83.32.184:3000");
            Insert("Account", "");
            Insert("Token", "");
        }

        public static void Insert(string inputKey, string inputText)
        {
            if (inputKey == null || inputKey.Equals(string.Empty)) return;
            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (var db = new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                var insertCommand = new SqliteCommand();
                insertCommand.Connection = db;

                // Use parameterized query to prevent SQL injection attacks
                insertCommand.CommandText = "INSERT or IGNORE INTO AppSettings VALUES (@Key, @Entry);";
                insertCommand.Parameters.AddWithValue("@Key", inputKey);
                insertCommand.Parameters.AddWithValue("@Entry", inputText);

                insertCommand.ExecuteReader();
            }
        }

        public static NameValueCollection GetAllDatas()
        {
            var entries = new NameValueCollection();

            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (var db = new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                var selectCommand = new SqliteCommand
                    ("SELECT * from AppSettings", db);

                var query = selectCommand.ExecuteReader();

                while (query.Read()) entries.Add(query.GetString(0), query.GetString(1));
            }

            return entries;
        }

        public static void UpdateSetting(string name, string value)
        {
            if (name == null || name.Equals(string.Empty))
                return;
            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (var db = new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                var updateCommand =
                    new SqliteCommand("UPDATE AppSettings SET Text_Entry=@Entry WHERE Primary_Key=@Key", db);
                updateCommand.Parameters.AddWithValue("@Entry", value);
                updateCommand.Parameters.AddWithValue("@Key", name);
                updateCommand.ExecuteNonQuery();
            }
        }

        #region 群組數據操作

        public static void SaveGroups(List<GroupInfo> groups)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    // 清空現有群組數據
                    var deleteCommand = new SqliteCommand("DELETE FROM Groups", db);
                    deleteCommand.ExecuteNonQuery();

                    // 插入新的群組數據
                    var insertCommand = new SqliteCommand();
                    insertCommand.Connection = db;
                    insertCommand.CommandText = @"
                        INSERT INTO Groups (GroupId, GroupName, GroupRemark, MemberCount, MaxMemberCount, GroupAllShut, LastUpdated) 
                        VALUES (@groupId, @groupName, @groupRemark, @memberCount, @maxMemberCount, @groupAllShut, @lastUpdated)";

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

                    Debug.WriteLine($"成功保存 {groups.Count} 個群組到數據庫");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存群組數據錯誤: {ex.Message}");
            }
        }

        public static List<GroupInfo> GetAllGroups()
        {
            var groups = new List<GroupInfo>();
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand("SELECT * FROM Groups ORDER BY GroupName", db);

                    using (var query = selectCommand.ExecuteReader())
                    {
                        while (query.Read())
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取群組數據錯誤: {ex.Message}");
            }

            return groups;
        }

        #endregion

        #region 好友數據操作

        public static void SaveFriendsWithCategories(List<FriendCategory> categories)
        {
            try
            {
                Debug.WriteLine($"開始保存好友數據，共 {categories.Count} 個分類");

                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    // 開始事務
                    using (var transaction = db.BeginTransaction())
                    {
                        try
                        {
                            // 清空現有數據
                            var deleteCmd1 = new SqliteCommand("DELETE FROM Friends", db, transaction);
                            var deletedFriends = deleteCmd1.ExecuteNonQuery();
                            Debug.WriteLine($"刪除了 {deletedFriends} 個舊好友記錄");

                            var deleteCmd2 = new SqliteCommand("DELETE FROM FriendCategories", db, transaction);
                            var deletedCategories = deleteCmd2.ExecuteNonQuery();
                            Debug.WriteLine($"刪除了 {deletedCategories} 個舊分類記錄");

                            // 插入分類數據
                            var insertCategoryCommand = new SqliteCommand();
                            insertCategoryCommand.Connection = db;
                            insertCategoryCommand.Transaction = transaction;
                            insertCategoryCommand.CommandText = @"
                        INSERT INTO FriendCategories (CategoryId, CategorySortId, CategoryName, CategoryMbCount, OnlineCount, LastUpdated) 
                        VALUES (@categoryId, @categorySortId, @categoryName, @categoryMbCount, @onlineCount, @lastUpdated)";

                            // 使用 INSERT OR REPLACE 來處理重複的 UserId
                            var insertFriendCommand = new SqliteCommand();
                            insertFriendCommand.Connection = db;
                            insertFriendCommand.Transaction = transaction;
                            insertFriendCommand.CommandText = @"
                        INSERT OR REPLACE INTO Friends (UserId, Qid, LongNick, BirthdayYear, BirthdayMonth, BirthdayDay, Age, Sex, Email, PhoneNum, 
                                            CategoryId, RichTime, Uid, Uin, Nick, Remark, Nickname, Level, LastUpdated) 
                        VALUES (@userId, @qid, @longNick, @birthdayYear, @birthdayMonth, @birthdayDay, @age, @sex, @email, @phoneNum, 
                                @categoryId, @richTime, @uid, @uin, @nick, @remark, @nickname, @level, @lastUpdated)";

                            // 用於追蹤已處理的好友，避免重複
                            var processedFriends = new HashSet<long>();

                            foreach (var category in categories)
                            {
                                Debug.WriteLine(
                                    $"保存分類: ID={category.CategoryId}, Name={category.CategoryName}, 好友數={category.BuddyList?.Count ?? 0}");

                                // 插入分類
                                insertCategoryCommand.Parameters.Clear();
                                insertCategoryCommand.Parameters.AddWithValue("@categoryId", category.CategoryId);
                                insertCategoryCommand.Parameters.AddWithValue("@categorySortId",
                                    category.CategorySortId);
                                insertCategoryCommand.Parameters.AddWithValue("@categoryName",
                                    category.CategoryName ?? "");
                                insertCategoryCommand.Parameters.AddWithValue("@categoryMbCount",
                                    category.CategoryMbCount);
                                insertCategoryCommand.Parameters.AddWithValue("@onlineCount", category.OnlineCount);
                                insertCategoryCommand.Parameters.AddWithValue("@lastUpdated",
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                                insertCategoryCommand.ExecuteNonQuery();

                                // 插入該分類下的好友
                                if (category.BuddyList != null)
                                {
                                    var categoryFriendCount = 0;
                                    foreach (var friend in category.BuddyList)
                                    {
                                        // 檢查是否已經處理過這個好友（現在 UserId 不應該是 0）
                                        if (friend.UserId == 0)
                                        {
                                            Debug.WriteLine($"警告: 好友 {friend.Nickname} 的 UserId 為 0，跳過");
                                            continue;
                                        }

                                        if (processedFriends.Contains(friend.UserId))
                                        {
                                            Debug.WriteLine(
                                                $"跳過重複的好友 UserId: {friend.UserId}, Name: {friend.Nickname}");
                                            continue;
                                        }

                                        try
                                        {
                                            insertFriendCommand.Parameters.Clear();
                                            insertFriendCommand.Parameters.AddWithValue("@userId", friend.UserId);
                                            insertFriendCommand.Parameters.AddWithValue("@qid", friend.Qid ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@longNick",
                                                friend.LongNick ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@birthdayYear",
                                                friend.BirthdayYear);
                                            insertFriendCommand.Parameters.AddWithValue("@birthdayMonth",
                                                friend.BirthdayMonth);
                                            insertFriendCommand.Parameters.AddWithValue("@birthdayDay",
                                                friend.BirthdayDay);
                                            insertFriendCommand.Parameters.AddWithValue("@age", friend.Age);
                                            insertFriendCommand.Parameters.AddWithValue("@sex", friend.Sex ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@email", friend.Email ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@phoneNum",
                                                friend.PhoneNum ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@categoryId",
                                                friend.CategoryId);
                                            insertFriendCommand.Parameters.AddWithValue("@richTime", friend.RichTime);
                                            insertFriendCommand.Parameters.AddWithValue("@uid", friend.Uid ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@uin", friend.Uin ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@nick", friend.Nick ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@remark", friend.Remark ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@nickname",
                                                friend.Nickname ?? "");
                                            insertFriendCommand.Parameters.AddWithValue("@level", friend.Level);
                                            insertFriendCommand.Parameters.AddWithValue("@lastUpdated",
                                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                                            var result = insertFriendCommand.ExecuteNonQuery();

                                            // 標記此好友已處理
                                            processedFriends.Add(friend.UserId);
                                            categoryFriendCount++;

                                            if (categoryFriendCount <= 3) // 只記錄前3個好友的保存情況
                                                Debug.WriteLine(
                                                    $"  保存好友 {categoryFriendCount}: UserId={friend.UserId}, Nickname={friend.Nickname}, CategoryId={friend.CategoryId}, Result={result}");
                                        }
                                        catch (Exception friendEx)
                                        {
                                            Debug.WriteLine($"保存好友 UserId: {friend.UserId} 時發生錯誤: {friendEx.Message}");
                                        }
                                    }

                                    Debug.WriteLine($"分類 {category.CategoryName} 成功保存 {categoryFriendCount} 個好友");
                                }
                            }

                            // 提交事務
                            transaction.Commit();

                            var totalFriends = processedFriends.Count;
                            Debug.WriteLine($"成功保存 {categories.Count} 個分類和 {totalFriends} 個好友到數據庫");
                        }
                        catch (Exception transactionEx)
                        {
                            // 如果發生錯誤，回滾事務
                            Debug.WriteLine($"事務執行時發生錯誤，正在回滾: {transactionEx.Message}");
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存好友數據錯誤: {ex.Message}");
                Debug.WriteLine($"錯誤堆疊: {ex.StackTrace}");
            }
        }

        public static List<FriendCategory> GetAllFriendsWithCategories()
        {
            var categories = new List<FriendCategory>();
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    // 首先檢查數據庫中實際有多少好友和分類
                    var countFriendsCmd = new SqliteCommand("SELECT COUNT(*) FROM Friends", db);
                    var friendsCount = Convert.ToInt32(countFriendsCmd.ExecuteScalar());
                    Debug.WriteLine($"數據庫中總共有 {friendsCount} 個好友");

                    var countCategoriesCmd = new SqliteCommand("SELECT COUNT(*) FROM FriendCategories", db);
                    var categoriesCount = Convert.ToInt32(countCategoriesCmd.ExecuteScalar());
                    Debug.WriteLine($"數據庫中總共有 {categoriesCount} 個分類");

                    // 檢查 Friends 表中的 CategoryId 分佈
                    var categoryDistCmd =
                        new SqliteCommand("SELECT CategoryId, COUNT(*) FROM Friends GROUP BY CategoryId", db);
                    using (var distQuery = categoryDistCmd.ExecuteReader())
                    {
                        Debug.WriteLine("好友分類分佈:");
                        while (distQuery.Read())
                            Debug.WriteLine($"CategoryId: {distQuery.GetInt64(0)}, 好友數: {distQuery.GetInt32(1)}");
                    }

                    // 獲取所有分類
                    var selectCategoriesCommand =
                        new SqliteCommand("SELECT * FROM FriendCategories ORDER BY CategorySortId", db);

                    using (var categoryQuery = selectCategoriesCommand.ExecuteReader())
                    {
                        while (categoryQuery.Read())
                        {
                            var category = new FriendCategory
                            {
                                CategoryId = Convert.ToInt64(categoryQuery["CategoryId"]),
                                CategorySortId = Convert.ToInt64(categoryQuery["CategorySortId"]),
                                CategoryName = Convert.ToString(categoryQuery["CategoryName"]) ?? "",
                                CategoryMbCount = Convert.ToInt32(categoryQuery["CategoryMbCount"]),
                                OnlineCount = Convert.ToInt32(categoryQuery["OnlineCount"]),
                                BuddyList = new List<FriendInfo>()
                            };
                            categories.Add(category);
                            Debug.WriteLine($"讀取分類: ID={category.CategoryId}, Name={category.CategoryName}");
                        }
                    } // 關閉第一個 DataReader

                    // 為每個分類獲取好友
                    foreach (var category in categories)
                    {
                        Debug.WriteLine($"正在查詢分類 {category.CategoryName} (ID: {category.CategoryId}) 的好友");

                        var selectFriendsCommand =
                            new SqliteCommand("SELECT * FROM Friends WHERE CategoryId = @categoryId ORDER BY Nickname",
                                db);
                        selectFriendsCommand.Parameters.AddWithValue("@categoryId", category.CategoryId);

                        using (var friendQuery = selectFriendsCommand.ExecuteReader())
                        {
                            var friendCount = 0;
                            while (friendQuery.Read())
                            {
                                var friend = new FriendInfo
                                {
                                    UserId = Convert.ToInt64(friendQuery["UserId"]),
                                    Qid = friendQuery.IsDBNull(friendQuery.GetOrdinal("Qid"))
                                        ? ""
                                        : Convert.ToString(friendQuery["Qid"]),
                                    LongNick = friendQuery.IsDBNull(friendQuery.GetOrdinal("LongNick"))
                                        ? ""
                                        : Convert.ToString(friendQuery["LongNick"]),
                                    BirthdayYear = Convert.ToInt32(friendQuery["BirthdayYear"]),
                                    BirthdayMonth = Convert.ToInt32(friendQuery["BirthdayMonth"]),
                                    BirthdayDay = Convert.ToInt32(friendQuery["BirthdayDay"]),
                                    Age = Convert.ToInt32(friendQuery["Age"]),
                                    Sex = friendQuery.IsDBNull(friendQuery.GetOrdinal("Sex"))
                                        ? ""
                                        : Convert.ToString(friendQuery["Sex"]),
                                    Email = friendQuery.IsDBNull(friendQuery.GetOrdinal("Email"))
                                        ? ""
                                        : Convert.ToString(friendQuery["Email"]),
                                    PhoneNum = friendQuery.IsDBNull(friendQuery.GetOrdinal("PhoneNum"))
                                        ? ""
                                        : Convert.ToString(friendQuery["PhoneNum"]),
                                    CategoryId = Convert.ToInt64(friendQuery["CategoryId"]),
                                    RichTime = Convert.ToInt64(friendQuery["RichTime"]),
                                    Uid = friendQuery.IsDBNull(friendQuery.GetOrdinal("Uid"))
                                        ? ""
                                        : Convert.ToString(friendQuery["Uid"]),
                                    Uin = friendQuery.IsDBNull(friendQuery.GetOrdinal("Uin"))
                                        ? ""
                                        : Convert.ToString(friendQuery["Uin"]),
                                    Nick = friendQuery.IsDBNull(friendQuery.GetOrdinal("Nick"))
                                        ? ""
                                        : Convert.ToString(friendQuery["Nick"]),
                                    Remark = friendQuery.IsDBNull(friendQuery.GetOrdinal("Remark"))
                                        ? ""
                                        : Convert.ToString(friendQuery["Remark"]),
                                    Nickname = friendQuery.IsDBNull(friendQuery.GetOrdinal("Nickname"))
                                        ? ""
                                        : Convert.ToString(friendQuery["Nickname"]),
                                    Level = Convert.ToInt32(friendQuery["Level"])
                                };

                                category.BuddyList.Add(friend);
                                friendCount++;

                                if (friendCount <= 3) // 只記錄前3個好友的詳細信息，避免日誌過多
                                    Debug.WriteLine(
                                        $"  好友 {friendCount}: UserId={friend.UserId}, Nick={friend.Nick}, Nickname={friend.Nickname}, CategoryId={friend.CategoryId}");
                            }

                            Debug.WriteLine($"分類 {category.CategoryName} 查詢到 {friendCount} 個好友");
                        }
                    }

                    Debug.WriteLine($"總共讀取了 {categories.Count} 個分類");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取好友數據錯誤: {ex.Message}");
                Debug.WriteLine($"錯誤堆疊: {ex.StackTrace}");
            }

            return categories;
        }

        #endregion

        #region 根據ID查找名稱的輔助方法

        public static string GetGroupNameById(long groupId)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand("SELECT GroupName FROM Groups WHERE GroupId = @groupId", db);
                    selectCommand.Parameters.AddWithValue("@groupId", groupId);

                    var result = selectCommand.ExecuteScalar();
                    return result?.ToString() ?? $"群組 {groupId}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取群組名稱錯誤: {ex.Message}");
                return $"群組 {groupId}";
            }
        }

        // 在 GetFriendNameById 方法中添加更好的錯誤處理
        public static string GetFriendNameById(long userId)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    // 優先使用備註，然後昵稱，最後使用 Nick
                    var selectCommand = new SqliteCommand(@"
                SELECT 
                    CASE 
                        WHEN Remark IS NOT NULL AND Remark <> '' THEN Remark
                        WHEN Nickname IS NOT NULL AND Nickname <> '' THEN Nickname
                        ELSE Nick
                    END as DisplayName 
                FROM Friends WHERE UserId = @userId", db);
                    selectCommand.Parameters.AddWithValue("@userId", userId);

                    var result = selectCommand.ExecuteScalar();
                    if (result != null && !string.IsNullOrEmpty(result.ToString())) return result.ToString();

                    // 如果在好友列表中沒找到，嘗試查找是否有相關的聊天記錄
                    var chatCommand = new SqliteCommand(@"
                SELECT DISTINCT SenderName 
                FROM Messages 
                WHERE ChatId = @userId AND IsGroup = 0 AND IsFromMe = 0 
                ORDER BY Timestamp DESC 
                LIMIT 1", db);
                    chatCommand.Parameters.AddWithValue("@userId", userId);

                    var chatResult = chatCommand.ExecuteScalar();
                    if (chatResult != null && !string.IsNullOrEmpty(chatResult.ToString()))
                    {
                        Debug.WriteLine($"從聊天記錄中找到用戶名: {chatResult}");
                        return chatResult.ToString();
                    }

                    return $"用戶 {userId}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取好友名稱錯誤: {ex.Message}");
                return $"用戶 {userId}";
            }
        }

        #endregion

        #region 消息數據操作

        /// <summary>
        ///     保存消息到數據庫 - 使用服務器 message_id 作為主鍵，避免重複
        /// </summary>
        public static void SaveMessage(long messageId, long chatId, bool isGroup, string content, string messageType,
            long senderId, string senderName, bool isFromMe, DateTime timestamp, List<MessageSegment> segments = null)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    // 序列化消息段
                    var segmentsJson = "";
                    if (segments != null && segments.Count > 0)
                        try
                        {
                            segmentsJson = JsonConvert.SerializeObject(segments);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"序列化消息段時發生錯誤: {ex.Message}");
                        }

                    // 使用 INSERT OR IGNORE 防止重複插入相同的 message_id
                    var insertCommand = new SqliteCommand(@"
                INSERT OR IGNORE INTO Messages (MessageId, ChatId, IsGroup, Content, MessageType, SenderId, SenderName, IsFromMe, Timestamp, SegmentsJson) 
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
                        Debug.WriteLine(
                            $"保存新消息: MessageId={messageId}, ChatId={chatId}, IsGroup={isGroup}, Content={content}, Sender={senderName}");
                    else
                        Debug.WriteLine($"消息已存在，跳過: MessageId={messageId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存消息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     保存消息到數據庫 - 兼容舊方法，自動生成 message_id
        /// </summary>
        public static void SaveMessage(long chatId, bool isGroup, string content, string messageType,
            long senderId, string senderName, bool isFromMe, DateTime timestamp, List<MessageSegment> segments = null)
        {
            // 為沒有 message_id 的消息生成一個基於時間戳的 ID
            var messageId = timestamp.Ticks;
            SaveMessage(messageId, chatId, isGroup, content, messageType, senderId, senderName, isFromMe, timestamp,
                segments);
        }

        /// <summary>
        ///     加載聊天記錄 - 修復時區問題
        /// </summary>
        public static List<ChatMessage> GetChatMessages(long chatId, bool isGroup, int limit = 50)
        {
            var messages = new List<ChatMessage>();
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand(@"
                SELECT * FROM Messages 
                WHERE ChatId = @chatId AND IsGroup = @isGroup 
                ORDER BY Timestamp ASC 
                LIMIT @limit", db);

                    selectCommand.Parameters.AddWithValue("@chatId", chatId);
                    selectCommand.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);
                    selectCommand.Parameters.AddWithValue("@limit", limit);

                    // 獲取當前用戶ID
                    var currentUserId = GetCurrentUserId();

                    using (var query = selectCommand.ExecuteReader())
                    {
                        while (query.Read())
                        {
                            var timestampStr = Convert.ToString(query["Timestamp"]);

                            // 解析時間戳
                            DateTime originalTimestamp;
                            if (!DateTime.TryParse(timestampStr, out originalTimestamp))
                            {
                                Debug.WriteLine($"無法解析時間戳字符串: {timestampStr}");
                                originalTimestamp = DateTime.Now;
                            }

                            Debug.WriteLine(
                                $"GetChatMessages: 從數據庫讀取時間戳: {timestampStr} -> {originalTimestamp:yyyy-MM-dd HH:mm:ss}");

                            // 使用修正後的時間戳處理
                            var timestamp = ProcessTimestamp(originalTimestamp);

                            Debug.WriteLine($"GetChatMessages: 處理後時間戳: {timestamp:yyyy-MM-dd HH:mm:ss}");

                            // 嘗試反序列化消息段
                            var segments = new List<MessageSegment>();
                            try
                            {
                                var segmentsJsonOrdinal = query.GetOrdinal("SegmentsJson");
                                if (!query.IsDBNull(segmentsJsonOrdinal))
                                {
                                    var segmentsJson = Convert.ToString(query["SegmentsJson"]);
                                    if (!string.IsNullOrEmpty(segmentsJson))
                                    {
                                        // 使用 MessageSegmentParser 重新解析以獲得正確的類型
                                        var rawSegments = JsonConvert.DeserializeObject<JArray>(segmentsJson);
                                        if (rawSegments != null)
                                            segments = MessageSegmentParser.ParseMessageArray(rawSegments,
                                                isGroup ? chatId : 0);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"反序列化消息段時發生錯誤: {ex.Message}");
                                // 如果反序列化失敗，從文本內容創建一個文本段
                                var content = Convert.ToString(query["Content"]) ?? "";
                                if (!string.IsNullOrEmpty(content))
                                    segments = new List<MessageSegment> { new TextSegment(content) };
                            }

                            // 確保至少有一個段落
                            if (segments.Count == 0)
                            {
                                var content = Convert.ToString(query["Content"]) ?? "";
                                if (!string.IsNullOrEmpty(content))
                                    segments = new List<MessageSegment> { new TextSegment(content) };
                            }

                            var senderId = Convert.ToInt64(query["SenderId"]);
                            var isFromMe = Convert.ToInt32(query["IsFromMe"]) == 1;

                            // 動態生成發送者名稱，優先使用最新的用戶信息
                            var senderName = "";
                            if (isFromMe)
                            {
                                senderName = "我";
                            }
                            else if (isGroup)
                            {
                                // 群組消息：優先從群組成員信息獲取最新的顯示名稱
                                var memberInfo = GetGroupMember(chatId, senderId);
                                if (memberInfo != null)
                                {
                                    senderName = memberInfo.GetDisplayName();
                                }
                                else
                                {
                                    // 如果沒有群組成員信息，嘗試從好友列表獲取
                                    var friendName = GetFriendNameById(senderId);
                                    senderName = friendName.StartsWith("用戶 ")
                                        ? Convert.ToString(query["SenderName"]) ?? friendName
                                        : friendName;
                                }
                            }
                            else
                            {
                                // 私聊消息：從好友列表獲取
                                var friendName = GetFriendNameById(senderId);
                                senderName = friendName.StartsWith("用戶 ")
                                    ? Convert.ToString(query["SenderName"]) ?? friendName
                                    : friendName;
                            }

                            // 如果仍然沒有找到合適的名稱，使用數據庫中保存的名稱或默認格式
                            if (string.IsNullOrEmpty(senderName) || senderName.StartsWith("用戶 "))
                            {
                                var dbSenderName = Convert.ToString(query["SenderName"]) ?? "";
                                senderName = !string.IsNullOrEmpty(dbSenderName) && !dbSenderName.StartsWith("用戶 ")
                                    ? dbSenderName
                                    : isFromMe
                                        ? "我"
                                        : $"用戶 {senderId}";
                            }

                            var chatMessage = new ChatMessage
                            {
                                Content = Convert.ToString(query["Content"]) ?? "",
                                MessageType = Convert.ToString(query["MessageType"]) ?? "text",
                                SenderId = senderId,
                                SenderName = senderName,
                                IsFromMe = isFromMe,
                                Timestamp = timestamp // 使用修正後的時間戳
                            };

                            // 最後設置消息段，這將觸發屬性更新
                            chatMessage.Segments = segments;

                            messages.Add(chatMessage);
                        }
                    }

                    Debug.WriteLine($"加載聊天記錄: ChatId={chatId}, IsGroup={isGroup}, 消息數={messages.Count}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加載聊天記錄錯誤: {ex.Message}");
            }

            return messages;
        }

        /// <summary>
        ///     修復數據庫中所有消息的時間戳問題 - 針對8小時時差問題
        /// </summary>
        public static void FixDatabaseTimestamps()
        {
            try
            {
                Debug.WriteLine("開始修復數據庫中的時間戳問題");

                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    using (var transaction = db.BeginTransaction())
                    {
                        try
                        {
                            // 查詢所有消息
                            var selectCommand = new SqliteCommand(@"
                        SELECT MessageId, Timestamp 
                        FROM Messages 
                        ORDER BY Timestamp", db, transaction);

                            var messagesToUpdate =
                                new List<(long MessageId, DateTime OriginalTime, DateTime CorrectedTime)>();

                            using (var reader = selectCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var messageId = Convert.ToInt64(reader["MessageId"]);
                                    var timestampStr = Convert.ToString(reader["Timestamp"]);

                                    if (DateTime.TryParse(timestampStr, out var originalTime))
                                    {
                                        var correctedTime = ProcessTimestamp(originalTime);

                                        // 如果時間有變化，加入更新列表
                                        if (Math.Abs((correctedTime - originalTime).TotalMinutes) > 1)
                                            messagesToUpdate.Add((messageId, originalTime, correctedTime));
                                    }
                                }
                            }

                            // 批量更新時間戳
                            if (messagesToUpdate.Count > 0)
                            {
                                Debug.WriteLine($"需要更新 {messagesToUpdate.Count} 條消息的時間戳");

                                var updateCommand = new SqliteCommand(@"
                            UPDATE Messages 
                            SET Timestamp = @timestamp 
                            WHERE MessageId = @messageId", db, transaction);

                                foreach (var (messageId, originalTime, correctedTime) in messagesToUpdate)
                                {
                                    updateCommand.Parameters.Clear();
                                    updateCommand.Parameters.AddWithValue("@timestamp",
                                        correctedTime.ToString("yyyy-MM-dd HH:mm:ss"));
                                    updateCommand.Parameters.AddWithValue("@messageId", messageId);
                                    updateCommand.ExecuteNonQuery();

                                    Debug.WriteLine(
                                        $"更新消息 {messageId}: {originalTime:HH:mm:ss} -> {correctedTime:HH:mm:ss}");
                                }

                                transaction.Commit();
                                Debug.WriteLine($"成功修復了 {messagesToUpdate.Count} 條消息的時間戳");
                            }
                            else
                            {
                                Debug.WriteLine("沒有需要修復的時間戳");
                                transaction.Commit();
                            }
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Debug.WriteLine($"修復時間戳時發生錯誤，已回滾: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"修復數據庫時間戳時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取最新消息（用於聊天列表顯示）
        /// </summary>
        public static ChatMessage GetLatestMessage(long chatId, bool isGroup)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand(@"
                SELECT * FROM Messages 
                WHERE ChatId = @chatId AND IsGroup = @isGroup 
                ORDER BY Timestamp DESC 
                LIMIT 1", db);

                    selectCommand.Parameters.AddWithValue("@chatId", chatId);
                    selectCommand.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);

                    using (var query = selectCommand.ExecuteReader())
                    {
                        if (query.Read())
                        {
                            var timestampStr = Convert.ToString(query["Timestamp"]);
                            DateTime.TryParse(timestampStr, out var timestamp);

                            return new ChatMessage
                            {
                                Content = Convert.ToString(query["Content"]) ?? "",
                                MessageType = Convert.ToString(query["MessageType"]) ?? "text",
                                SenderId = Convert.ToInt64(query["SenderId"]),
                                SenderName = Convert.ToString(query["SenderName"]) ?? "",
                                IsFromMe = Convert.ToInt32(query["IsFromMe"]) == 1,
                                Timestamp = timestamp
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取最新消息錯誤: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        ///     獲取未讀消息數量
        /// </summary>
        public static int GetUnreadMessageCount(long chatId, bool isGroup, DateTime lastReadTime)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand(@"
                SELECT COUNT(*) FROM Messages 
                WHERE ChatId = @chatId AND IsGroup = @isGroup 
                AND IsFromMe = 0 AND Timestamp > @lastReadTime", db);

                    selectCommand.Parameters.AddWithValue("@chatId", chatId);
                    selectCommand.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);
                    selectCommand.Parameters.AddWithValue("@lastReadTime",
                        lastReadTime.ToString("yyyy-MM-dd HH:mm:ss"));

                    var result = selectCommand.ExecuteScalar();
                    return Convert.ToInt32(result);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取未讀消息數錯誤: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region 聊天列表緩存操作

        /// <summary>
        ///     保存聊天列表緩存
        /// </summary>
        /// <param name="account">當前登入的帳號</param>
        /// <param name="chatItems">聊天列表項目</param>
        public static void SaveChatListCache(string account, IEnumerable<ChatItem> chatItems)
        {
            try
            {
                if (string.IsNullOrEmpty(account) || chatItems == null)
                {
                    Debug.WriteLine("保存聊天列表緩存: 參數無效");
                    return;
                }

                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    using (var transaction = db.BeginTransaction())
                    {
                        try
                        {
                            // 刪除該帳號的舊緩存
                            var deleteCommand = new SqliteCommand("DELETE FROM ChatListCache WHERE Account = @account",
                                db, transaction);
                            deleteCommand.Parameters.AddWithValue("@account", account);
                            var deletedCount = deleteCommand.ExecuteNonQuery();
                            Debug.WriteLine($"刪除帳號 {account} 的舊緩存: {deletedCount} 條記錄");

                            // 插入新的緩存
                            var insertCommand = new SqliteCommand(@"
                                INSERT OR REPLACE INTO ChatListCache 
                                (Account, ChatId, ChatName, LastMessage, LastTime, UnreadCount, AvatarColor, IsGroup, MemberCount, LastUpdated) 
                                VALUES (@account, @chatId, @chatName, @lastMessage, @lastTime, @unreadCount, @avatarColor, @isGroup, @memberCount, @lastUpdated)",
                                db, transaction);

                            var savedCount = 0;
                            foreach (var chatItem in chatItems)
                            {
                                insertCommand.Parameters.Clear();
                                insertCommand.Parameters.AddWithValue("@account", account);
                                insertCommand.Parameters.AddWithValue("@chatId", chatItem.ChatId);
                                insertCommand.Parameters.AddWithValue("@chatName", chatItem.Name ?? "");
                                insertCommand.Parameters.AddWithValue("@lastMessage", chatItem.LastMessage ?? "");
                                insertCommand.Parameters.AddWithValue("@lastTime", chatItem.LastTime ?? "");
                                insertCommand.Parameters.AddWithValue("@unreadCount", chatItem.UnreadCount);
                                insertCommand.Parameters.AddWithValue("@avatarColor", chatItem.AvatarColor ?? "");
                                insertCommand.Parameters.AddWithValue("@isGroup", chatItem.IsGroup ? 1 : 0);
                                insertCommand.Parameters.AddWithValue("@memberCount", chatItem.MemberCount);
                                insertCommand.Parameters.AddWithValue("@lastUpdated",
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                                insertCommand.ExecuteNonQuery();
                                savedCount++;
                            }

                            transaction.Commit();
                            Debug.WriteLine($"成功保存帳號 {account} 的聊天列表緩存: {savedCount} 個聊天項目");
                        }
                        catch (Exception transactionEx)
                        {
                            transaction.Rollback();
                            Debug.WriteLine($"保存聊天列表緩存事務錯誤: {transactionEx.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存聊天列表緩存錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     載入聊天列表緩存
        /// </summary>
        /// <param name="account">當前登入的帳號</param>
        /// <returns>緩存的聊天列表</returns>
        public static List<ChatItem> LoadChatListCache(string account)
        {
            var chatItems = new List<ChatItem>();

            try
            {
                if (string.IsNullOrEmpty(account))
                {
                    Debug.WriteLine("載入聊天列表緩存: 帳號參數無效");
                    return chatItems;
                }

                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand(@"
                        SELECT * FROM ChatListCache 
                        WHERE Account = @account 
                        ORDER BY LastUpdated DESC", db);
                    selectCommand.Parameters.AddWithValue("@account", account);

                    using (var query = selectCommand.ExecuteReader())
                    {
                        while (query.Read())
                        {
                            var chatItem = new ChatItem
                            {
                                ChatId = Convert.ToInt64(query["ChatId"]),
                                Name = Convert.ToString(query["ChatName"]) ?? "",
                                LastMessage = Convert.ToString(query["LastMessage"]) ?? "",
                                LastTime = Convert.ToString(query["LastTime"]) ?? "",
                                UnreadCount = Convert.ToInt32(query["UnreadCount"]),
                                AvatarColor = Convert.ToString(query["AvatarColor"]) ?? "#FF4A90E2",
                                IsGroup = Convert.ToInt32(query["IsGroup"]) == 1,
                                MemberCount = Convert.ToInt32(query["MemberCount"])
                            };

                            chatItems.Add(chatItem);
                        }
                    }

                    Debug.WriteLine($"載入帳號 {account} 的聊天列表緩存: {chatItems.Count} 個聊天項目");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聊天列表緩存錯誤: {ex.Message}");
            }

            return chatItems;
        }

        /// <summary>
        ///     清除指定帳號的聊天列表緩存
        /// </summary>
        /// <param name="account">要清除緩存的帳號</param>
        public static void ClearChatListCache(string account)
        {
            try
            {
                if (string.IsNullOrEmpty(account))
                {
                    Debug.WriteLine("清除聊天列表緩存: 帳號參數無效");
                    return;
                }

                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var deleteCommand = new SqliteCommand("DELETE FROM ChatListCache WHERE Account = @account", db);
                    deleteCommand.Parameters.AddWithValue("@account", account);
                    var deletedCount = deleteCommand.ExecuteNonQuery();

                    Debug.WriteLine($"清除帳號 {account} 的聊天列表緩存: {deletedCount} 條記錄");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除聊天列表緩存錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取當前保存的帳號（用於檢查帳號是否改變）
        /// </summary>
        /// <returns>當前保存的帳號</returns>
        public static string GetCurrentAccount()
        {
            try
            {
                var settings = GetAllDatas();
                return settings.Get("Account") ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取當前帳號錯誤: {ex.Message}");
                return "";
            }
        }

        #endregion

        #region 最近聯繫人消息操作

        /// <summary>
        ///     保存最近聯繫人消息 - 增強版，支持消息段
        /// </summary>
        public static void SaveRecentMessages(List<RecentContactMessage> recentMessages)
        {
            try
            {
                if (recentMessages == null || recentMessages.Count == 0)
                {
                    Debug.WriteLine("保存最近消息: 沒有消息需要保存");
                    return;
                }

                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    using (var transaction = db.BeginTransaction())
                    {
                        try
                        {
                            var insertCommand = new SqliteCommand(@"
                                INSERT OR IGNORE INTO Messages (ChatId, IsGroup, Content, MessageType, SenderId, SenderName, IsFromMe, Timestamp, SegmentsJson) 
                                VALUES (@chatId, @isGroup, @content, @messageType, @senderId, @senderName, @isFromMe, @timestamp, @segmentsJson)",
                                db, transaction);

                            var savedCount = 0;
                            var currentUserId = GetCurrentUserId(); // 獲取當前用戶ID

                            foreach (var recentMsg in recentMessages)
                                try
                                {
                                    // 確定聊天ID和類型
                                    var isGroup = recentMsg.ChatType == 2; // ChatType 2 為群組
                                    var chatId = isGroup ? recentMsg.GroupId : recentMsg.UserId;

                                    // 確定發送者信息
                                    var senderId = recentMsg.Sender?.UserId ?? recentMsg.UserId;
                                    var senderName = "";
                                    var isFromMe = false;

                                    if (isGroup)
                                    {
                                        // 群組消息
                                        senderName = !string.IsNullOrEmpty(recentMsg.Sender?.Card)
                                            ? recentMsg.Sender.Card
                                            : recentMsg.Sender?.Nickname ?? recentMsg.SendNickName ?? "群成員";
                                        isFromMe = senderId == currentUserId;
                                    }
                                    else
                                    {
                                        // 私聊消息
                                        if (senderId == currentUserId)
                                        {
                                            senderName = "我";
                                            isFromMe = true;
                                        }
                                        else
                                        {
                                            senderName = !string.IsNullOrEmpty(recentMsg.Remark)
                                                ? recentMsg.Remark
                                                : recentMsg.PeerName ?? recentMsg.SendNickName ?? "好友";
                                            isFromMe = false;
                                        }
                                    }

                                    // 使用解析後的消息內容，如果沒有則使用原始消息
                                    var messageContent = !string.IsNullOrEmpty(recentMsg.ParsedMessage)
                                        ? recentMsg.ParsedMessage
                                        : !string.IsNullOrEmpty(recentMsg.RawMessage)
                                            ? recentMsg.RawMessage
                                            : recentMsg.Message ?? "[空消息]";

                                    // 嘗試從 RecentContactMessage 獲取消息段（如果有的話）
                                    var segmentsJson = "";
                                    if (recentMsg.MessageSegments != null && recentMsg.MessageSegments.Count > 0)
                                    {
                                        try
                                        {
                                            segmentsJson = JsonConvert.SerializeObject(recentMsg.MessageSegments);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"序列化最近消息段時發生錯誤: {ex.Message}");
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(messageContent))
                                    {
                                        // 如果沒有消息段，為文本內容創建一個文本段
                                        var textSegments = new List<MessageSegment> { new TextSegment(messageContent) };
                                        try
                                        {
                                            segmentsJson = JsonConvert.SerializeObject(textSegments);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"創建文本段時發生錯誤: {ex.Message}");
                                        }
                                    }

                                    // 將時間戳轉換為DateTime
                                    var timestamp = DateTimeOffset.FromUnixTimeSeconds(recentMsg.Time).DateTime;

                                    insertCommand.Parameters.Clear();
                                    insertCommand.Parameters.AddWithValue("@chatId", chatId);
                                    insertCommand.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);
                                    insertCommand.Parameters.AddWithValue("@content", messageContent);
                                    insertCommand.Parameters.AddWithValue("@messageType",
                                        recentMsg.MessageType ?? "text");
                                    insertCommand.Parameters.AddWithValue("@senderId", senderId);
                                    insertCommand.Parameters.AddWithValue("@senderName", senderName);
                                    insertCommand.Parameters.AddWithValue("@isFromMe", isFromMe ? 1 : 0);
                                    insertCommand.Parameters.AddWithValue("@timestamp",
                                        timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                                    insertCommand.Parameters.AddWithValue("@segmentsJson", segmentsJson);

                                    var result = insertCommand.ExecuteNonQuery();
                                    if (result > 0)
                                    {
                                        savedCount++;
                                        if (savedCount <= 5) // 只記錄前5條消息的詳細信息
                                            Debug.WriteLine(
                                                $"保存消息 {savedCount}: ChatId={chatId}, IsGroup={isGroup}, Sender={senderName}, Content={messageContent.Substring(0, Math.Min(50, messageContent.Length))}...");
                                    }
                                }
                                catch (Exception msgEx)
                                {
                                    Debug.WriteLine($"保存單條最近消息時發生錯誤: {msgEx.Message}");
                                }

                            transaction.Commit();
                            Debug.WriteLine($"成功保存 {savedCount} 條最近聯繫人消息到數據庫");
                        }
                        catch (Exception transactionEx)
                        {
                            transaction.Rollback();
                            Debug.WriteLine($"保存最近消息事務錯誤: {transactionEx.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存最近消息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取當前用戶ID（從設置中獲取）
        /// </summary>
        private static long GetCurrentUserId()
        {
            try
            {
                var settings = GetAllDatas();
                var account = settings.Get("Account") ?? "";
                if (long.TryParse(account, out var userId)) return userId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取當前用戶ID錯誤: {ex.Message}");
            }

            return 0;
        }

        // 確保 CreateChatItemsFromRecentMessages 方法正確處理時間戳和持久化
        public static List<ChatItem> CreateChatItemsFromRecentMessages(List<RecentContactMessage> recentMessages)
        {
            var chatItems = new List<ChatItem>();
            var processedChats = new HashSet<string>(); // 用於避免重複

            try
            {
                foreach (var recentMsg in recentMessages)
                {
                    var isGroup = recentMsg.ChatType == 2;
                    var chatId = isGroup ? recentMsg.GroupId : recentMsg.UserId;
                    var chatKey = $"{chatId}_{isGroup}"; // 創建唯一鍵

                    if (processedChats.Contains(chatKey)) continue;

                    var chatName = "";
                    var memberCount = 0;

                    if (isGroup)
                    {
                        // 群組聊天
                        chatName = GetGroupNameById(chatId);
                        var groupInfo = GetAllGroups().FirstOrDefault(g => g.GroupId == chatId);
                        memberCount = groupInfo?.MemberCount ?? 0;
                    }
                    else
                    {
                        // 私聊
                        chatName = !string.IsNullOrEmpty(recentMsg.Remark)
                            ? recentMsg.Remark
                            : recentMsg.PeerName ?? GetFriendNameById(chatId);
                    }

                    // 使用解析後的消息內容
                    var lastMessage = !string.IsNullOrEmpty(recentMsg.ParsedMessage)
                        ? recentMsg.ParsedMessage
                        : !string.IsNullOrEmpty(recentMsg.RawMessage)
                            ? recentMsg.RawMessage
                            : recentMsg.Message ?? "";

                    // 使用處理後的時間戳
                    DateTime timestamp;
                    if (recentMsg.ProcessedTimestamp != DateTime.MinValue)
                    {
                        timestamp = recentMsg.ProcessedTimestamp;
                    }
                    else
                    {
                        var utcTime = DateTimeOffset.FromUnixTimeSeconds(recentMsg.Time).UtcDateTime;
                        var localTime = utcTime.ToLocalTime();
                        timestamp = ProcessTimestamp(localTime);
                    }

                    var chatItem = new ChatItem
                    {
                        ChatId = chatId,
                        Name = chatName,
                        LastMessage = lastMessage,
                        LastTime = timestamp.ToString("HH:mm"),
                        UnreadCount = 0, // 初始未讀數為0
                        AvatarColor = GetRandomAvatarColor(),
                        IsGroup = isGroup,
                        MemberCount = memberCount
                    };

                    chatItems.Add(chatItem);
                    processedChats.Add(chatKey);
                }

                // 按時間排序，最新的在前面
                chatItems = chatItems.OrderByDescending(c =>
                {
                    var recentMsg = recentMessages.FirstOrDefault(m =>
                        (m.ChatType == 2 ? m.GroupId : m.UserId) == c.ChatId &&
                        m.ChatType == 2 == c.IsGroup);

                    return recentMsg?.ProcessedTimestamp.Ticks ?? recentMsg?.Time ?? 0;
                }).ToList();

                Debug.WriteLine($"從最近聯繫人創建了 {chatItems.Count} 個聊天項目");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"從最近消息創建聊天項目錯誤: {ex.Message}");
            }

            return chatItems;
        }

        /// <summary>
        ///     獲取隨機頭像顏色
        /// </summary>
        private static string GetRandomAvatarColor()
        {
            var colors = new[]
            {
                "#FF4A90E2", "#FF50C878", "#FFE74C3C", "#FF9B59B6",
                "#FFF39C12", "#FF3498DB", "#FF2ECC71", "#FFE67E22",
                "#FF9B59B6", "#FF1ABC9C", "#FF34495E", "#FFF1C40F"
            };

            var random = new Random();
            return colors[random.Next(colors.Length)];
        }

        #endregion

        #region 群組成員數據操作

        /// <summary>
        ///     保存群組成員信息
        /// </summary>
        public static void SaveGroupMember(GroupMemberInfo memberInfo)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var insertCommand = new SqliteCommand(@"
                        INSERT OR REPLACE INTO GroupMembers 
                        (GroupId, UserId, Nickname, Card, Sex, Age, Area, JoinTime, LastSentTime, Level, Role, 
                         Unfriendly, Title, TitleExpireTime, CardChangeable, ShutUpTimestamp, LastUpdated) 
                        VALUES (@groupId, @userId, @nickname, @card, @sex, @age, @area, @joinTime, @lastSentTime, 
                                @level, @role, @unfriendly, @title, @titleExpireTime, @cardChangeable, @shutUpTimestamp, @lastUpdated)",
                        db);

                    insertCommand.Parameters.AddWithValue("@groupId", memberInfo.GroupId);
                    insertCommand.Parameters.AddWithValue("@userId", memberInfo.UserId);
                    insertCommand.Parameters.AddWithValue("@nickname", memberInfo.Nickname ?? "");
                    insertCommand.Parameters.AddWithValue("@card", memberInfo.Card ?? "");
                    insertCommand.Parameters.AddWithValue("@sex", memberInfo.Sex ?? "");
                    insertCommand.Parameters.AddWithValue("@age", memberInfo.Age);
                    insertCommand.Parameters.AddWithValue("@area", memberInfo.Area ?? "");
                    insertCommand.Parameters.AddWithValue("@joinTime", memberInfo.JoinTime);
                    insertCommand.Parameters.AddWithValue("@lastSentTime", memberInfo.LastSentTime);
                    insertCommand.Parameters.AddWithValue("@level", memberInfo.Level ?? "");
                    insertCommand.Parameters.AddWithValue("@role", memberInfo.Role ?? "");
                    insertCommand.Parameters.AddWithValue("@unfriendly", memberInfo.Unfriendly ? 1 : 0);
                    insertCommand.Parameters.AddWithValue("@title", memberInfo.Title ?? "");
                    insertCommand.Parameters.AddWithValue("@titleExpireTime", memberInfo.TitleExpireTime);
                    insertCommand.Parameters.AddWithValue("@cardChangeable", memberInfo.CardChangeable ? 1 : 0);
                    insertCommand.Parameters.AddWithValue("@shutUpTimestamp", memberInfo.ShutUpTimestamp);
                    insertCommand.Parameters.AddWithValue("@lastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    insertCommand.ExecuteNonQuery();

                    Debug.WriteLine(
                        $"保存群組成員信息: GroupId={memberInfo.GroupId}, UserId={memberInfo.UserId}, DisplayName={memberInfo.GetDisplayName()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存群組成員信息錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     獲取群組成員信息
        /// </summary>
        public static GroupMemberInfo GetGroupMember(long groupId, long userId)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand(@"
                        SELECT * FROM GroupMembers 
                        WHERE GroupId = @groupId AND UserId = @userId", db);
                    selectCommand.Parameters.AddWithValue("@groupId", groupId);
                    selectCommand.Parameters.AddWithValue("@userId", userId);

                    using (var query = selectCommand.ExecuteReader())
                    {
                        if (query.Read())
                            return new GroupMemberInfo
                            {
                                GroupId = Convert.ToInt64(query["GroupId"]),
                                UserId = Convert.ToInt64(query["UserId"]),
                                Nickname = Convert.ToString(query["Nickname"]) ?? "",
                                Card = Convert.ToString(query["Card"]) ?? "",
                                Sex = Convert.ToString(query["Sex"]) ?? "",
                                Age = Convert.ToInt32(query["Age"]),
                                Area = Convert.ToString(query["Area"]) ?? "",
                                JoinTime = Convert.ToInt64(query["JoinTime"]),
                                LastSentTime = Convert.ToInt64(query["LastSentTime"]),
                                Level = Convert.ToString(query["Level"]) ?? "",
                                Role = Convert.ToString(query["Role"]) ?? "",
                                Unfriendly = Convert.ToInt32(query["Unfriendly"]) == 1,
                                Title = Convert.ToString(query["Title"]) ?? "",
                                TitleExpireTime = Convert.ToInt64(query["TitleExpireTime"]),
                                CardChangeable = Convert.ToInt32(query["CardChangeable"]) == 1,
                                ShutUpTimestamp = Convert.ToInt64(query["ShutUpTimestamp"])
                            };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取群組成員信息錯誤: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        ///     獲取群組成員的顯示名稱（優先級：card > nickname > user_id）
        /// </summary>
        public static string GetGroupMemberDisplayName(long groupId, long userId)
        {
            try
            {
                var memberInfo = GetGroupMember(groupId, userId);
                if (memberInfo != null) return memberInfo.GetDisplayName();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取群組成員顯示名稱錯誤: {ex.Message}");
            }

            return userId.ToString(); // 回退到顯示 user_id
        }

        #endregion

        #region 消息查詢和獲取操作

        /// <summary>
        ///     根據消息ID獲取消息內容
        /// </summary>
        public static ChatMessage GetMessageById(long messageId)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand(@"
                        SELECT * FROM Messages 
                        WHERE MessageId = @messageId 
                        LIMIT 1", db);
                    selectCommand.Parameters.AddWithValue("@messageId", messageId);

                    using (var query = selectCommand.ExecuteReader())
                    {
                        if (query.Read())
                        {
                            var timestampStr = Convert.ToString(query["Timestamp"]);
                            DateTime.TryParse(timestampStr, out var timestamp);

                            // 嘗試反序列化消息段
                            var segments = new List<MessageSegment>();
                            try
                            {
                                var segmentsJsonOrdinal = query.GetOrdinal("SegmentsJson");
                                if (!query.IsDBNull(segmentsJsonOrdinal))
                                {
                                    var segmentsJson = Convert.ToString(query["SegmentsJson"]);
                                    if (!string.IsNullOrEmpty(segmentsJson))
                                    {
                                        var rawSegments = JsonConvert.DeserializeObject<JArray>(segmentsJson);
                                        if (rawSegments != null)
                                        {
                                            var isGroup = Convert.ToInt32(query["IsGroup"]) == 1;
                                            var chatId = Convert.ToInt64(query["ChatId"]);
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

                            // 確保至少有一個段落
                            if (segments.Count == 0)
                            {
                                var content = Convert.ToString(query["Content"]) ?? "";
                                if (!string.IsNullOrEmpty(content))
                                    segments = new List<MessageSegment> { new TextSegment(content) };
                            }

                            var chatMessage = new ChatMessage
                            {
                                Content = Convert.ToString(query["Content"]) ?? "",
                                MessageType = Convert.ToString(query["MessageType"]) ?? "text",
                                SenderId = Convert.ToInt64(query["SenderId"]),
                                SenderName = Convert.ToString(query["SenderName"]) ?? "",
                                IsFromMe = Convert.ToInt32(query["IsFromMe"]) == 1,
                                Timestamp = timestamp,
                                Segments = segments
                            };

                            Debug.WriteLine($"從數據庫獲取消息: MessageId={messageId}, Content={chatMessage.Content}");
                            return chatMessage;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"根據ID獲取消息錯誤: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        ///     檢查消息是否存在於數據庫中
        /// </summary>
        public static bool MessageExists(long messageId)
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand(@"
                        SELECT COUNT(*) FROM Messages 
                        WHERE MessageId = @messageId", db);
                    selectCommand.Parameters.AddWithValue("@messageId", messageId);

                    var result = selectCommand.ExecuteScalar();
                    return Convert.ToInt32(result) > 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"檢查消息是否存在錯誤: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 用戶信息更新方法

        /// <summary>
        ///     更新舊消息中的用戶信息 - 改進版，優先使用card名稱
        /// </summary>
        public static void UpdateUserInfoInMessages()
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    // 獲取所有需要更新的消息
                    var selectOldMessagesCommand = new SqliteCommand(@"
                SELECT DISTINCT ChatId, SenderId, IsGroup, SenderName 
                FROM Messages 
                WHERE IsFromMe = 0", db);

                    var messagesToUpdate = new List<(long ChatId, long SenderId, bool IsGroup, string OldSenderName)>();

                    using (var query = selectOldMessagesCommand.ExecuteReader())
                    {
                        while (query.Read())
                            messagesToUpdate.Add((
                                Convert.ToInt64(query["ChatId"]),
                                Convert.ToInt64(query["SenderId"]),
                                Convert.ToInt32(query["IsGroup"]) == 1,
                                Convert.ToString(query["SenderName"]) ?? ""
                            ));
                    }

                    Debug.WriteLine($"找到 {messagesToUpdate.Count} 條需要檢查用戶信息的消息");

                    var updatedCount = 0;
                    var updateCommand = new SqliteCommand(@"
                UPDATE Messages 
                SET SenderName = @newSenderName 
                WHERE ChatId = @chatId AND SenderId = @senderId AND IsGroup = @isGroup AND IsFromMe = 0",
                        db);

                    foreach (var (chatId, senderId, isGroup, oldSenderName) in messagesToUpdate)
                    {
                        var newSenderName = "";

                        if (isGroup)
                        {
                            // 群組消息：優先從群組成員信息獲取card名稱
                            var memberInfo = GetGroupMember(chatId, senderId);
                            if (memberInfo != null)
                            {
                                newSenderName = memberInfo.GetDisplayName();
                            }
                            else
                            {
                                // 沒有群組成員信息，嘗試從好友列表獲取
                                var friendName = GetFriendNameById(senderId);
                                if (!friendName.StartsWith("用戶 ")) newSenderName = friendName;
                            }
                        }
                        else
                        {
                            // 私聊消息：從好友列表獲取
                            var friendName = GetFriendNameById(senderId);
                            if (!friendName.StartsWith("用戶 ")) newSenderName = friendName;
                        }

                        // 只有當找到了更好的名稱時才更新
                        if (!string.IsNullOrEmpty(newSenderName) && newSenderName != oldSenderName)
                        {
                            updateCommand.Parameters.Clear();
                            updateCommand.Parameters.AddWithValue("@newSenderName", newSenderName);
                            updateCommand.Parameters.AddWithValue("@chatId", chatId);
                            updateCommand.Parameters.AddWithValue("@senderId", senderId);
                            updateCommand.Parameters.AddWithValue("@isGroup", isGroup ? 1 : 0);

                            var affectedRows = updateCommand.ExecuteNonQuery();
                            if (affectedRows > 0)
                            {
                                updatedCount += affectedRows;
                                Debug.WriteLine(
                                    $"更新用戶信息: {oldSenderName} -> {newSenderName} (ChatId: {chatId}, SenderId: {senderId}, IsGroup: {isGroup})");
                            }
                        }
                    }

                    Debug.WriteLine($"成功更新了 {updatedCount} 條消息的用戶信息");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新用戶信息時發生錯誤: {ex.Message}");
            }
        }


        /// <summary>
        ///     改進版時間戳處理 - 修復時區問題
        /// </summary>
        public static DateTime ProcessTimestamp(DateTime originalTimestamp)
        {
            try
            {
                // 先檢查時間戳是否合理
                var now = DateTime.Now;
                var timeDifference = Math.Abs((now - originalTimestamp).TotalHours);

                Debug.WriteLine(
                    $"ProcessTimestamp: 原始時間={originalTimestamp:yyyy-MM-dd HH:mm:ss}, 當前時間={now:yyyy-MM-dd HH:mm:ss}, 時差={timeDifference:F1}小時");

                // 如果時間戳看起來合理（在過去30天內且不超過當前時間1小時），直接使用
                if (originalTimestamp >= now.AddDays(-30) && originalTimestamp <= now.AddHours(1))
                {
                    Debug.WriteLine($"ProcessTimestamp: 時間戳合理，直接使用: {originalTimestamp:yyyy-MM-dd HH:mm:ss}");
                    return originalTimestamp;
                }

                // 檢查是否是常見的時區問題（8小時偏差）
                if (Math.Abs(timeDifference - 8) < 0.1)
                {
                    // 很可能是UTC時間需要轉換為本地時間
                    var utcTime = DateTime.SpecifyKind(originalTimestamp, DateTimeKind.Utc);
                    var localTime = utcTime.ToLocalTime();
                    Debug.WriteLine(
                        $"ProcessTimestamp: 修正8小時時區偏差: {originalTimestamp:yyyy-MM-dd HH:mm:ss} -> {localTime:yyyy-MM-dd HH:mm:ss}");
                    return localTime;
                }

                // 如果時間戳在未來，可能需要減去時區偏移
                if (originalTimestamp > now.AddHours(1))
                {
                    var correctedTime = originalTimestamp.AddHours(-8);
                    if (correctedTime >= now.AddDays(-30) && correctedTime <= now.AddHours(1))
                    {
                        Debug.WriteLine(
                            $"ProcessTimestamp: 未來時間戳修正: {originalTimestamp:yyyy-MM-dd HH:mm:ss} -> {correctedTime:yyyy-MM-dd HH:mm:ss}");
                        return correctedTime;
                    }
                }

                // 如果時間戳太舊，可能需要加上時區偏移
                if (originalTimestamp < now.AddDays(-1))
                {
                    var correctedTime = originalTimestamp.AddHours(8);
                    if (correctedTime >= now.AddDays(-30) && correctedTime <= now.AddHours(1))
                    {
                        Debug.WriteLine(
                            $"ProcessTimestamp: 過舊時間戳修正: {originalTimestamp:yyyy-MM-dd HH:mm:ss} -> {correctedTime:yyyy-MM-dd HH:mm:ss}");
                        return correctedTime;
                    }
                }

                // 嘗試按照不同的時區類型處理
                DateTime processedTime;

                if (originalTimestamp.Kind == DateTimeKind.Utc)
                {
                    // 如果是UTC時間，直接轉換為本地時間
                    processedTime = originalTimestamp.ToLocalTime();
                    Debug.WriteLine($"ProcessTimestamp: UTC時間轉換為本地時間: {processedTime:yyyy-MM-dd HH:mm:ss}");
                }
                else if (originalTimestamp.Kind == DateTimeKind.Unspecified)
                {
                    // 如果時間類型未指定，嘗試不同的處理方式
                    var utcTime = DateTime.SpecifyKind(originalTimestamp, DateTimeKind.Utc);
                    var localTime = utcTime.ToLocalTime();

                    // 檢查轉換後的時間是否更合理
                    var localTimeDifference = Math.Abs((now - localTime).TotalHours);
                    if (localTimeDifference < timeDifference)
                    {
                        processedTime = localTime;
                        Debug.WriteLine($"ProcessTimestamp: 未指定類型時間當作UTC處理: {processedTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        processedTime = originalTimestamp;
                        Debug.WriteLine($"ProcessTimestamp: 未指定類型時間保持原樣: {processedTime:yyyy-MM-dd HH:mm:ss}");
                    }
                }
                else
                {
                    // 如果已經是本地時間，檢查是否需要進一步調整
                    processedTime = originalTimestamp;
                    Debug.WriteLine($"ProcessTimestamp: 本地時間保持原樣: {processedTime:yyyy-MM-dd HH:mm:ss}");
                }

                return processedTime;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessTimestamp: 處理時間戳時發生錯誤: {ex.Message}");
                // 如果處理失敗，返回原始時間戳
                return originalTimestamp;
            }
        }

        /// <summary>
        ///     新增：修復數據庫中所有消息的時間戳問題
        /// </summary>
        public static void FixAllTimestampIssues()
        {
            try
            {
                Debug.WriteLine("開始修復所有消息的時間戳問題");

                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var connection = new SqliteConnection($"Filename={dbpath}"))
                {
                    connection.Open();

                    // 查詢所有消息
                    var query = @"
                SELECT MessageId, ChatId, IsGroup, Timestamp 
                FROM Messages 
                ORDER BY Timestamp";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var updates = new List<(long messageId, DateTime correctedTime)>();

                            while (reader.Read())
                            {
                                // 使用正確的 Microsoft.Data.Sqlite API
                                var messageId = Convert.ToInt64(reader["MessageId"]);
                                var timestampStr = Convert.ToString(reader["Timestamp"]);

                                if (DateTime.TryParse(timestampStr, out var originalTime))
                                {
                                    // 重新處理時間戳
                                    var correctedTime = ProcessTimestamp(originalTime);

                                    // 如果時間不同，加入更新列表
                                    if (Math.Abs((correctedTime - originalTime).TotalMinutes) > 1)
                                        updates.Add((messageId, correctedTime));
                                }
                            }


                            // 批量更新時間戳
                            if (updates.Count > 0)
                            {
                                Debug.WriteLine($"需要更新 {updates.Count} 條消息的時間戳");

                                using (var transaction = connection.BeginTransaction())
                                {
                                    var updateQuery =
                                        "UPDATE Messages SET Timestamp = @timestamp WHERE MessageId = @messageId";
                                    using (var updateCommand = new SqliteCommand(updateQuery, connection, transaction))
                                    {
                                        foreach (var (messageId, correctedTime) in updates)
                                        {
                                            updateCommand.Parameters.Clear();
                                            updateCommand.Parameters.AddWithValue("@timestamp",
                                                correctedTime.ToString("yyyy-MM-dd HH:mm:ss"));
                                            updateCommand.Parameters.AddWithValue("@messageId", messageId);
                                            updateCommand.ExecuteNonQuery();
                                        }
                                    }

                                    transaction.Commit();
                                    Debug.WriteLine($"成功更新了 {updates.Count} 條消息的時間戳");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("沒有需要更新的時間戳");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"修復時間戳時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     強制修復時間戳問題 - 針對特定的時區偏移問題
        /// </summary>
        public static void ForceFixTimestampOffset()
        {
            try
            {
                Debug.WriteLine("開始強制修復時間戳偏移問題");

                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var connection = new SqliteConnection($"Filename={dbpath}"))
                {
                    connection.Open();

                    // 檢查是否有明顯錯誤的時間戳（比如比當前時間晚很多小時的）
                    var now = DateTime.Now;
                    var checkQuery = @"
                SELECT COUNT(*) FROM Messages 
                WHERE datetime(Timestamp) > datetime('now', '+1 hour')";

                    using (var checkCommand = new SqliteCommand(checkQuery, connection))
                    {
                        var futureMessagesCount = Convert.ToInt32(checkCommand.ExecuteScalar());
                        Debug.WriteLine($"發現 {futureMessagesCount} 條時間戳在未來的消息");

                        if (futureMessagesCount > 0)
                        {
                            // 如果有未來的消息，很可能是時區問題，統一減去時區偏移
                            var timeZoneOffset = TimeZoneInfo.Local.BaseUtcOffset;
                            Debug.WriteLine($"當前時區偏移: {timeZoneOffset}");

                            // 如果是東八區（+8小時），則可能需要減去時區偏移
                            if (Math.Abs(timeZoneOffset.TotalHours - 8) < 0.1)
                            {
                                var updateQuery = @"
                            UPDATE Messages 
                            SET Timestamp = datetime(Timestamp, '-8 hours')
                            WHERE datetime(Timestamp) > datetime('now', '+1 hour')";

                                using (var updateCommand = new SqliteCommand(updateQuery, connection))
                                {
                                    var updatedRows = updateCommand.ExecuteNonQuery();
                                    Debug.WriteLine($"修正了 {updatedRows} 條消息的時間戳（減去8小時）");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"強制修復時間戳時發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     改進版時間戳修復方法 - 替換原有的 FixTimestampIssue
        /// </summary>
        public static void FixTimestampIssue()
        {
            // 調用新的全面修復方法
            FixAllTimestampIssues();
        }

        #endregion

        // 在 DataAccess.cs 文件的末尾添加以下方法

        #region 數據庫管理和統計

        /// <summary>
        ///     刪除所有聊天資料
        /// </summary>
        public static void DeleteAllChatData()
        {
            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    using (var transaction = db.BeginTransaction())
                    {
                        try
                        {
                            // 刪除所有聊天消息
                            var deleteMessagesCmd = new SqliteCommand("DELETE FROM Messages", db, transaction);
                            var deletedMessages = deleteMessagesCmd.ExecuteNonQuery();

                            // 刪除所有聊天列表緩存
                            var deleteChatListCmd = new SqliteCommand("DELETE FROM ChatListCache", db, transaction);
                            var deletedChatList = deleteChatListCmd.ExecuteNonQuery();

                            transaction.Commit();

                            Debug.WriteLine($"成功刪除所有聊天資料：{deletedMessages} 條消息，{deletedChatList} 個聊天列表項");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Debug.WriteLine($"刪除聊天資料時發生錯誤，已回滾：{ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刪除所有聊天資料錯誤：{ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     獲取數據庫統計信息
        /// </summary>
        public static DatabaseStatistics GetDatabaseStatistics()
        {
            var stats = new DatabaseStatistics();

            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    // 獲取消息數量
                    var messagesCountCmd = new SqliteCommand("SELECT COUNT(*) FROM Messages", db);
                    stats.TotalMessages = Convert.ToInt32(messagesCountCmd.ExecuteScalar());

                    // 獲取群組數量
                    var groupsCountCmd = new SqliteCommand("SELECT COUNT(*) FROM Groups", db);
                    stats.TotalGroups = Convert.ToInt32(groupsCountCmd.ExecuteScalar());

                    // 獲取好友數量
                    var friendsCountCmd = new SqliteCommand("SELECT COUNT(*) FROM Friends", db);
                    stats.TotalFriends = Convert.ToInt32(friendsCountCmd.ExecuteScalar());

                    // 獲取好友分類數量
                    var categoriesCountCmd = new SqliteCommand("SELECT COUNT(*) FROM FriendCategories", db);
                    stats.TotalCategories = Convert.ToInt32(categoriesCountCmd.ExecuteScalar());

                    // 獲取群組成員數量
                    var groupMembersCountCmd = new SqliteCommand("SELECT COUNT(*) FROM GroupMembers", db);
                    stats.TotalGroupMembers = Convert.ToInt32(groupMembersCountCmd.ExecuteScalar());

                    // 獲取聊天列表緩存數量
                    var chatListCountCmd = new SqliteCommand("SELECT COUNT(*) FROM ChatListCache", db);
                    stats.TotalChatListItems = Convert.ToInt32(chatListCountCmd.ExecuteScalar());

                    // 獲取設定項數量
                    var settingsCountCmd = new SqliteCommand("SELECT COUNT(*) FROM AppSettings", db);
                    stats.TotalSettings = Convert.ToInt32(settingsCountCmd.ExecuteScalar());

                    // 獲取數據庫文件大小
                    var fileInfo = new FileInfo(dbpath);
                    stats.DatabaseSize = fileInfo.Length;

                    Debug.WriteLine(
                        $"獲取數據庫統計信息成功：消息 {stats.TotalMessages} 條，群組 {stats.TotalGroups} 個，好友 {stats.TotalFriends} 個");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取數據庫統計信息錯誤：{ex.Message}");
            }

            return stats;
        }

        /// <summary>
        ///     獲取最近的聊天消息（用於展示）
        /// </summary>
        public static List<ChatMessage> GetRecentChatMessages(int limit = 10)
        {
            var messages = new List<ChatMessage>();

            try
            {
                var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
                using (var db = new SqliteConnection($"Filename={dbpath}"))
                {
                    db.Open();

                    var selectCommand = new SqliteCommand(@"
                SELECT * FROM Messages 
                ORDER BY Timestamp DESC 
                LIMIT @limit", db);
                    selectCommand.Parameters.AddWithValue("@limit", limit);

                    using (var query = selectCommand.ExecuteReader())
                    {
                        while (query.Read())
                        {
                            var timestampStr = Convert.ToString(query["Timestamp"]);
                            DateTime.TryParse(timestampStr, out var timestamp);

                            var message = new ChatMessage
                            {
                                Content = Convert.ToString(query["Content"]) ?? "",
                                MessageType = Convert.ToString(query["MessageType"]) ?? "text",
                                SenderId = Convert.ToInt64(query["SenderId"]),
                                SenderName = Convert.ToString(query["SenderName"]) ?? "",
                                IsFromMe = Convert.ToInt32(query["IsFromMe"]) == 1,
                                Timestamp = timestamp
                            };

                            messages.Add(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"獲取最近聊天消息錯誤：{ex.Message}");
            }

            return messages;
        }

        #endregion
    }
}