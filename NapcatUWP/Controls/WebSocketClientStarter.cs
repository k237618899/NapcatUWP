using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.UI.Core;
using NapcatUWP.Controls;
using NapcatUWP.Controls.APIHandler;
using NapcatUWP.Models;
using NapcatUWP.Pages;
using NapcatUWP.Tools;
using WebSocket.UAP;

public class WebSocketClientStarter
{
    private readonly OneBotAPIHandler _apiHandler = new OneBotAPIHandler();
    public readonly WebSocket.UAP.WebSocket _socket = new WebSocket.UAP.WebSocket();
    private Timer _refreshTimer; // 添加定时器
    public bool IsConnected;

    public WebSocketClientStarter()
    {
        _socket = new WebSocket.UAP.WebSocket();

        _socket.Opened += socket_Opened;
        _socket.Closed += socket_Closed;
        _socket.OnPong += socket_OnPong;
        _socket.OnMessage += socket_OnMessage;
        _socket.OnError += socket_OnError;
    }

    // 在 SocketClientStarter 類中添加方法
    public void RequestGroupMemberInfo(long groupId, long userId)
    {
        _apiHandler?.RequestGroupMemberInfo(groupId, userId);
    }

    /// <summary>
    ///     設置 MainView 引用給 API 處理器 - 修改版本
    /// </summary>
    /// <param name="mainView">MainView 實例</param>
    public void SetMainViewReference(MainView mainView)
    {
        _apiHandler.SetMainView(mainView);
    }


