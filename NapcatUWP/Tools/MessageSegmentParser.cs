using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NapcatUWP.Controls;
using NapcatUWP.Models;
using Newtonsoft.Json.Linq;

namespace NapcatUWP.Tools
{
    /// <summary>
    ///     OneBot 11 消息段解析器
    /// </summary>
    public static class MessageSegmentParser
    {
        /// <summary>
        ///     解析文本消息段
        /// </summary>
        private static MessageSegment ParseTextSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var text = data.Value<string>("text") ?? "";
                    return new TextSegment(text);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析文本消息段時發生錯誤: {ex.Message}");
            }

            return new TextSegment();
        }

        /// <summary>
        ///     解析 @ 消息段，並在需要時請求群組成員信息
        /// </summary>
        private static MessageSegment ParseAtSegment(JToken segmentToken, long groupId = 0)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var qq = data.Value<string>("qq") ?? "";
                    var atSegment = new AtSegment(qq);

                    if (groupId > 0)
                    {
                        atSegment.GroupId = groupId;

                        // 如果不是 @全體成員，檢查是否需要請求成員信息
                        if (!atSegment.IsAtAll && long.TryParse(qq, out var userId))
                            // 檢查數據庫中是否已有該成員信息
                            Task.Run(() =>
                            {
                                var existingMember = DataAccess.GetGroupMember(groupId, userId);
                                if (existingMember == null)
                                {
                                    // 如果沒有成員信息，請求獲取
                                    MainPage.SocketClientStarter?.RequestGroupMemberInfo(groupId, userId);
                                    Debug.WriteLine($"請求群組成員信息: GroupId={groupId}, UserId={userId}");
                                }
                                else
                                {
                                    Debug.WriteLine(
                                        $"群組成員信息已存在: GroupId={groupId}, UserId={userId}, DisplayName={existingMember.GetDisplayName()}");
                                }
                            });
                    }

                    return atSegment;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析 @ 消息段時發生錯誤: {ex.Message}");
            }

            return new AtSegment();
        }

        /// <summary>
        ///     解析表情消息段
        /// </summary>
        private static MessageSegment ParseFaceSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var id = data.Value<string>("id") ?? "";
                    return new FaceSegment(id);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析表情消息段時發生錯誤: {ex.Message}");
            }

            return new FaceSegment();
        }

        /// <summary>
        ///     解析圖片消息段
        /// </summary>
        private static MessageSegment ParseImageSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var file = data.Value<string>("file") ?? "";
                    var imageSegment = new ImageSegment(file);

                    // 設置其他屬性
                    if (data["url"] != null)
                        imageSegment.Data["url"] = data.Value<string>("url");
                    if (data["cache"] != null)
                        imageSegment.Data["cache"] = data.Value<bool>("cache");
                    if (data["proxy"] != null)
                        imageSegment.Data["proxy"] = data.Value<bool>("proxy");
                    if (data["timeout"] != null)
                        imageSegment.Data["timeout"] = data.Value<int>("timeout");

                    return imageSegment;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析圖片消息段時發生錯誤: {ex.Message}");
            }

            return new ImageSegment();
        }

        /// <summary>
        ///     解析語音消息段
        /// </summary>
        private static MessageSegment ParseRecordSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var file = data.Value<string>("file") ?? "";
                    var recordSegment = new RecordSegment(file);

                    // 設置其他屬性
                    if (data["url"] != null)
                        recordSegment.Data["url"] = data.Value<string>("url");
                    if (data["magic"] != null)
                        recordSegment.Data["magic"] = data.Value<bool>("magic");
                    if (data["cache"] != null)
                        recordSegment.Data["cache"] = data.Value<bool>("cache");
                    if (data["proxy"] != null)
                        recordSegment.Data["proxy"] = data.Value<bool>("proxy");
                    if (data["timeout"] != null)
                        recordSegment.Data["timeout"] = data.Value<int>("timeout");

                    return recordSegment;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析語音消息段時發生錯誤: {ex.Message}");
            }

            return new RecordSegment();
        }

        /// <summary>
        ///     解析視頻消息段
        /// </summary>
        private static MessageSegment ParseVideoSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var file = data.Value<string>("file") ?? "";
                    var videoSegment = new VideoSegment(file);

                    // 設置其他屬性
                    if (data["url"] != null)
                        videoSegment.Data["url"] = data.Value<string>("url");
                    if (data["cache"] != null)
                        videoSegment.Data["cache"] = data.Value<bool>("cache");
                    if (data["proxy"] != null)
                        videoSegment.Data["proxy"] = data.Value<bool>("proxy");
                    if (data["timeout"] != null)
                        videoSegment.Data["timeout"] = data.Value<int>("timeout");

                    return videoSegment;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析視頻消息段時發生錯誤: {ex.Message}");
            }

            return new VideoSegment();
        }

        /// <summary>
        ///     解析文件消息段
        /// </summary>
        private static MessageSegment ParseFileSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var file = data.Value<string>("file") ?? "";
                    var fileSegment = new FileSegment(file);

                    // 設置其他屬性
                    if (data["url"] != null)
                        fileSegment.Data["url"] = data.Value<string>("url");

                    return fileSegment;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析文件消息段時發生錯誤: {ex.Message}");
            }

            return new FileSegment();
        }

        /// <summary>
        ///     解析回覆消息段
        /// </summary>
        private static MessageSegment ParseReplySegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var id = data.Value<string>("id") ?? "";
                    return new ReplySegment(id);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析回覆消息段時發生錯誤: {ex.Message}");
            }

            return new ReplySegment();
        }

        /// <summary>
        ///     解析戳一戳消息段
        /// </summary>
        private static MessageSegment ParsePokeSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var type = data.Value<string>("type") ?? "";
                    var id = data.Value<string>("id") ?? "";
                    return new PokeSegment(type, id);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析戳一戳消息段時發生錯誤: {ex.Message}");
            }

            return new PokeSegment();
        }

        /// <summary>
        ///     解析禮物消息段
        /// </summary>
        private static MessageSegment ParseGiftSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var qq = data.Value<string>("qq") ?? "";
                    var id = data.Value<string>("id") ?? "";
                    return new GiftSegment(qq, id);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析禮物消息段時發生錯誤: {ex.Message}");
            }

            return new GiftSegment();
        }

        /// <summary>
        ///     解析轉發消息段
        /// </summary>
        private static MessageSegment ParseForwardSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var id = data.Value<string>("id") ?? "";
                    return new ForwardSegment(id);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析轉發消息段時發生錯誤: {ex.Message}");
            }

            return new ForwardSegment();
        }

        /// <summary>
        ///     解析節點消息段
        /// </summary>
        private static MessageSegment ParseNodeSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var id = data.Value<string>("id") ?? "";
                    var nodeSegment = new NodeSegment(id);

                    // 設置其他屬性
                    if (data["user_id"] != null)
                        nodeSegment.Data["user_id"] = data.Value<string>("user_id");
                    if (data["nickname"] != null)
                        nodeSegment.Data["nickname"] = data.Value<string>("nickname");
                    if (data["content"] != null)
                        nodeSegment.Data["content"] = data["content"];

                    return nodeSegment;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析節點消息段時發生錯誤: {ex.Message}");
            }

            return new NodeSegment();
        }

        /// <summary>
        ///     解析XML消息段
        /// </summary>
        private static MessageSegment ParseXmlSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var xmlData = data.Value<string>("data") ?? "";
                    return new XmlSegment(xmlData);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析XML消息段時發生錯誤: {ex.Message}");
            }

            return new XmlSegment();
        }

        /// <summary>
        ///     解析JSON消息段
        /// </summary>
        private static MessageSegment ParseJsonSegment(JToken segmentToken)
        {
            try
            {
                var data = segmentToken["data"];
                if (data != null)
                {
                    var jsonData = data.Value<string>("data") ?? "";
                    return new JsonSegment(jsonData);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析JSON消息段時發生錯誤: {ex.Message}");
            }

            return new JsonSegment();
        }

        /// <summary>
        ///     修改解析消息數組的方法以傳遞群組ID
        /// </summary>
        public static List<MessageSegment> ParseMessageArray(JToken messageArray, long groupId = 0)
        {
            var segments = new List<MessageSegment>();

            try
            {
                if (messageArray != null && messageArray.Type == JTokenType.Array)
                    foreach (var segmentToken in messageArray)
                        try
                        {
                            var segment = ParseMessageSegment(segmentToken, groupId);
                            if (segment != null) segments.Add(segment);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"解析消息段時發生錯誤: {ex.Message}");
                            // 繼續處理下一個段落
                        }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析消息數組時發生錯誤: {ex.Message}");
            }

            return segments;
        }

        /// <summary>
        ///     修改解析單個消息段的方法
        /// </summary>
        private static MessageSegment ParseMessageSegment(JToken segmentToken, long groupId = 0)
        {
            var type = segmentToken.Value<string>("type") ?? "";

            switch (type.ToLower())
            {
                case "text":
                    return ParseTextSegment(segmentToken);
                case "at":
                    return ParseAtSegment(segmentToken, groupId);
                case "face":
                    return ParseFaceSegment(segmentToken);
                case "image":
                    return ParseImageSegment(segmentToken);
                case "record":
                    return ParseRecordSegment(segmentToken);
                case "video":
                    return ParseVideoSegment(segmentToken);
                case "file":
                    return ParseFileSegment(segmentToken);
                case "reply":
                    return ParseReplySegment(segmentToken);
                case "poke":
                    return ParsePokeSegment(segmentToken);
                case "gift":
                    return ParseGiftSegment(segmentToken);
                case "forward":
                    return ParseForwardSegment(segmentToken);
                case "node":
                    return ParseNodeSegment(segmentToken);
                case "xml":
                    return ParseXmlSegment(segmentToken);
                case "json":
                    return ParseJsonSegment(segmentToken);
                default:
                    Debug.WriteLine($"未知的消息段類型: {type}");
                    return new MessageSegment(type);
            }
        }

        /// <summary>
        ///     從消息段生成純文本內容
        /// </summary>
        public static string GenerateTextFromSegments(List<MessageSegment> segments)
        {
            if (segments == null || segments.Count == 0) return "";

            var textBuilder = new StringBuilder();

            foreach (var segment in segments)
                switch (segment.Type)
                {
                    case "text":
                        var textSegment = segment as TextSegment;
                        textBuilder.Append(textSegment?.Text ?? "");
                        break;
                    case "at":
                        var atSegment = segment as AtSegment;
                        if (atSegment?.IsAtAll == true)
                            textBuilder.Append("@所有人 ");
                        else
                            textBuilder.Append($"@{atSegment?.QQ} ");
                        break;
                    case "face":
                        textBuilder.Append("[表情] ");
                        break;
                    case "image":
                        textBuilder.Append("[圖片] ");
                        break;
                    case "record":
                        textBuilder.Append("[語音] ");
                        break;
                    case "video":
                        textBuilder.Append("[視頻] ");
                        break;
                    case "file":
                        textBuilder.Append("[文件] ");
                        break;
                    case "reply":
                        textBuilder.Append("[回覆] ");
                        break;
                    case "poke":
                        textBuilder.Append("[戳一戳] ");
                        break;
                    case "gift":
                        textBuilder.Append("[禮物] ");
                        break;
                    case "forward":
                        textBuilder.Append("[轉發] ");
                        break;
                    case "node":
                        textBuilder.Append("[節點] ");
                        break;
                    case "xml":
                        textBuilder.Append("[XML卡片] ");
                        break;
                    case "json":
                        textBuilder.Append("[JSON卡片] ");
                        break;
                    default:
                        textBuilder.Append($"[{segment.Type}] ");
                        break;
                }

            return textBuilder.ToString().Trim();
        }

        /// <summary>
        ///     從消息段生成富文本內容（可用於UI展示）
        /// </summary>
        public static string GenerateRichTextFromSegments(List<MessageSegment> segments)
        {
            if (segments == null || segments.Count == 0) return "";

            var richTextBuilder = new StringBuilder();

            foreach (var segment in segments)
                switch (segment.Type)
                {
                    case "text":
                        var textSegment = segment as TextSegment;
                        richTextBuilder.Append(textSegment?.Text ?? "");
                        break;
                    case "at":
                        var atSegment = segment as AtSegment;
                        if (atSegment?.IsAtAll == true)
                            richTextBuilder.Append("📢@所有人 ");
                        else
                            richTextBuilder.Append($"👤@{atSegment?.DisplayText ?? atSegment?.QQ} ");
                        break;
                    case "face":
                        var faceSegment = segment as FaceSegment;
                        richTextBuilder.Append($"😀[表情{faceSegment?.Id}] ");
                        break;
                    case "image":
                        var imageSegment = segment as ImageSegment;
                        richTextBuilder.Append("🖼️[圖片] ");
                        break;
                    case "record":
                        var recordSegment = segment as RecordSegment;
                        if (recordSegment?.Magic == true)
                            richTextBuilder.Append("🎙️[變聲語音] ");
                        else
                            richTextBuilder.Append("🎵[語音] ");
                        break;
                    case "video":
                        richTextBuilder.Append("🎬[視頻] ");
                        break;
                    case "file":
                        richTextBuilder.Append("📎[文件] ");
                        break;
                    case "reply":
                        var replySegment = segment as ReplySegment;
                        richTextBuilder.Append($"💬[回覆#{replySegment?.Id}] ");
                        break;
                    case "poke":
                        richTextBuilder.Append("👋[戳一戳] ");
                        break;
                    case "gift":
                        var giftSegment = segment as GiftSegment;
                        richTextBuilder.Append($"🎁[禮物給@{giftSegment?.QQ}] ");
                        break;
                    case "forward":
                        richTextBuilder.Append("↗️[轉發消息] ");
                        break;
                    case "node":
                        richTextBuilder.Append("🔗[消息節點] ");
                        break;
                    case "xml":
                        richTextBuilder.Append("📋[XML卡片] ");
                        break;
                    case "json":
                        richTextBuilder.Append("📋[JSON卡片] ");
                        break;
                    default:
                        richTextBuilder.Append($"❓[{segment.Type}] ");
                        break;
                }

            return richTextBuilder.ToString().Trim();
        }

        /// <summary>
        ///     獲取消息的媒體文件URL列表
        /// </summary>
        public static List<string> GetMediaUrls(List<MessageSegment> segments, string mediaType = null)
        {
            var urls = new List<string>();

            if (segments == null) return urls;

            foreach (var segment in segments)
            {
                string url = null;

                switch (segment.Type)
                {
                    case "image" when mediaType == null || mediaType == "image":
                        var imageSegment = segment as ImageSegment;
                        url = imageSegment?.Url ?? imageSegment?.File;
                        break;
                    case "record" when mediaType == null || mediaType == "record":
                        var recordSegment = segment as RecordSegment;
                        url = recordSegment?.Url ?? recordSegment?.File;
                        break;
                    case "video" when mediaType == null || mediaType == "video":
                        var videoSegment = segment as VideoSegment;
                        url = videoSegment?.Url ?? videoSegment?.File;
                        break;
                    case "file" when mediaType == null || mediaType == "file":
                        var fileSegment = segment as FileSegment;
                        url = fileSegment?.Url ?? fileSegment?.File;
                        break;
                }

                if (!string.IsNullOrEmpty(url)) urls.Add(url);
            }

            return urls;
        }

        /// <summary>
        ///     檢查消息是否包含特定類型的段落
        /// </summary>
        public static bool HasSegmentType(List<MessageSegment> segments, string type)
        {
            if (segments == null) return false;

            foreach (var segment in segments)
                if (segment.Type == type)
                    return true;
            return false;
        }

        /// <summary>
        ///     獲取特定類型的消息段
        /// </summary>
        public static List<MessageSegment> GetSegmentsByType(List<MessageSegment> segments, string type)
        {
            var result = new List<MessageSegment>();
            if (segments == null) return result;

            foreach (var segment in segments)
                if (segment.Type == type)
                    result.Add(segment);
            return result;
        }
    }
}