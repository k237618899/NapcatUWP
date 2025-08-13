using System;
using System.Text;
using System.Threading.Tasks;

namespace AnnaMessager.Core.WebSocket
{
    public interface IWebSocketClient : IDisposable
    {
        bool IsConnected { get; }

        event EventHandler Opened;
        event EventHandler<WebSocketMessageEventArgs> MessageReceived;
        event EventHandler<WebSocketErrorEventArgs> Error;
        event EventHandler<WebSocketClosedEventArgs> Closed;

        Task ConnectAsync(Uri uri);
        Task SendAsync(string message);
        Task SendAsync(byte[] data);
        Task CloseAsync(int closeCode = 1000, string reason = "Normal closure");

        void SetRequestHeader(string name, string value);
    }

    public enum WebSocketMessageType
    {
        Text,
        Binary
    }

    public class WebSocketMessageEventArgs : EventArgs
    {
        public WebSocketMessageEventArgs(WebSocketMessageType messageType, byte[] data)
        {
            MessageType = messageType;
            Data = data;
        }

        public WebSocketMessageType MessageType { get; }
        public byte[] Data { get; }
        public string Text => Data != null ? Encoding.UTF8.GetString(Data, 0, Data.Length) : string.Empty;
    }

    public class WebSocketErrorEventArgs : EventArgs
    {
        public WebSocketErrorEventArgs(Exception exception)
        {
            Exception = exception;
            Message = exception?.Message;
        }

        public WebSocketErrorEventArgs(string message)
        {
            Message = message;
        }

        public Exception Exception { get; }
        public string Message { get; }
    }

    public class WebSocketClosedEventArgs : EventArgs
    {
        public WebSocketClosedEventArgs(int code, string reason)
        {
            Code = code;
            Reason = reason;
        }

        public int Code { get; }
        public string Reason { get; }
    }
}