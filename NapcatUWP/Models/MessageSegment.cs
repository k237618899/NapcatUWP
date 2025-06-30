using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using NapcatUWP.Controls;
using Newtonsoft.Json;

namespace NapcatUWP.Models
{
    /// <summary>
    ///     OneBot 11 消息段基類
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
    ///     文本消息段
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
    ///     @提及消息段
    /// </summary>
    public class AtSegment : MessageSegment
    {
        private string _cachedDisplayText; // 緩存的顯示文本
        private long _groupId; // 添加群組ID屬性

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
        ///     設置群組ID（用於查詢成員信息）
        /// </summary>
        public long GroupId
        {
            get => _groupId;
            set
            {
                _groupId = value;
                _cachedDisplayText = null; // 清空緩存，強制重新計算
                OnPropertyChanged(nameof(GroupId));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        ///     獲取顯示文本（優先顯示群組成員名稱）
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedDisplayText))
                    return _cachedDisplayText;

                if (IsAtAll)
                {
                    _cachedDisplayText = "@全體成員";
                    return _cachedDisplayText;
                }

                if (long.TryParse(QQ, out var userId) && _groupId > 0)
                    // 嘗試從數據庫獲取群組成員信息
                    try
                    {
                        var displayName = DataAccess.GetGroupMemberDisplayName(_groupId, userId);
                        _cachedDisplayText = $"@{displayName}";
                        return _cachedDisplayText;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"獲取群組成員顯示名稱時發生錯誤: {ex.Message}");
                    }

                _cachedDisplayText = $"@{QQ}";
                return _cachedDisplayText;
            }
        }

        /// <summary>
        ///     刷新顯示文本（當群組成員信息更新後調用）
        /// </summary>
        public void RefreshDisplayText()
        {
            _cachedDisplayText = null; // 清空緩存
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>
    ///     表情消息段
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
    ///     圖片消息段
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
    ///     語音消息段
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
    ///     視頻消息段
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
    ///     文件消息段
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
    ///     回復消息段
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
        ///     靜態委託，用於請求獲取消息內容
        /// </summary>
        public static Action<long> RequestMessageContentDelegate { get; set; }

        /// <summary>
        ///     獲取回復消息的內容（靜態委託方式）
        /// </summary>
        public string GetReplyContent()
        {
            if (string.IsNullOrEmpty(Id) || !long.TryParse(Id, out var messageId))
                return $"回覆 #{Id}";

            // 先從數據庫查找
            var message = DataAccess.GetMessageById(messageId);
            if (message != null)
            {
                // 如果消息內容過長，截取前50個字符
                var content = message.Content ?? "";
                if (content.Length > 50)
                    content = content.Substring(0, 50) + "...";
                return $"回覆 {message.SenderName}: {content}";
            }

            // 如果數據庫中沒有，通知請求獲取消息內容
            RequestMessageContentDelegate?.Invoke(messageId);
            return $"回覆 #{messageId}"; // 暫時顯示ID，等待API響應
        }
    }

    /// <summary>
    ///     戳一戳消息段
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
    ///     禮物消息段
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
    ///     轉發消息段
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
    ///     節點消息段
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
    ///     XML 消息段
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
    ///     JSON 消息段
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