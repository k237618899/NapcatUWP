using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using WebSocket.UAP.ToolBox;

namespace WebSocket.UAP
{
    /// <summary>
    ///     WebSocket
    /// </summary>
    public partial class WebSocket : IWebSocket, IDisposable
    {
        /// <summary>
        ///     强制为true，不能使用Server功能
        /// </summary>
        private const bool IS_CLIENT = true;

        private bool _closed;
        private bool _disposed;
        private List<Http.Header> _extraRequestHeaders;
        private string _host; // set in server websocket to check in onConnect();
        private string _origin; // set in server websocket to check in onConnect();
        private List<Http.Header> _serverResponseHeaders;
        private InternalSocket _socket;

        private IRandomAccessStream _stream = new InMemoryRandomAccessStream();

        private Uri _uri;

        public WebSocket()
        {
            Control = new WebSocketControl();
            Information = new WebSocketInformation();
        }

        public WebSocketControl Control { get; }
        public WebSocketInformation Information { get; private set; }
        public event TypedEventHandler<IWebSocket, WebSocketClosedEventArgs> Closed;

        public IOutputStream OutputStream => _socket.OutputStream;

        public IAsyncAction ConnectAsync(Uri uri)
        {
            _uri = uri;
            return AsyncInfo.Run(token => // CancellationToken token
                Task.Run(
                    async () =>
                    {
                        await ConnectClientInternal();
                        token.ThrowIfCancellationRequested();
                    },
                    token));
        }


        /// <summary>
        ///     设置请求头
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="headerValue"></param>
        public void SetRequestHeader(string headerName, string headerValue)
        {
            if (_extraRequestHeaders == null) _extraRequestHeaders = new List<Http.Header>();
            _extraRequestHeaders.Add(new Http.Header(headerName, headerValue));
        }

        /// <summary>
        ///     释放对象
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async void Close(ushort code, string reason)
        {
            if (_closed) return;
            byte[] data = null;
            if (reason != null && reason.Length > 0)
                try
                {
                    data = Encoding.UTF8.GetBytes(reason);
                }
                catch (Exception e)
                {
                    data = new byte[0];
                }
            else
                data = new byte[0];

            var frame = FrameParser.BuildFrame(data, FrameParser.OP_CLOSE, code, IS_CLIENT, true);
            await SendFrame(frame);
            Disconnect();
        }

        /// <summary>
        ///     连接建立成功
        /// </summary>
        public event EventHandler Opened;

        public event EventHandler<WebSocketMessageEventArgs> OnMessage;
        public event EventHandler<Exception> OnError;
        public event EventHandler<byte[]> OnPong;

        private async Task ProcessIncomingFrame(FrameParser.Frame frame)
        {
            switch (frame.opCode)
            {
                case FrameParser.OP_CONTINUATION:
                    await _stream.WriteAsync(frame.payload?.ToBuffer());
                    if (frame.isFinal)
                    {
                        var buffer = _stream.ToBuffer();
                        var message = buffer.ToByteArray();
                        if (OnMessage != null)
                            OnMessage(this, new WebSocketMessageEventArgs(Control.MessageType, message));
                        ResetStream();
                    }

                    break;
                case FrameParser.OP_TEXT:
                    if (frame.isFinal)
                    {
                        if (OnMessage != null)
                            OnMessage(this, new WebSocketMessageEventArgs(Control.MessageType, frame.payload));
                    }
                    else
                    {
                        if (_stream.Size != 0) throw new IOException("no FIN frame");
                        Control.MessageType = WebSocketMessageType.Text;
                        await _stream.WriteAsync(frame.payload.ToBuffer());
                    }

                    break;
                case FrameParser.OP_BINARY:
                    if (frame.isFinal)
                    {
                        if (OnMessage != null)
                            OnMessage(this, new WebSocketMessageEventArgs(Control.MessageType, frame.payload));
                    }
                    else
                    {
                        if (_stream.Size != 0) throw new IOException("no FIN frame");
                        Control.MessageType = WebSocketMessageType.Binary;
                        await _stream.WriteAsync(frame.payload.ToBuffer());
                    }

                    break;
                case FrameParser.OP_CLOSE:
                    var code = 0;
                    if (frame.payload.Length >= 2)
                        code = ((frame.payload[0] << 8) | (frame.payload[1] & 0xFF)) & 0xFFFF;
                    string reason = null;
                    if (frame.payload.Length > 2)
                    {
                        var temp = frame.payload.CopyOfRange(2, frame.payload.Length);
                        reason = Encoding.UTF8.GetString(temp, 0, temp.Length);
                    }

                    if (Closed != null) Closed(this, new WebSocketClosedEventArgs(code, reason));
                    Disconnect();
                    break;
                case FrameParser.OP_PING:
                    if (frame.payload.Length > 125) throw new IOException("Ping payload too large");
                    var data = FrameParser.BuildFrame(frame.payload, FrameParser.OP_PONG, -1, IS_CLIENT, true);
                    await SendFrame(data);
                    break;
                case FrameParser.OP_PONG:
                    if (OnPong != null) OnPong(this, frame.payload);
                    break;
            }
        }

