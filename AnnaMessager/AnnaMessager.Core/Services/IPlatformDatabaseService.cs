using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AnnaMessager.Core.Models;

namespace AnnaMessager.Core.Services
{
    /// <summary>
    ///     平台特定的數據庫服務介面，由各平台實現
    /// </summary>
    public interface IPlatformDatabaseService
    {
        /// <summary>
        ///     初始化數據庫
        /// </summary>
        Task InitializeDatabaseAsync();

        #region 賬號管理

        Task<List<AccountEntity>> GetAllAccountsAsync();
        Task<AccountEntity> GetAccountAsync(string account);
        Task<AccountEntity> GetDefaultAccountAsync();
        Task<int> SaveAccountAsync(AccountEntity account);
        Task DeleteAccountAsync(int accountId);
        Task SetDefaultAccountAsync(int accountId);

        #endregion

        #region 伺服器管理

        Task<List<ServerEntity>> GetAllServersAsync();
        Task<ServerEntity> GetServerAsync(int serverId);
        Task<ServerEntity> GetDefaultServerAsync();
        Task<int> SaveServerAsync(ServerEntity server);
        Task DeleteServerAsync(int serverId);
        Task SetDefaultServerAsync(int serverId);

        #endregion

        #region 聊天緩存

        Task<List<ChatCacheEntity>> GetAllChatCacheAsync();
        Task<ChatCacheEntity> GetChatCacheAsync(long chatId, bool isGroup);
        Task<int> SaveChatCacheAsync(ChatCacheEntity chatCache);
        Task DeleteChatCacheAsync(long chatId, bool isGroup);
        Task ClearChatCacheAsync();

        #endregion

        #region 聯絡人緩存

        Task<List<ContactCacheEntity>> GetAllContactCacheAsync();
        Task<ContactCacheEntity> GetContactCacheAsync(long userId);
        Task<int> SaveContactCacheAsync(ContactCacheEntity contact);
        Task DeleteContactCacheAsync(long userId);
        Task ClearContactCacheAsync();

        #endregion

        #region 群組緩存

        Task<List<GroupCacheEntity>> GetAllGroupCacheAsync();
        Task<GroupCacheEntity> GetGroupCacheAsync(long groupId);
        Task<int> SaveGroupCacheAsync(GroupCacheEntity group);
        Task DeleteGroupCacheAsync(long groupId);
        Task ClearGroupCacheAsync();

        #endregion

        #region 設定管理

        Task<List<AppSettingsEntity>> GetAllSettingsAsync();
        Task<AppSettingsEntity> GetSettingAsync(string key);
        Task<int> SaveSettingAsync(AppSettingsEntity setting);
        Task DeleteSettingAsync(string key);
        Task ClearAllSettingsAsync();

        #endregion

        #region 緩存統計

        Task<long> GetDatabaseSizeAsync();
        Task<int> GetCachedItemsCountAsync(string tableName);
        Task ClearExpiredCacheAsync(DateTime expireTime);

        #endregion

        #region 好友分類緩存
        Task<List<CategoryCacheEntity>> GetAllCategoryCacheAsync();
        Task<int> SaveCategoryCacheAsync(CategoryCacheEntity category);
        Task ClearCategoryCacheAsync();
        #endregion
    }
}