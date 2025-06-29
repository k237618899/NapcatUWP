using System;
using System.Diagnostics;
using System.Text;
using NapcatUWP.Controls.APIHandler;
using WebSocket.UAP;

namespace NapcatUWP.Controls
{
    public class WebSocketClientStarter
    {
        public readonly WebSocket.UAP.WebSocket _socket = new WebSocket.UAP.WebSocket();
        public bool IsConnected = false;
        private OneBotAPIHandler _apiHandler = new OneBotAPIHandler();

        public WebSocketClientStarter()
        {
            _socket = new WebSocket.UAP.WebSocket();
            
            _socket.Opened += socket_Opened;
            _socket.Closed += socket_Closed;
            _socket.OnPong += socket_OnPong;
            _socket.OnMessage += socket_OnMessage;
            _socket.OnError += socket_OnError;
            
        }

       

        public async void WebSocketConnet(string connectionURI, string token)
        {
            _socket.SetRequestHeader("Authorization", $"Bearer {token}");
            await _socket.ConnectAsync(new Uri(connectionURI));
        }

        private void socket_OnError(object sender, Exception e)
        {
            Debug.WriteLine("111" + e.Message);
        }

        private void socket_OnMessage(object sender, WebSocketMessageEventArgs e)
        {
            Debug.WriteLine("收到信息：" + Encoding.UTF8.GetString(e.Data));
            _apiHandler.IncomingTask(Encoding.UTF8.GetString(e.Data));
        }

        private void socket_OnPong(object sender, byte[] e)
        {
            Debug.WriteLine("111" + e.Length);
        }

        private void socket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            IsConnected = false;
            Debug.WriteLine("WebSocket Closed! Reason:" + args.Reason);
        }

        private void socket_Opened(object sender, EventArgs e)
        {
            IsConnected = true;
            Debug.WriteLine("WebSocket Connected!" + e);

        }
    }
}