        private async Task ConnectClientInternal()
        {
            if (_socket != null) throw new Exception("connect() is already called");
            try
            {
                var port = _uri.Port != -1 ? _uri.Port : _uri.Scheme.Equals("wss") ? 443 : 80;
                var path = _uri.AbsolutePath != null ? _uri.AbsolutePath : "/";
                if (_uri.Query != null) path += "?" + _uri.Query;
                var originScheme = _uri.Scheme.Equals("wss") ? "https" : "http";
                var origin = new Uri(originScheme + "://" + _uri.Host);
                // To fix: get Origin from extraHeaders if set there.
                _socket = new InternalSocket();
                await _socket.ConnectAsync(_uri.Host, port);
                if (_uri.Scheme.Equals("wss")) await _socket.UpgradeToSslAsync(_uri.Host);
                var key = WebSocketHelper.CreateClientKey();
                var writer = new DataWriter(_socket.OutputStream);
                writer.WriteString("GET " + path + " HTTP/1.1\r\n");
                writer.WriteString("Upgrade: websocket\r\n");
                writer.WriteString("Connection: Upgrade\r\n");
                writer.WriteString("Host: " + _uri.Host + "\r\n");
                writer.WriteString("Origin: " + origin + "\r\n");
                writer.WriteString("Sec-WebSocket-Key: " + key + "\r\n");
                writer.WriteString("Sec-WebSocket-Version: 13\r\n");
                if (_extraRequestHeaders != null)
                    foreach (var header in _extraRequestHeaders)
                        writer.WriteString(header + "\r\n");

                writer.WriteString("\r\n");
                await writer.StoreAsync(); //异步发送数据
                writer.DetachStream(); //分离
                writer.Dispose(); //结束writer
                var reader = new DataReader(_socket.InputStream);
                reader.ByteOrder = ByteOrder.LittleEndian;
                //// Read HTTP response status line.
                var startLine = await ReadLine(reader);
                if (startLine == null) throw new Exception("Received no reply from server.");
                var statusLine = new Http.StatusLine(startLine);
                var statusCode = statusLine.StatusCode;
                if (statusCode != 101) throw new Exception("wrong HTTP response code: " + statusCode);

                // Read HTTP response headers.
                string line;
                while ((line = await ReadLine(reader)) != null && line.Length > 0)
                {
                    var header = new Http.Header(line);
                    Debug.WriteLine(line);

                    if (header.HeaderName.Equals("Sec-WebSocket-Accept", StringComparison.OrdinalIgnoreCase))
                    {
                        var receivedAccept = header.HeaderValue;
                        var shouldBeAccept = WebSocketHelper.CreateAccept(key);
                        if (!receivedAccept.Equals(shouldBeAccept))
                            throw new Exception("Wrong Sec-WebSocket-Accept: " + receivedAccept + " should be: " +
                                                shouldBeAccept);
                    }

                    if (_serverResponseHeaders == null) _serverResponseHeaders = new List<Http.Header>();
                    _serverResponseHeaders.Add(header);
                }

                if (Opened != null) Opened(this, EventArgs.Empty);
                //Upgrade: websocket
                //Connection: Upgrade
                //Sec-WebSocket-Accept: 1xY289lHcEMbLpEBgOYRBBL9N9c=
                //Sec-WebSocket-Protocol: chat
                //Content-Type: application/octet-stream
                //Seq-Id: 667035124
                // Read & process frame
                while (true)
                {
                    var frame = await FrameParser.ReadFrame(reader);
                    if (frame != null) await ProcessIncomingFrame(frame);
                }
            }
            catch (IOException ex)
            {
                if (Closed != null) Closed(this, new WebSocketClosedEventArgs(0, "EOF"));
            }
            catch (Exception ex)
            {
                if (Closed != null) Closed(this, new WebSocketClosedEventArgs(0, ex.Message));
            }
            finally
            {
                Disconnect();
            }
        }

