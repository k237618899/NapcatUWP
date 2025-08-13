using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using AnnaMessager.Core.WebSocket;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;
using UwpWebSocketClosedEventArgs = Windows.Networking.Sockets.WebSocketClosedEventArgs;
using WebSocketClosedEventArgs = AnnaMessager.Core.WebSocket.WebSocketClosedEventArgs;

namespace AnnaMessager.UWP.WebSocket
{
    public class UwpWebSocketClient : WebSocketClientBase
    {
        private bool _isConnected;
        private MessageWebSocket _webSocket;

        public override bool IsConnected => _isConnected;

        public override async Task ConnectAsync(Uri uri)
        {
            try
            {
                if (_webSocket != null)
                    await CloseAsync();

                _webSocket = new MessageWebSocket();

                // 設置請求標頭
                foreach (var header in _headers)
                    try
                    {
                        _webSocket.SetRequestHeader(header.Key, header.Value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"設置標頭 {header.Key} 失敗: {ex.Message}");
                    }

                // 註冊事件處理器
                _webSocket.MessageReceived += OnMessageReceived;
                _webSocket.Closed += OnWebSocketClosed;

                await _webSocket.ConnectAsync(uri);
                _isConnected = true;
                OnOpened();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UWP WebSocket 連接失敗: {ex.Message}");
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
                using (var dataWriter = new DataWriter(_webSocket.OutputStream))
                {
                    dataWriter.WriteString(message);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UWP WebSocket 發送文字消息失敗: {ex.Message}");
                OnError(new WebSocketErrorEventArgs(ex));
            }
        }

        public override async Task SendAsync(byte[] data)
        {
            if (!IsConnected)
                throw new InvalidOperationException("WebSocket is not connected");

            try
            {
                using (var dataWriter = new DataWriter(_webSocket.OutputStream))
                {
                    dataWriter.WriteBytes(data);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UWP WebSocket 發送二進制消息失敗: {ex.Message}");
                OnError(new WebSocketErrorEventArgs(ex));
            }
        }

        public override async Task CloseAsync(int closeCode = 1000, string reason = "Normal closure")
        {
            if (_webSocket != null && IsConnected)
                try
                {
                    _webSocket.Close((ushort)closeCode, reason);
                    _isConnected = false;
                    await Task.Delay(100); // 給一點時間讓關閉完成
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"UWP WebSocket 關閉失敗: {ex.Message}");
                }
        }

        private void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                string message;
                using (var dataReader = args.GetDataReader())
                {
                    dataReader.UnicodeEncoding = UnicodeEncoding.Utf8;
                    message = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                }

                var data = Encoding.UTF8.GetBytes(message);
                var eventArgs = new WebSocketMessageEventArgs(WebSocketMessageType.Text, data);
                OnMessageReceived(eventArgs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理 UWP WebSocket 消息時發生錯誤: {ex.Message}");
                OnError(new WebSocketErrorEventArgs(ex));
            }
        }

        // 修正：使用正確的 UWP WebSocket 事件簽名
        private void OnWebSocketClosed(IWebSocket sender, UwpWebSocketClosedEventArgs args)
        {
            _isConnected = false;
            Debug.WriteLine($"UWP WebSocket 連接關閉: {args.Code} - {args.Reason}");

            // 轉換為自定義的 WebSocketClosedEventArgs
            OnClosed(new WebSocketClosedEventArgs((int)args.Code, args.Reason));
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
                if (_webSocket != null)
                    try
                    {
                        // 取消註冊事件處理器
                        _webSocket.MessageReceived -= OnMessageReceived;
                        _webSocket.Closed -= OnWebSocketClosed;

                        _webSocket.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"釋放 UWP WebSocket 資源時發生錯誤: {ex.Message}");
                    }
                    finally
                    {
                        _webSocket = null;
                    }

            base.Dispose(disposing);
        }
    }
}