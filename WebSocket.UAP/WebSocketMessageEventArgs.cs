using System;
using System.Text;

namespace WebSocket.UAP
{
    public sealed class WebSocketMessageEventArgs
    {
        private readonly WebSocketMessageType _messageType;
        private string _text;

        public WebSocketMessageEventArgs(WebSocketMessageType messageType, byte[] data)
        {
            _messageType = messageType;
            Data = data;
        }


        public string Text
        {
            get
            {
                if (_messageType == WebSocketMessageType.Binary)
                    throw new InvalidOperationException("This request message type is binary, Can't convert to Text");
                if (Data != null && string.IsNullOrEmpty(_text)) _text = Encoding.UTF8.GetString(Data, 0, Data.Length);
                return _text;
            }
        }

        public byte[] Data { get; private set; }
    }
}