        private void Disconnect()
        {
            if (_socket != null)
                try
                {
                    _socket.Dispose();
                    _socket = null;
                    _closed = true;
                }
                catch (Exception ex)
                {
                    if (OnError != null) OnError(this, ex);
                }
        }

        public async Task Send(string str)
        {
            await SendFragment(str, true, true);
        }

        public async Task Send(byte[] data)
        {
            await SendFragment(data, true, true);
        }

        public async Task SendFragment(string str, bool isFirst, bool isLast)
        {
            if (_closed) return;
            var data = Encoding.UTF8.GetBytes(str);
            var frame = FrameParser.BuildFrame(data, isFirst ? FrameParser.OP_TEXT : FrameParser.OP_CONTINUATION, -1,
                IS_CLIENT, isLast);
            await SendFrame(frame);
        }

        public async Task SendFragment(byte[] data, bool isFirst, bool isLast)
        {
            if (_closed) return;
            var frame = FrameParser.BuildFrame(data, isFirst ? FrameParser.OP_BINARY : FrameParser.OP_CONTINUATION, -1,
                IS_CLIENT, isLast);
            await SendFrame(frame);
        }

        /// <summary>
        ///     Ping
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task Ping(byte[] data = null)
        {
            if (_closed) return;
            var frame = FrameParser.BuildFrame(data, FrameParser.OP_PING, -1, IS_CLIENT, true);
            await SendFrame(frame);
        }


        /// <summary>
        ///     获取返回头
        /// </summary>
        /// <param name="headerName"></param>
        /// <returns></returns>
        public string GetResponseHeader(string headerName)
        {
            if (_serverResponseHeaders == null || _serverResponseHeaders.Count == 0) return null;
            foreach (var item in _serverResponseHeaders)
                if (item.HeaderName != null && item.HeaderName.Equals(headerName))
                    return item.HeaderValue;

            return null;
        }

        ~WebSocket()
        {
            Dispose(false);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
                if (_socket != null)
                {
                    _socket.Dispose();
                    _socket = null;
                }

            _disposed = true;
        }

        /// <summary>
        ///     Send Frame
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        private async Task SendFrame(byte[] frame)
        {
            try
            {
                var outputStream = _socket.OutputStream;
                var writer = new DataWriter(outputStream);
                writer.WriteBytes(frame);
                await writer.StoreAsync(); //异步发送数据
                writer.DetachStream(); //分离
                writer.Dispose(); //结束writer
            }
            catch (IOException ex)
            {
                if (OnError != null) OnError(this, ex);
            }
        }

        /// <summary>
        ///     重置Stream
        /// </summary>
        private void ResetStream()
        {
            Control.MessageType = default(WebSocketMessageType);
            _stream?.Dispose();
            _stream = new InMemoryRandomAccessStream();
        }

        private async Task<string> ReadLine(DataReader reader)
        {
            var stringBuilder = new StringBuilder();
            await reader.LoadAsync(sizeof(byte));

            int readChar = reader.ReadByte();
            if (readChar == -1) return null;
            while (readChar != '\n')
            {
                if (readChar != '\r') stringBuilder.Append((char)readChar);
                await reader.LoadAsync(sizeof(byte));
                readChar = reader.ReadByte();
                if (readChar == -1) return null;
            }

            return stringBuilder.ToString();
        }
    }
}