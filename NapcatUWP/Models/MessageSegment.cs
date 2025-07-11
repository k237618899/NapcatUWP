using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using NapcatUWP.Controls;
using Newtonsoft.Json;

namespace NapcatUWP.Models
{
    /// <summary>
    ///     OneBot 11 消息段基类
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
        private string _cachedDisplayText; // 缓存的显示文本
        private long _groupId; // 添加群组ID属性

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
        ///     设置群组ID（用于查询成员信息）
        /// </summary>
        public long GroupId
        {
            get => _groupId;
            set
            {
                _groupId = value;
                _cachedDisplayText = null; // 清空缓存，强制重新计算
                OnPropertyChanged(nameof(GroupId));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        ///     获取显示文本（优先显示群组成员名称）
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedDisplayText))
                    return _cachedDisplayText;

                if (IsAtAll)
                {
                    _cachedDisplayText = "@全体成员";
                    return _cachedDisplayText;
                }

                if (long.TryParse(QQ, out var userId) && _groupId > 0)
                    // 尝试从数据库获取群组成员信息
                    try
                    {
                        var displayName = DataAccess.GetGroupMemberDisplayName(_groupId, userId);
                        _cachedDisplayText = $"@{displayName}";
                        return _cachedDisplayText;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"获取群组成员显示名称时发生错误: {ex.Message}");
                    }

                _cachedDisplayText = $"@{QQ}";
                return _cachedDisplayText;
            }
        }

        /// <summary>
        ///     刷新显示文本（当群组成员信息更新时调用）
        /// </summary>
        public void RefreshDisplayText()
        {
            _cachedDisplayText = null; // 清空缓存
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
    ///     视频消息段
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
    ///     回复消息段
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
        ///     静态委托，用于请求获取消息内容
        /// </summary>
        public static Action<long> RequestMessageContentDelegate { get; set; }

        /// <summary>
        ///     获取回复消息的内容，优化版本 - 支持富媒体内容显示
        /// </summary>
        public string GetReplyContent()
        {
            if (string.IsNullOrEmpty(Id) || !long.TryParse(Id, out var messageId))
                return $"回复 #{Id}";

            // 先检查数据库
            var message = DataAccess.GetMessageById(messageId);
            if (message != null)
            {
                // 优先使用消息段来生成富文本内容
                if (message.Segments != null && message.Segments.Count > 0)
                {
                    try
                    {
                        // 使用 MessageSegmentParser 生成富文本
                        var richContent = GenerateDisplayTextFromSegments(message.Segments);
                        if (!string.IsNullOrEmpty(richContent))
                        {
                            // 限制长度
                            if (richContent.Length > 50)
                                richContent = richContent.Substring(0, 50) + "...";
                            return $"回复 {message.SenderName}: {richContent}";
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"生成回复消息富文本时发生错误: {ex.Message}");
                    }
                }

                // 回退到使用 Content 字段，但尝试替换常见的 CQ 码为友好文本
                var content = ProcessContentForDisplay(message.Content ?? "");
                if (content.Length > 50)
                    content = content.Substring(0, 50) + "...";
                return $"回复 {message.SenderName}: {content}";
            }

            // 如果数据库中没有，通知请求获取消息内容
            RequestMessageContentDelegate?.Invoke(messageId);
            return $"回复 #{messageId}"; // 临时显示ID，等待API响应
        }

        /// <summary>
        /// 从消息段生成显示文本 - 专为回复消息优化
        /// </summary>
        private string GenerateDisplayTextFromSegments(List<MessageSegment> segments)
        {
            if (segments == null || segments.Count == 0) return "";

            var result = new System.Text.StringBuilder();

            foreach (var segment in segments)
            {
                switch (segment.Type)
                {
                    case "text":
                        var textSegment = segment as TextSegment;
                        result.Append(textSegment?.Text ?? "");
                        break;
                    case "at":
                        var atSegment = segment as AtSegment;
                        if (atSegment?.IsAtAll == true)
                            result.Append("📢@所有人 ");
                        else
                            result.Append($"👤@{atSegment?.DisplayText ?? atSegment?.QQ} ");
                        break;
                    case "face":
                        result.Append("😀[表情] ");
                        break;
                    case "image":
                        result.Append("🖼️[圖片] ");
                        break;
                    case "record":
                        var recordSegment = segment as RecordSegment;
                        if (recordSegment?.Magic == true)
                            result.Append("🎙️[變聲語音] ");
                        else
                            result.Append("🎵[語音] ");
                        break;
                    case "video":
                        result.Append("🎬[視頻] ");
                        break;
                    case "file":
                        result.Append("📎[文件] ");
                        break;
                    case "reply":
                        result.Append("💬[回覆] ");
                        break;
                    case "poke":
                        result.Append("👋[戳一戳] ");
                        break;
                    case "gift":
                        result.Append("🎁[禮物] ");
                        break;
                    case "forward":
                        result.Append("↗️[轉發] ");
                        break;
                    case "node":
                        result.Append("🔗[節點] ");
                        break;
                    case "xml":
                        result.Append("📋[XML卡片] ");
                        break;
                    case "json":
                        result.Append("📋[JSON卡片] ");
                        break;
                    default:
                        result.Append($"[{segment.Type}] ");
                        break;
                }
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// 處理內容以便顯示 - 將常見的CQ碼轉換為友好文字 (UWP 15063相容版本)
        /// </summary>
        private string ProcessContentForDisplay(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";

            // 使用簡單的字串替換，避免使用正規表示式以保持相容性
            // 替換圖片CQ碼
            while (content.IndexOf("[CQ:image") >= 0)
            {
                var start = content.IndexOf("[CQ:image");
                var end = content.IndexOf("]", start);
                if (end > start)
                {
                    content = content.Substring(0, start) + "🖼️[圖片]" + content.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // 替換語音CQ碼
            while (content.IndexOf("[CQ:record") >= 0)
            {
                var start = content.IndexOf("[CQ:record");
                var end = content.IndexOf("]", start);
                if (end > start)
                {
                    content = content.Substring(0, start) + "🎵[語音]" + content.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // 替換視頻CQ碼
            while (content.IndexOf("[CQ:video") >= 0)
            {
                var start = content.IndexOf("[CQ:video");
                var end = content.IndexOf("]", start);
                if (end > start)
                {
                    content = content.Substring(0, start) + "🎬[視頻]" + content.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // 替換文件CQ碼
            while (content.IndexOf("[CQ:file") >= 0)
            {
                var start = content.IndexOf("[CQ:file");
                var end = content.IndexOf("]", start);
                if (end > start)
                {
                    content = content.Substring(0, start) + "📎[文件]" + content.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // 替換@所有人
            content = content.Replace("[CQ:at,qq=all]", "📢@所有人");

            // 替換@某人 - 簡化版本
            while (content.IndexOf("[CQ:at,qq=") >= 0)
            {
                var start = content.IndexOf("[CQ:at,qq=");
                var end = content.IndexOf("]", start);
                if (end > start)
                {
                    var qqStart = start + "[CQ:at,qq=".Length;
                    var qq = content.Substring(qqStart, end - qqStart);
                    content = content.Substring(0, start) + $"👤@{qq}" + content.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // 替換表情CQ碼
            while (content.IndexOf("[CQ:face") >= 0)
            {
                var start = content.IndexOf("[CQ:face");
                var end = content.IndexOf("]", start);
                if (end > start)
                {
                    content = content.Substring(0, start) + "😀[表情]" + content.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // 處理其他未知的CQ碼
            while (content.IndexOf("[CQ:") >= 0)
            {
                var start = content.IndexOf("[CQ:");
                var end = content.IndexOf("]", start);
                if (end > start)
                {
                    content = content.Substring(0, start) + "[多媒體內容]" + content.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            return content;
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
    ///     礼物消息段
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
    ///     转发消息段
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
    ///     节点消息段
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