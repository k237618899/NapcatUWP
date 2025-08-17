using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AnnaMessager.Core.Models
{
    // OneBot API 基础结构
    public class OneBotRequest
    {
        [JsonProperty("action")] public string Action { get; set; }

        [JsonProperty("params")] public object Params { get; set; }

        [JsonProperty("echo")] public string Echo { get; set; }
    }

    public class OneBotResponse<T>
    {
        [JsonProperty("status")] public string Status { get; set; }

        [JsonProperty("retcode")] public int RetCode { get; set; }

        [JsonProperty("data")] public T Data { get; set; }

        [JsonProperty("echo")] public string Echo { get; set; }
    }

    // 事件模型
    public class OneBotEvent
    {
        [JsonProperty("time")] public long Time { get; set; }

        [JsonProperty("self_id")] public long SelfId { get; set; }

        [JsonProperty("post_type")] public string PostType { get; set; }

        // 修正：使用 PCL 兼容的方式轉換 Unix 時間戳
        public DateTime DateTime
        {
            get
            {
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return epoch.AddSeconds(Time);
            }
        }
    }

    // 消息事件
    public class MessageEvent : OneBotEvent
    {
        [JsonProperty("message_type")] public string MessageType { get; set; }

        [JsonProperty("sub_type")] public string SubType { get; set; }

        [JsonProperty("message_id")] public long MessageId { get; set; }

        [JsonProperty("user_id")] public long UserId { get; set; }

        // NapCat/OneBot 在 message_format = "array" 時此欄位為陣列，否則為字串
        [JsonProperty("message")] public JToken MessageToken { get; set; }

        [JsonProperty("raw_message")] public string RawMessage { get; set; }

        [JsonProperty("font")] public int Font { get; set; }

        [JsonProperty("sender")] public SenderInfo Sender { get; set; }

        // 群消息特有字段
        [JsonProperty("group_id")] public long? GroupId { get; set; }

        [JsonProperty("anonymous")] public AnonymousInfo Anonymous { get; set; }

        // 兼容舊程式碼使用的 Message 文字內容
        [JsonIgnore]
        public string Message
        {
            get
            {
                if (MessageToken == null) return string.Empty;
                if (MessageToken.Type == JTokenType.String || MessageToken.Type == JTokenType.Integer ||
                    MessageToken.Type == JTokenType.Float)
                    return MessageToken.ToString();
                if (MessageToken.Type == JTokenType.Array)
                {
                    var parts = new List<string>();
                    foreach (var seg in MessageToken as JArray)
                    {
                        var type = seg["type"]?.ToString();
                        var data = seg["data"] as JObject;
                        if (type == "text")
                        {
                            var text = data?["text"]?.ToString();
                            if (!string.IsNullOrEmpty(text)) parts.Add(text);
                        }
                        else if (type == "image")
                        {
                            // 圖片顯示簡短標記，可根據需要更換
                            var summary = data?["summary"]?.ToString();
                            parts.Add(!string.IsNullOrEmpty(summary) ? summary : "[圖片]");
                        }
                        else if (type == "face")
                        {
                            parts.Add("[表情]");
                        }
                        else if (type == "at")
                        {
                            var qq = data?["qq"]?.ToString();
                            parts.Add(!string.IsNullOrEmpty(qq) ? $"@{qq}" : "@?");
                        }
                        else
                        {
                            parts.Add($"[{type}]");
                        }
                    }
                    return string.Join(string.Empty, parts);
                }
                return MessageToken.ToString();
            }
        }

        [JsonIgnore]
        public List<MessageSegment> Segments
        {
            get
            {
                var list = new List<MessageSegment>();
                if (MessageToken == null || MessageToken.Type != JTokenType.Array) return list;
                foreach (var seg in MessageToken as JArray)
                {
                    try
                    {
                        var type = seg["type"]?.ToString() ?? "unknown";
                        var dataObj = seg["data"] as JObject;
                        var dataDict = new Dictionary<string, object>();
                        if (dataObj != null)
                        {
                            foreach (var p in dataObj.Properties()) dataDict[p.Name] = p.Value.Type == JTokenType.String ? (object)p.Value.ToString() : p.Value.ToString();
                        }
                        var ms = new MessageSegment(type) { Data = dataDict };
                        list.Add(ms);
                    }
                    catch
                    {
                        // 忽略單個段解析錯誤
                    }
                }
                return list;
            }
        }
    }

    // 通知事件
    public class NoticeEvent : OneBotEvent
    {
        [JsonProperty("notice_type")] public string NoticeType { get; set; }
    }

    // 请求事件
    public class RequestEvent : OneBotEvent
    {
        [JsonProperty("request_type")] public string RequestType { get; set; }
    }

    // 元事件
    public class MetaEvent : OneBotEvent
    {
        [JsonProperty("meta_event_type")] public string MetaEventType { get; set; }

        [JsonProperty("status")] public object Status { get; set; }

        [JsonProperty("interval")] public long? Interval { get; set; }
    }

    // 消息发送者信息
    public class SenderInfo
    {
        [JsonProperty("user_id")] public long UserId { get; set; }

        [JsonProperty("nickname")] public string Nickname { get; set; }

        [JsonProperty("sex")] public string Sex { get; set; }

        [JsonProperty("age")] public int Age { get; set; }

        // 群消息发送者额外字段
        [JsonProperty("card")] public string Card { get; set; }

        [JsonProperty("area")] public string Area { get; set; }

        [JsonProperty("level")] public string Level { get; set; }

        [JsonProperty("role")] public string Role { get; set; }

        [JsonProperty("title")] public string Title { get; set; }
    }

    // 匿名信息
    public class AnonymousInfo
    {
        [JsonProperty("id")] public long Id { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("flag")] public string Flag { get; set; }
    }

    // 好友信息
    public class FriendInfo
    {
        [JsonProperty("user_id")] public long UserId { get; set; }

        [JsonProperty("nickname")] public string Nickname { get; set; }

        [JsonProperty("remark")] public string Remark { get; set; }
    }

    // 群组信息
    public class GroupInfo
    {
        [JsonProperty("group_id")] public long GroupId { get; set; }

        [JsonProperty("group_name")] public string GroupName { get; set; }

        [JsonProperty("member_count")] public int MemberCount { get; set; }

        [JsonProperty("max_member_count")] public int MaxMemberCount { get; set; }
    }

    // 群成员信息
    public class GroupMemberInfo
    {
        [JsonProperty("group_id")] public long GroupId { get; set; }

        [JsonProperty("user_id")] public long UserId { get; set; }

        [JsonProperty("nickname")] public string Nickname { get; set; }

        [JsonProperty("card")] public string Card { get; set; }

        [JsonProperty("sex")] public string Sex { get; set; }

        [JsonProperty("age")] public int Age { get; set; }

        [JsonProperty("area")] public string Area { get; set; }

        [JsonProperty("join_time")] public long JoinTime { get; set; }

        [JsonProperty("last_sent_time")] public long LastSentTime { get; set; }

        [JsonProperty("level")] public string Level { get; set; }

        [JsonProperty("role")] public string Role { get; set; }

        [JsonProperty("unfriendly")] public bool Unfriendly { get; set; }

        [JsonProperty("title")] public string Title { get; set; }

        [JsonProperty("title_expire_time")] public long TitleExpireTime { get; set; }

        [JsonProperty("card_changeable")] public bool CardChangeable { get; set; }
    }

    // 历史消息
    public class HistoryMessage
    {
        [JsonProperty("message_id")] public long MessageId { get; set; }

        [JsonProperty("real_id")] public long RealId { get; set; }

        [JsonProperty("sender")] public SenderInfo Sender { get; set; }

        [JsonProperty("time")] public long Time { get; set; }

        [JsonProperty("message")] public string Message { get; set; }

        [JsonProperty("message_seq")] public long MessageSeq { get; set; } // 新增: 用於翻頁
    }

    public class MessageHistoryData
    {
        [JsonProperty("messages")] public List<HistoryMessage> Messages { get; set; }
    }

    // NapCat 扩展模型
    public class MessageHistoryItem
    {
        [JsonProperty("message_id")] public long MessageId { get; set; }
        [JsonProperty("real_id")] public long RealId { get; set; }
        [JsonProperty("sender")] public MessageSender Sender { get; set; }
        [JsonProperty("time")] public long Time { get; set; }

        // NapCat 可能返回陣列 (segments) 或字串，改為使用 JToken
        [JsonProperty("message")] public JToken MessageToken { get; set; }
        [JsonProperty("raw_message")] public string RawMessage { get; set; }
        [JsonProperty("message_seq")] public long MessageSeq { get; set; } // 新增: 序列號

        // 轉換統一文字顯示 (複用 MessageEvent 的邏輯精簡版)
        [JsonIgnore]
        public string Message
        {
            get
            {
                try
                {
                    if (MessageToken == null) return RawMessage ?? string.Empty;
                    if (MessageToken.Type == JTokenType.String || MessageToken.Type == JTokenType.Integer || MessageToken.Type == JTokenType.Float)
                        return MessageToken.ToString();
                    if (MessageToken.Type == JTokenType.Array)
                    {
                        var parts = new List<string>();
                        foreach (var seg in MessageToken as JArray)
                        {
                            var type = seg["type"]?.ToString();
                            var data = seg["data"] as JObject;
                            switch (type)
                            {
                                case "text":
                                    var text = data?["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(text)) parts.Add(text);
                                    break;
                                case "image":
                                    parts.Add("[圖片]");
                                    break;
                                case "face":
                                    parts.Add("[表情]");
                                    break;
                                case "at":
                                    var qq = data?["qq"]?.ToString();
                                    parts.Add(!string.IsNullOrEmpty(qq) ? $"@{qq}" : "@?");
                                    break;
                                default:
                                    parts.Add($"[{type}]");
                                    break;
                            }
                        }
                        return string.Join(string.Empty, parts);
                    }
                }
                catch { }
                return RawMessage ?? MessageToken?.ToString() ?? string.Empty;
            }
        }

        public DateTime DateTime
        {
            get
            {
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return epoch.AddSeconds(Time);
            }
        }
    }

    public class MessageSender
    {
        [JsonProperty("user_id")] public long UserId { get; set; }

        [JsonProperty("nickname")] public string Nickname { get; set; }

        [JsonProperty("card")] public string Card { get; set; }

        [JsonProperty("role")] public string Role { get; set; }
    }

    // 历史消息响应
    public class MessageHistoryResponse
    {
        [JsonProperty("messages")] public List<MessageHistoryItem> Messages { get; set; }
    }

    // 文件信息 - 注意：這裡使用 OneBotFileInfo 避免與 Core.Models.FileInfo 衝突
    public class OneBotFileInfo
    {
        [JsonProperty("file_id")] public string FileId { get; set; }

        [JsonProperty("file_name")] public string FileName { get; set; }

        [JsonProperty("file_size")] public long FileSize { get; set; }

        [JsonProperty("file_url")] public string FileUrl { get; set; }

        [JsonProperty("base64")] public string Base64 { get; set; }
    }

    // 精华消息
    public class EssenceMessage
    {
        [JsonProperty("sender_id")] public long SenderId { get; set; }

        [JsonProperty("sender_nick")] public string SenderNick { get; set; }

        [JsonProperty("sender_time")] public long SenderTime { get; set; }

        [JsonProperty("operator_id")] public long OperatorId { get; set; }

        [JsonProperty("operator_nick")] public string OperatorNick { get; set; }

        [JsonProperty("operator_time")] public long OperatorTime { get; set; }

        [JsonProperty("message_id")] public long MessageId { get; set; }
    }

    // 群文件信息
    public class GroupFileInfo
    {
        [JsonProperty("group_id")] public long GroupId { get; set; }

        [JsonProperty("file_id")] public string FileId { get; set; }

        [JsonProperty("file_name")] public string FileName { get; set; }

        [JsonProperty("busid")] public int BusId { get; set; }

        [JsonProperty("file_size")] public long FileSize { get; set; }

        [JsonProperty("upload_time")] public long UploadTime { get; set; }

        [JsonProperty("dead_time")] public long DeadTime { get; set; }

        [JsonProperty("modify_time")] public long ModifyTime { get; set; }

        [JsonProperty("download_times")] public int DownloadTimes { get; set; }

        [JsonProperty("uploader")] public long Uploader { get; set; }

        [JsonProperty("uploader_name")] public string UploaderName { get; set; }
    }

    // 群文件夹信息
    public class GroupFolderInfo
    {
        [JsonProperty("group_id")] public long GroupId { get; set; }

        [JsonProperty("folder_id")] public string FolderId { get; set; }

        [JsonProperty("folder_name")] public string FolderName { get; set; }

        [JsonProperty("create_time")] public long CreateTime { get; set; }

        [JsonProperty("creator")] public long Creator { get; set; }

        [JsonProperty("creator_name")] public string CreatorName { get; set; }

        [JsonProperty("total_file_count")] public int TotalFileCount { get; set; }
    }

    // 登录信息 - 注意：重命名避免與 LoginInfo 衝突
    public class LoginInfoData
    {
        [JsonProperty("user_id")] public long UserId { get; set; }

        [JsonProperty("nickname")] public string Nickname { get; set; }
    }

    // 保留原有的 LoginInfo 類以確保兼容性
    public class LoginInfo
    {
        [JsonProperty("user_id")] public long UserId { get; set; }

        [JsonProperty("nickname")] public string Nickname { get; set; }
    }

    // 陌生人信息
    public class StrangerInfo
    {
        [JsonProperty("user_id")] public long UserId { get; set; }

        [JsonProperty("nickname")] public string Nickname { get; set; }

        [JsonProperty("sex")] public string Sex { get; set; }

        [JsonProperty("age")] public int Age { get; set; }

        [JsonProperty("qid")] public string Qid { get; set; }

        [JsonProperty("level")] public int Level { get; set; }

        [JsonProperty("login_days")] public int LoginDays { get; set; }
    }

    // 事件参数类
    public class MessageEventArgs : EventArgs
    {
        public MessageEvent Message { get; set; }
    }

    public class NoticeEventArgs : EventArgs
    {
        public NoticeEvent Notice { get; set; }
    }

    public class RequestEventArgs : EventArgs
    {
        public RequestEvent Request { get; set; }
    }

    public class MetaEventArgs : EventArgs
    {
        public MetaEvent Meta { get; set; }
    }

    // 消息段基类
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
                OnPropertyChanged("Type"); // PCL 不支持 nameof，使用字符串
            }
        }

        [JsonProperty("data")]
        public Dictionary<string, object> Data
        {
            get => _data ?? (_data = new Dictionary<string, object>());
            set
            {
                _data = value;
                OnPropertyChanged("Data"); // PCL 不支持 nameof，使用字符串
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 文本消息段
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

    // @提及消息段
    public class AtSegment : MessageSegment
    {
        public AtSegment() : base("at")
        {
        }

        public AtSegment(string qq) : base("at")
        {
            Data["qq"] = qq;
        }

        public string QQ => Data.ContainsKey("qq") ? Data["qq"]?.ToString() ?? "" : "";
        public bool IsAtAll => QQ == "all";
    }

    // 朋友分組信息 (get_friends_with_category)
    public class FriendCategoryItem
    {
        // 兩種不同字段命名 (舊: category_id, 新: categoryId)
        [JsonProperty("category_id")] public long? CategoryIdLegacy { get; set; }
        [JsonProperty("categoryId")] public long? CategoryIdNew { get; set; }

        [JsonProperty("category_name")] public string CategoryNameLegacy { get; set; }
        [JsonProperty("categoryName")] public string CategoryNameNew { get; set; }

        // 分組排序 ID (舊/新)
        [JsonProperty("category_sort_id")] public int? CategorySortIdLegacy { get; set; }
        [JsonProperty("categorySortId")] public int? CategorySortIdNew { get; set; }

        // 朋友列表兩種命名 (friends / buddyList)
        [JsonProperty("friends")] public List<FriendInfo> FriendsLegacy { get; set; }
        [JsonProperty("buddyList")] public List<FriendInfo> BuddyList { get; set; }

        // 統一訪問屬性
        [JsonIgnore] public long CategoryId => CategoryIdLegacy ?? CategoryIdNew ?? 0;
        [JsonIgnore] public string CategoryName => !string.IsNullOrEmpty(CategoryNameLegacy) ? CategoryNameLegacy : CategoryNameNew;
        [JsonIgnore] public List<FriendInfo> Friends => FriendsLegacy ?? BuddyList ?? new List<FriendInfo>();
        [JsonIgnore] public int SortOrderRaw => CategorySortIdLegacy ?? CategorySortIdNew ?? int.MaxValue;
        [JsonIgnore] public int FinalSortOrder => CategoryName == "特别关心" ? int.MinValue : SortOrderRaw;
    }

    // 移除舊 RecentContact 定義，改用 ChatModels.cs 中擴展版本
}