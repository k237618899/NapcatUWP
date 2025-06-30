using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using NapcatUWP.Controls;
using Newtonsoft.Json;

namespace NapcatUWP.Models
{
    /// <summary>
    ///     OneBot 11 ��Ϣ�λ��
    /// </summary>
    public class MessageSegment : INotifyPropertyChanged
    {
        private Dictionary<string, object> _data;
        private string _type;

        public MessageSegment()
        {
            _data = new Dictionary<string, object>();
        }

        public MessageSegment(string type) : this()
        {
            Type = type;
        }

        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
            }
        }

        [JsonProperty("data")]
        public Dictionary<string, object> Data
        {
            get => _data ?? (_data = new Dictionary<string, object>());
            set
            {
                _data = value;
                OnPropertyChanged(nameof(Data));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    ///     �ı���Ϣ��
    /// </summary>
    public class TextSegment : MessageSegment
    {
        public TextSegment() : base("text")
        {
        }

        public TextSegment(string text) : base("text")
        {
            Data["text"] = text;
        }

        public string Text => Data.ContainsKey("text") ? Data["text"]?.ToString() ?? "" : "";
    }

    /// <summary>
    ///     @�ἰ��Ϣ��
    /// </summary>
    public class AtSegment : MessageSegment
    {
        private string _cachedDisplayText; // ������@ʾ�ı�
        private long _groupId; // ���Ⱥ�MID����

        public AtSegment() : base("at")
        {
        }

        public AtSegment(string qq) : base("at")
        {
            Data["qq"] = qq;
        }

        public AtSegment(string qq, long groupId) : base("at")
        {
            Data["qq"] = qq;
            _groupId = groupId;
        }

        public string QQ => Data.ContainsKey("qq") ? Data["qq"]?.ToString() ?? "" : "";
        public bool IsAtAll => QQ == "all";

        /// <summary>
        ///     �O��Ⱥ�MID����춲�ԃ�ɆT��Ϣ��
        /// </summary>
        public long GroupId
        {
            get => _groupId;
            set
            {
                _groupId = value;
                _cachedDisplayText = null; // ��վ��棬��������Ӌ��
                OnPropertyChanged(nameof(GroupId));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        ///     �@ȡ�@ʾ�ı��������@ʾȺ�M�ɆT���Q��
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedDisplayText))
                    return _cachedDisplayText;

                if (IsAtAll)
                {
                    _cachedDisplayText = "@ȫ�w�ɆT";
                    return _cachedDisplayText;
                }

                if (long.TryParse(QQ, out var userId) && _groupId > 0)
                    // �Lԇ�Ĕ�����@ȡȺ�M�ɆT��Ϣ
                    try
                    {
                        var displayName = DataAccess.GetGroupMemberDisplayName(_groupId, userId);
                        _cachedDisplayText = $"@{displayName}";
                        return _cachedDisplayText;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"�@ȡȺ�M�ɆT�@ʾ���Q�r�l���e�`: {ex.Message}");
                    }

                _cachedDisplayText = $"@{QQ}";
                return _cachedDisplayText;
            }
        }

        /// <summary>
        ///     ˢ���@ʾ�ı�����Ⱥ�M�ɆT��Ϣ�������{�ã�
        /// </summary>
        public void RefreshDisplayText()
        {
            _cachedDisplayText = null; // ��վ���
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>
    ///     ������Ϣ��
    /// </summary>
    public class FaceSegment : MessageSegment
    {
        public FaceSegment() : base("face")
        {
        }

        public FaceSegment(string id) : base("face")
        {
            Data["id"] = id;
        }

        public string Id => Data.ContainsKey("id") ? Data["id"]?.ToString() ?? "" : "";
    }

    /// <summary>
    ///     �DƬ��Ϣ��
    /// </summary>
    public class ImageSegment : MessageSegment
    {
        public ImageSegment() : base("image")
        {
        }

        public ImageSegment(string file) : base("image")
        {
            Data["file"] = file;
        }

        public string File => Data.ContainsKey("file") ? Data["file"]?.ToString() ?? "" : "";
        public string Url => Data.ContainsKey("url") ? Data["url"]?.ToString() ?? "" : "";

        public bool Cache
        {
            get
            {
                if (Data.ContainsKey("cache") && Data["cache"] != null)
                    if (bool.TryParse(Data["cache"].ToString(), out var cache))
                        return cache;
                return false;
            }
        }

        public bool Proxy
        {
            get
            {
                if (Data.ContainsKey("proxy") && Data["proxy"] != null)
                    if (bool.TryParse(Data["proxy"].ToString(), out var proxy))
                        return proxy;
                return false;
            }
        }

        public int Timeout
        {
            get
            {
                if (Data.ContainsKey("timeout") && Data["timeout"] != null)
                    if (int.TryParse(Data["timeout"].ToString(), out var timeout))
                        return timeout;
                return 0;
            }
        }
    }

    /// <summary>
    ///     �Z����Ϣ��
    /// </summary>
    public class RecordSegment : MessageSegment
    {
        public RecordSegment() : base("record")
        {
        }

        public RecordSegment(string file) : base("record")
        {
            Data["file"] = file;
        }

        public string File => Data.ContainsKey("file") ? Data["file"]?.ToString() ?? "" : "";
        public string Url => Data.ContainsKey("url") ? Data["url"]?.ToString() ?? "" : "";

        public bool Magic
        {
            get
            {
                if (Data.ContainsKey("magic") && Data["magic"] != null)
                    if (bool.TryParse(Data["magic"].ToString(), out var magic))
                        return magic;
                return false;
            }
        }

        public bool Cache
        {
            get
            {
                if (Data.ContainsKey("cache") && Data["cache"] != null)
                    if (bool.TryParse(Data["cache"].ToString(), out var cache))
                        return cache;
                return false;
            }
        }

        public bool Proxy
        {
            get
            {
                if (Data.ContainsKey("proxy") && Data["proxy"] != null)
                    if (bool.TryParse(Data["proxy"].ToString(), out var proxy))
                        return proxy;
                return false;
            }
        }

        public int Timeout
        {
            get
            {
                if (Data.ContainsKey("timeout") && Data["timeout"] != null)
                    if (int.TryParse(Data["timeout"].ToString(), out var timeout))
                        return timeout;
                return 0;
            }
        }
    }

    /// <summary>
    ///     ҕ�l��Ϣ��
    /// </summary>
    public class VideoSegment : MessageSegment
    {
        public VideoSegment() : base("video")
        {
        }

        public VideoSegment(string file) : base("video")
        {
            Data["file"] = file;
        }

        public string File => Data.ContainsKey("file") ? Data["file"]?.ToString() ?? "" : "";
        public string Url => Data.ContainsKey("url") ? Data["url"]?.ToString() ?? "" : "";

        public bool Cache
        {
            get
            {
                if (Data.ContainsKey("cache") && Data["cache"] != null)
                    if (bool.TryParse(Data["cache"].ToString(), out var cache))
                        return cache;
                return false;
            }
        }

        public bool Proxy
        {
            get
            {
                if (Data.ContainsKey("proxy") && Data["proxy"] != null)
                    if (bool.TryParse(Data["proxy"].ToString(), out var proxy))
                        return proxy;
                return false;
            }
        }

        public int Timeout
        {
            get
            {
                if (Data.ContainsKey("timeout") && Data["timeout"] != null)
                    if (int.TryParse(Data["timeout"].ToString(), out var timeout))
                        return timeout;
                return 0;
            }
        }
    }

    /// <summary>
    ///     �ļ���Ϣ��
    /// </summary>
    public class FileSegment : MessageSegment
    {
        public FileSegment() : base("file")
        {
        }

        public FileSegment(string file) : base("file")
        {
            Data["file"] = file;
        }

        public string File => Data.ContainsKey("file") ? Data["file"]?.ToString() ?? "" : "";
        public string Url => Data.ContainsKey("url") ? Data["url"]?.ToString() ?? "" : "";
    }

    /// <summary>
    ///     �؏���Ϣ��
    /// </summary>
    public class ReplySegment : MessageSegment
    {
        public ReplySegment() : base("reply")
        {
        }

        public ReplySegment(string id) : base("reply")
        {
            Data["id"] = id;
        }

        public string Id => Data.ContainsKey("id") ? Data["id"]?.ToString() ?? "" : "";

        /// <summary>
        ///     �o�BίӚ�����Ո��@ȡ��Ϣ����
        /// </summary>
        public static Action<long> RequestMessageContentDelegate { get; set; }

        /// <summary>
        ///     �@ȡ�؏���Ϣ�ă��ݣ��o�BίӚ��ʽ��
        /// </summary>
        public string GetReplyContent()
        {
            if (string.IsNullOrEmpty(Id) || !long.TryParse(Id, out var messageId))
                return $"�ظ� #{Id}";

            // �ȏĔ��������
            var message = DataAccess.GetMessageById(messageId);
            if (message != null)
            {
                // �����Ϣ�����^�L����ȡǰ50���ַ�
                var content = message.Content ?? "";
                if (content.Length > 50)
                    content = content.Substring(0, 50) + "...";
                return $"�ظ� {message.SenderName}: {content}";
            }

            // ����������Л]�У�֪ͨՈ��@ȡ��Ϣ����
            RequestMessageContentDelegate?.Invoke(messageId);
            return $"�ظ� #{messageId}"; // ���r�@ʾID���ȴ�API푑�
        }
    }

    /// <summary>
    ///     ��һ����Ϣ��
    /// </summary>
    public class PokeSegment : MessageSegment
    {
        public PokeSegment() : base("poke")
        {
        }

        public PokeSegment(string type, string id) : base("poke")
        {
            Data["type"] = type;
            Data["id"] = id;
        }

        public string PokeType => Data.ContainsKey("type") ? Data["type"]?.ToString() ?? "" : "";
        public string Id => Data.ContainsKey("id") ? Data["id"]?.ToString() ?? "" : "";
    }

    /// <summary>
    ///     �Y����Ϣ��
    /// </summary>
    public class GiftSegment : MessageSegment
    {
        public GiftSegment() : base("gift")
        {
        }

        public GiftSegment(string qq, string id) : base("gift")
        {
            Data["qq"] = qq;
            Data["id"] = id;
        }

        public string QQ => Data.ContainsKey("qq") ? Data["qq"]?.ToString() ?? "" : "";
        public string Id => Data.ContainsKey("id") ? Data["id"]?.ToString() ?? "" : "";
    }

    /// <summary>
    ///     �D�l��Ϣ��
    /// </summary>
    public class ForwardSegment : MessageSegment
    {
        public ForwardSegment() : base("forward")
        {
        }

        public ForwardSegment(string id) : base("forward")
        {
            Data["id"] = id;
        }

        public string Id => Data.ContainsKey("id") ? Data["id"]?.ToString() ?? "" : "";
    }

    /// <summary>
    ///     ���c��Ϣ��
    /// </summary>
    public class NodeSegment : MessageSegment
    {
        public NodeSegment() : base("node")
        {
        }

        public NodeSegment(string id) : base("node")
        {
            Data["id"] = id;
        }

        public string Id => Data.ContainsKey("id") ? Data["id"]?.ToString() ?? "" : "";
        public string UserId => Data.ContainsKey("user_id") ? Data["user_id"]?.ToString() ?? "" : "";
        public string Nickname => Data.ContainsKey("nickname") ? Data["nickname"]?.ToString() ?? "" : "";
        public object Content => Data.ContainsKey("content") ? Data["content"] : null;
    }

    /// <summary>
    ///     XML ��Ϣ��
    /// </summary>
    public class XmlSegment : MessageSegment
    {
        public XmlSegment() : base("xml")
        {
        }

        public XmlSegment(string data) : base("xml")
        {
            Data["data"] = data;
        }

        public string XmlData => Data.ContainsKey("data") ? Data["data"]?.ToString() ?? "" : "";
    }

    /// <summary>
    ///     JSON ��Ϣ��
    /// </summary>
    public class JsonSegment : MessageSegment
    {
        public JsonSegment() : base("json")
        {
        }

        public JsonSegment(string data) : base("json")
        {
            Data["data"] = data;
        }

        public string JsonData => Data.ContainsKey("data") ? Data["data"]?.ToString() ?? "" : "";
    }
}