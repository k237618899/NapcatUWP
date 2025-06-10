namespace WebSocket.UAP
{
    public sealed class WebSocketClosedEventArgs
    {
        internal WebSocketClosedEventArgs(int code, string reason)
        {
            Code = code;
            Reason = reason;
        }

        public int Code { get; private set; }
        public string Reason { get; private set; }
    }
}