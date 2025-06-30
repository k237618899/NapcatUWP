using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using NapcatUWP.Controls.APIHandler;
using NapcatUWP.Pages;
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
    ///     設置 MainView 引用給 API 處理器
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

    public async void WebSocketConnet(string connectionURI, string token)
    {
        _socket.SetRequestHeader("Authorization", $"Bearer {token}");
        await _socket.ConnectAsync(new Uri(connectionURI));
    }

    private void socket_OnError(object sender, Exception e)
    {
        Debug.WriteLine("WebSocket Error: " + e.Message);
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
        StopPeriodicRefresh(); // 连接断开时停止定期刷新
        Debug.WriteLine("WebSocket Closed! Reason:" + args.Reason);
    }

    private void socket_Opened(object sender, EventArgs e)
    {
        IsConnected = true;
        StartPeriodicRefresh(); // 连接成功时启动定期刷新
        Debug.WriteLine("WebSocket Connected!" + e);
    }
}