    /// <summary>
    ///     启动定期刷新联系人信息
    /// </summary>
    public void StartPeriodicRefresh()
    {
        try
        {
            // 每5分钟刷新一次群组和好友列表
            _refreshTimer = new Timer(
                _ => _apiHandler?.RefreshGroupAndFriendListPeriodically(),
                null,
                TimeSpan.FromMinutes(5), // 5分钟后开始
                TimeSpan.FromMinutes(5) // 每5分钟执行一次
            );

            Debug.WriteLine("定期刷新计时器已启动");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动定期刷新时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    ///     停止定期刷新
    /// </summary>
    public void StopPeriodicRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        Debug.WriteLine("定期刷新计时器已停止");
    }

    /// <summary>
    ///     智能合併聊天列表 - 保留已載入的頭像
    /// </summary>
    public void SmartMergeChatListWithAvatars(List<ChatItem> newChatItems)
    {
        try
        {
            if (newChatItems == null || newChatItems.Count == 0)
            {
                Debug.WriteLine("智能合併聊天列表: 沒有新的聊天項目");
                return;
            }

            Debug.WriteLine($"開始智能合併聊天列表: {newChatItems.Count} 個新項目");

            // 首先升級數據庫表結構（如果需要的話）
            DataAccess.UpgradeChatListCacheTable();

            // 預處理：為新的聊天項目設置頭像緩存鍵
            foreach (var chatItem in newChatItems)
            {
                var avatarType = chatItem.IsGroup ? "group" : "friend";
                var avatarCacheKey = $"{avatarType}_{chatItem.ChatId}";

                // 檢查是否有現有的頭像緩存
                var cacheInfo = DataAccess.GetAvatarCacheInfo(avatarCacheKey);
                if (cacheInfo != null && !string.IsNullOrEmpty(cacheInfo.LocalPath) && File.Exists(cacheInfo.LocalPath))
                    // 異步載入已緩存的頭像
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var avatar = await AvatarManager.LoadImageFromFileAsync(cacheInfo.LocalPath);
                            if (avatar != null)
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                    CoreDispatcherPriority.High, () =>
                                    {
                                        chatItem.AvatarImage = avatar;
                                        Debug.WriteLine($"合併時預載入頭像成功: {avatarCacheKey}");
                                    });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"合併時預載入頭像失敗: {avatarCacheKey}, {ex.Message}");
                        }
                    });
                else
                    // 如果沒有緩存，觸發低優先級後台載入
                    chatItem.LoadAvatarFromCacheAsync();
            }

            // 通知 MainView 進行智能合併
            _apiHandler?.SmartMergeChatList(newChatItems);

            Debug.WriteLine("智能合併聊天列表完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"智能合併聊天列表時發生錯誤: {ex.Message}");
        }
    }

    /// <summary>
    ///     優化的頭像預載入流程
    /// </summary>
    public async Task PreloadChatAvatarsOptimized()
    {
        try
        {
            Debug.WriteLine("開始優化的頭像預載入流程");

            // 獲取當前帳號
            var currentAccount = DataAccess.GetCurrentAccount();
            if (string.IsNullOrEmpty(currentAccount))
            {
                Debug.WriteLine("預載入頭像: 無效的當前帳號");
                return;
            }

            // 從緩存載入聊天列表
            var cachedChatItems = DataAccess.LoadChatListCacheWithAvatars(currentAccount);
            if (cachedChatItems.Count == 0)
            {
                Debug.WriteLine("預載入頭像: 沒有緩存的聊天項目");
                return;
            }

            // 分批處理頭像載入，避免同時載入太多
            const int batchSize = 5;
            var batches = cachedChatItems
                .Where(item => item.AvatarImage == null) // 只處理還沒有頭像的項目
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                var loadTasks = batch.Select(chatItem => LoadAvatarForChatItem(chatItem)).ToList();
                await Task.WhenAll(loadTasks);

                // 批次間短暫延遲，避免過載
                await Task.Delay(100);
            }

            Debug.WriteLine($"優化頭像預載入完成: 處理了 {batches.Sum(b => b.Count)} 個聊天項目");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"優化頭像預載入時發生錯誤: {ex.Message}");
        }
    }

    /// <summary>
    ///     為特定聊天項目載入頭像
    /// </summary>
    private async Task LoadAvatarForChatItem(ChatItem chatItem)
    {
        try
        {
            var avatarType = chatItem.IsGroup ? "group" : "friend";
            var avatar = await AvatarManager.GetAvatarAsync(avatarType, chatItem.ChatId, 2, true);

            if (avatar != null)
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        if (chatItem.AvatarImage == null) // 防止重複設置
                        {
                            chatItem.AvatarImage = avatar;
                            Debug.WriteLine($"批次載入頭像成功: {avatarType}_{chatItem.ChatId}");
                        }
                    });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"為聊天項目載入頭像失敗: {ex.Message}");
        }
    }

    public async void WebSocketConnet(string connectionURI, string token)
    {
        try
        {
            Debug.WriteLine($"正在連線到 WebSocket: {connectionURI}");
            _socket.SetRequestHeader("Authorization", $"Bearer {token}");

            await _socket.ConnectAsync(new Uri(connectionURI));
            Debug.WriteLine("WebSocket 連線請求已發送");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebSocket 連線失敗: {ex.Message}");
            Debug.WriteLine($"異常類型: {ex.GetType().Name}");
            Debug.WriteLine($"堆疊追蹤: {ex.StackTrace}");
            IsConnected = false;
        }
    }

    private void socket_OnError(object sender, Exception e)
    {
        Debug.WriteLine($"WebSocket 錯誤: {e.Message}");
        Debug.WriteLine($"錯誤類型: {e.GetType().Name}");
        if (e.InnerException != null) Debug.WriteLine($"內部異常: {e.InnerException.Message}");
        IsConnected = false;
    }


    private void socket_OnMessage(object sender, WebSocketMessageEventArgs e)
    {
        Debug.WriteLine("收到信息：" + Encoding.UTF8.GetString(e.Data));
        _apiHandler.IncomingTask(Encoding.UTF8.GetString(e.Data));
    }

    private void socket_OnPong(object sender, byte[] e)
    {
        Debug.WriteLine("WebSocket Pong: " + e.Length);
    }

    private void socket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
    {
        IsConnected = false;
        StopPeriodicRefresh();
        Debug.WriteLine($"WebSocket 已關閉！原因: {args.Reason}");
        Debug.WriteLine($"狀態碼: {args.Code}");
    }

    private void socket_Opened(object sender, EventArgs e)
    {
        IsConnected = true;

        // 連接成功後立即請求登入信息（觸發優化的載入流程）
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // 短暫延遲確保連接穩定
            try
            {
                var loginAction = JSONTools.ActionToJSON("get_login_info", new JsonObject(), "login_info");
                await _socket.Send(loginAction);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"請求登入信息失敗: {ex.Message}");
            }
        });

        StartPeriodicRefresh();
        Debug.WriteLine("WebSocket Connected!");
    }
}