using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace WebSocket.UAP
{
    public partial class WebSocket
    {
        private void ConnectServer()
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var stream = new DataReader(_socket.InputStream);

                    // Read HTTP request line.
                    var startLine = await ReadLine(stream);
                    if (startLine == null) throw new Exception("Cannot read HTTP request start line");

                    var requestLine = new Http.RequestLine(startLine);
                    _uri = new Uri(requestLine.RequestURI); // can be checked in
                    // onConnect()

                    // Read HTTP response headers
                    var map = new Dictionary<string, string>();
                    string line;
                    while ((line = await ReadLine(stream)) != null && line.Length > 0)
                    {
                        var header = new Http.Header(line);
                        map.Add(header.HeaderName.ToLower(), header.HeaderValue);
                    }

                    var value = map["sec-websocket-version"];
                    if (!"13".Equals(value))
                        throw new IOException("wrong Sec-WebSocket-Version");

                    var key = map["sec-websocket-key"];
                    if (key == null)
                        throw new IOException("missed Sec-WebSocket-Key");
                    var accept = WebSocketHelper.CreateAccept(key);

                    var upgrade = map["upgrade"];
                    if (upgrade == null || !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase))
                        throw new IOException("wrong Upgrade");

                    var connection = map["connection"];
                    if (connection == null || !connection.Equals("upgrade", StringComparison.OrdinalIgnoreCase))
                        throw new IOException("wrong Connection");

                    // Host and Origin can be checked later in onConnect() callback.
                    _host = map["host"];
                    if (_host == null)
                        throw new IOException("Missed 'Host' header");

                    _origin = map["origin"];
                    if (_origin == null)
                        throw new IOException("Missed 'Origin' header");

                    // Some naive protocol selection.
                    var protocols = map["sec-websocket-protocol"];
                    string selectedProtocol = null;
                    if (protocols != null && protocols.Contains("chat"))
                        selectedProtocol = "chat";

                    var writer = new DataWriter(_socket.OutputStream);
                    writer.WriteString("HTTP/1.1 101 Switching Protocols\r\n");
                    writer.WriteString("Upgrade: websocket\r\n");
                    writer.WriteString("Connection: Upgrade\r\n");
                    writer.WriteString("Sec-WebSocket-Accept: " + accept + "\r\n");
                    if (selectedProtocol != null)
                        writer.WriteString("Sec-WebSocket-Protocol: " + selectedProtocol + "\r\n");
                    writer.WriteString("\r\n");
                    await writer.FlushAsync();

                    if (Opened != null) Opened(this, EventArgs.Empty);

                    // Read & process frame
                    for (;;)
                    {
                        var frame = await FrameParser.ReadFrame(stream);
                        await ProcessIncomingFrame(frame);
                    }
                }
                catch (IOException ex)
                {
                    if (Closed != null) Closed(this, new WebSocketClosedEventArgs(0, "EOF"));
                }
                catch (Exception ex)
                {
                    if (Closed != null) Closed(this, new WebSocketClosedEventArgs(0, "Exception"));
                }
                finally
                {
                    Disconnect();
                }
            });
        }
    }
}