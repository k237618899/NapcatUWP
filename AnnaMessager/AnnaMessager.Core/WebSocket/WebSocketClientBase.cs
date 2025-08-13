using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnnaMessager.Core.WebSocket
{
    public abstract class WebSocketClientBase : IWebSocketClient
    {
        protected readonly Dictionary<string, string> _headers;
        protected bool _disposed;

        public WebSocketClientBase()
        {
            _headers = new Dictionary<string, string>();
        }

        public abstract bool IsConnected { get; }

        public event EventHandler Opened;
        public event EventHandler<WebSocketMessageEventArgs> MessageReceived;
        public event EventHandler<WebSocketErrorEventArgs> Error;
        public event EventHandler<WebSocketClosedEventArgs> Closed;

        public abstract Task ConnectAsync(Uri uri);
        public abstract Task SendAsync(string message);
        public abstract Task SendAsync(byte[] data);
        public abstract Task CloseAsync(int closeCode = 1000, string reason = "Normal closure");

        public virtual void SetRequestHeader(string name, string value)
        {
            _headers[name] = value;
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void OnOpened()
        {
            Opened?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnMessageReceived(WebSocketMessageEventArgs args)
        {
            MessageReceived?.Invoke(this, args);
        }

        protected virtual void OnError(WebSocketErrorEventArgs args)
        {
            Error?.Invoke(this, args);
        }

        protected virtual void OnClosed(WebSocketClosedEventArgs args)
        {
            Closed?.Invoke(this, args);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
                // 清理託管資源
                _headers.Clear();

            _disposed = true;
        }
    }
}