using System;
using WebSocket.UAP.Internal;

namespace WebSocket.UAP
{
    public class Http
    {
        // message-header = field-name ":" [ field-value ]
        public class Header
        {
            public Header(string headerName, string headerValue)
            {
                this.HeaderName = headerName;
                this.HeaderValue = headerValue;
            }

            public Header(string line)
            {
                var colon = line.IndexOf(':');
                if (colon == -1) throw new Exception("http header without ':', line=" + line);
                HeaderName = line.Substring(0, colon).Trim();
                HeaderValue = line.Substring(colon + 1).Trim();
            }

            public string HeaderName { get; }

            public string HeaderValue { get; }

            public override string ToString()
            {
                return HeaderName + ": " + HeaderValue;
            }
        }

        // Method SP Request-URI SP HTTP-Version CRLF
        public class RequestLine
        {
            public RequestLine(string method, string requestURI, string httpVersion)
            {
                this.Method = method;
                this.RequestURI = requestURI;
                this.HttpVersion = httpVersion;
            }

            public RequestLine(string line)
            {
                var st = new StringTokenizer(line);
                Method = st.NextToken();
                RequestURI = st.NextToken();
                HttpVersion = st.NextToken();
            }

            public string Method { get; }


            public string RequestURI { get; }

            public string HttpVersion { get; }

            public override string ToString()
            {
                return Method + " " + RequestURI + " " + HttpVersion;
            }
        }

        public class StatusLine
        {
            public StatusLine(string httpVersion, int statusCode, string reasonPhrase)
            {
                if (statusCode < 100 || statusCode > 999)
                    throw new Exception("status code must be XXX");
                this.HttpVersion = httpVersion;
                this.StatusCode = statusCode;
                this.ReasonPhrase = reasonPhrase;
            }

            public StatusLine(string line)
            {
                var colon1 = line.IndexOf(' ');
                if (colon1 == -1)
                    throw new Exception("wrong status line - no the 1st space");
                HttpVersion = line.Substring(0, colon1);
                var colon2 = line.IndexOf(' ', colon1 + 1);
                if (colon2 == -1)
                    throw new Exception("wrong status line - no the 2nd space");
                var strStatusCode = line.Substring(colon1 + 1, colon2 - colon1 - 1);
                StatusCode = Convert.ToInt32(strStatusCode);
                if (StatusCode < 100 || StatusCode > 999)
                    throw new Exception("status code must be XXX");
                ReasonPhrase = line.Substring(colon2 + 1);
            }

            public string HttpVersion { get; }

            public int StatusCode { get; }

            public string ReasonPhrase { get; }

            public override string ToString()
            {
                return HttpVersion + " " + StatusCode + " " + ReasonPhrase;
            }
        }
    }
}