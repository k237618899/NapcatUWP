using System.ComponentModel;

namespace NapcatUWP.Models
{
    public class ChatItem : INotifyPropertyChanged
    {
        private string _avatarColor;
        private long _chatId; // 聊天對象的ID（好友UserId或群組GroupId）
        private bool _isGroup; // 是否為群組聊天
        private string _lastMessage;
        private string _lastTime;
        private int _memberCount; // 群組成員數（僅群組聊天有效）
        private string _name;
        private int _unreadCount;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string LastMessage
        {
            get => _lastMessage;
            set
            {
                _lastMessage = value;
                OnPropertyChanged(nameof(LastMessage));
                OnPropertyChanged(nameof(DisplayLastMessage)); // 通知显示消息属性更新
            }
        }

        /// <summary>
        ///     用于显示的最后消息（处理CQ码转换）
        /// </summary>
        public string DisplayLastMessage
        {
            get
            {
                if (string.IsNullOrEmpty(_lastMessage))
                    return "";

                return ProcessCQCodeForDisplay(_lastMessage);
            }
        }

        public string LastTime
        {
            get => _lastTime;
            set
            {
                _lastTime = value;
                OnPropertyChanged(nameof(LastTime));
            }
        }

        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                _unreadCount = value;
                OnPropertyChanged(nameof(UnreadCount));
            }
        }

        public string AvatarColor
        {
            get => _avatarColor;
            set
            {
                _avatarColor = value;
                OnPropertyChanged(nameof(AvatarColor));
            }
        }

        public long ChatId
        {
            get => _chatId;
            set
            {
                _chatId = value;
                OnPropertyChanged(nameof(ChatId));
            }
        }

        public bool IsGroup
        {
            get => _isGroup;
            set
            {
                _isGroup = value;
                OnPropertyChanged(nameof(IsGroup));
            }
        }

        public int MemberCount
        {
            get => _memberCount;
            set
            {
                _memberCount = value;
                OnPropertyChanged(nameof(MemberCount));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        ///     處理CQ碼以便在聊天列表中友好顯示 (UWP 15063相容版本)
        /// </summary>
        private string ProcessCQCodeForDisplay(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";

            // 创建内容副本进行处理
            var processedContent = content;

            // 替換圖片CQ碼
            while (processedContent.IndexOf("[CQ:image") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:image");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[圖片]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換語音CQ碼
            while (processedContent.IndexOf("[CQ:record") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:record");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                {
                    // 检查是否是变声语音
                    var cqCode = processedContent.Substring(start, end - start + 1);
                    var displayText = cqCode.Contains("magic=true") || cqCode.Contains("magic=1") ? "[變聲語音]" : "[語音]";
                    processedContent = processedContent.Substring(0, start) + displayText +
                                       processedContent.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // 替換視頻CQ碼
            while (processedContent.IndexOf("[CQ:video") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:video");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[視頻]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換文件CQ碼
            while (processedContent.IndexOf("[CQ:file") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:file");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[文件]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換@所有人
            processedContent = processedContent.Replace("[CQ:at,qq=all]", "@所有人");

            // 替換@某人CQ碼
            while (processedContent.IndexOf("[CQ:at,qq=") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:at,qq=");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                {
                    var qqStart = start + "[CQ:at,qq=".Length;
                    var qqEnd = end;

                    // 查找是否有其他參數
                    var commaIndex = processedContent.IndexOf(",", qqStart);
                    if (commaIndex > 0 && commaIndex < qqEnd) qqEnd = commaIndex;

                    var qq = processedContent.Substring(qqStart, qqEnd - qqStart);
                    processedContent = processedContent.Substring(0, start) + $"@{qq}" +
                                       processedContent.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // 替換表情CQ碼
            while (processedContent.IndexOf("[CQ:face") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:face");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[表情]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換回覆CQ碼
            while (processedContent.IndexOf("[CQ:reply") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:reply");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[回覆]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換戳一戳CQ碼
            while (processedContent.IndexOf("[CQ:poke") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:poke");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[戳一戳]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換禮物CQ碼
            while (processedContent.IndexOf("[CQ:gift") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:gift");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[禮物]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換轉發CQ碼
            while (processedContent.IndexOf("[CQ:forward") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:forward");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[轉發]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換XML卡片CQ碼
            while (processedContent.IndexOf("[CQ:xml") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:xml");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[XML卡片]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 替換JSON卡片CQ碼
            while (processedContent.IndexOf("[CQ:json") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:json");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[JSON卡片]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // 處理其他未知的CQ碼
            while (processedContent.IndexOf("[CQ:") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[多媒體內容]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            return processedContent;
        }
    }
}