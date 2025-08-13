using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnnaMessager.Core.WebSocket;
using SystemWebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;
using CoreWebSocketMessageType = AnnaMessager.Core.WebSocket.WebSocketMessageType;

namespace AnnaMessager.iOS.WebSocket
{
    public class iOSWebSocketClient : WebSocketClientBase
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isConnected;
        private ClientWebSocket _webSocket;

        public override bool IsConnected => _isConnected;

        public override async Task ConnectAsync(Uri uri)
        {
            try
            {
                if (_webSocket != null)
                    await CloseAsync();

                _cancellationTokenSource = new CancellationTokenSource();
                _webSocket = new ClientWebSocket();

                // 添加請求標頭
                foreach (var header in _headers)
                    try
                    {
                        _webSocket.Options.SetRequestHeader(header.Key, header.Value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"設置 iOS WebSocket 標頭失敗: {header.Key} = {header.Value}, Error: {ex.Message}");
                    }

                await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
                _isConnected = true;
                OnOpened();

                // 開始接收消息
                _ = Task.Run(ReceiveLoop);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"iOS WebSocket 連接失敗: {ex.Message}");
                _isConnected = false;
                OnError(new WebSocketErrorEventArgs(ex));
            }
        }

        public override async Task SendAsync(string message)
        {
            if (!IsConnected)
                throw new InvalidOperationException("WebSocket is not connected");

            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);
                await _webSocket.SendAsync(segment, SystemWebSocketMessageType.Text, true,
                    _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"iOS WebSocket 發送消息失敗: {ex.Message}");
                OnError(new WebSocketErrorEventArgs(ex));
            }
        }

        public override async Task SendAsync(byte[] data)
        {
            if (!IsConnected)
                throw new InvalidOperationException("WebSocket is not connected");

            try
            {
                var segment = new ArraySegment<byte>(data);
                await _webSocket.SendAsync(segment, SystemWebSocketMessageType.Binary, true,
                    _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"iOS WebSocket 發送二進制消息失敗: {ex.Message}");
                OnError(new WebSocketErrorEventArgs(ex));
            }
        }

        public override async Task CloseAsync(int closeCode = 1000, string reason = "Normal closure")
        {
            if (_webSocket != null && IsConnected)
                try
                {
                    _cancellationTokenSource?.Cancel();
                    await _webSocket.CloseAsync((WebSocketCloseStatus)closeCode, reason, CancellationToken.None);
                    _isConnected = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"iOS WebSocket 關閉失敗: {ex.Message}");
                }
        }

        private async void ReceiveLoop()
        {
            var buffer = new byte[4096];

            while (_webSocket != null && _webSocket.State == WebSocketState.Open &&
                   !_cancellationTokenSource.Token.IsCancellationRequested)
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                        _cancellationTokenSource.Token);

                    if (result.MessageType == SystemWebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var data = Encoding.UTF8.GetBytes(message);
                        OnMessageReceived(new WebSocketMessageEventArgs(CoreWebSocketMessageType.Text, data));
                    }
                    else if (result.MessageType == SystemWebSocketMessageType.Binary)
                    {
                        var data = new byte[result.Count];
                        Array.Copy(buffer, 0, data, 0, result.Count);
                        OnMessageReceived(new WebSocketMessageEventArgs(CoreWebSocketMessageType.Binary, data));
                    }
                    else if (result.MessageType == SystemWebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        OnClosed(new WebSocketClosedEventArgs((int)result.CloseStatus, result.CloseStatusDescription));
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消操作
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"iOS WebSocket 接收消息異常: {ex.Message}");
                    _isConnected = false;
                    OnError(new WebSocketErrorEventArgs(ex));
                    break;
                }
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"釋放 iOS WebSocket CancellationTokenSource 時發生錯誤: {ex.Message}");
                }

                if (_webSocket != null)
                    try
                    {
                        if (_webSocket.State == WebSocketState.Open)
                            _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing",
                                CancellationToken.None).Wait(1000);
                        _webSocket.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"釋放 iOS WebSocket 資源時發生錯誤: {ex.Message}");
                    }
                    finally
                    {
                        _webSocket = null;
                    }
            }

            base.Dispose(disposing);
        }
    }
}