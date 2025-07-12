using System.ComponentModel;

namespace NapcatUWP.Models
{
    public class ChatItem : INotifyPropertyChanged
    {
        private string _avatarColor;
        private long _chatId; // ���쌦���ID������UserId��Ⱥ�MGroupId��
        private bool _isGroup; // �Ƿ��Ⱥ�M����
        private string _lastMessage;
        private string _lastTime;
        private int _memberCount; // Ⱥ�M�ɆT�����HȺ�M������Ч��
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
                OnPropertyChanged(nameof(DisplayLastMessage)); // ֪ͨ��ʾ��Ϣ���Ը���
            }
        }

        /// <summary>
        ///     ������ʾ�������Ϣ������CQ��ת����
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
        ///     ̎��CQ�a�Ա��������б����Ѻ��@ʾ (UWP 15063���ݰ汾)
        /// </summary>
        private string ProcessCQCodeForDisplay(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";

            // �������ݸ������д���
            var processedContent = content;

            // ��Q�DƬCQ�a
            while (processedContent.IndexOf("[CQ:image") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:image");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[�DƬ]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��Q�Z��CQ�a
            while (processedContent.IndexOf("[CQ:record") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:record");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                {
                    // ����Ƿ��Ǳ�������
                    var cqCode = processedContent.Substring(start, end - start + 1);
                    var displayText = cqCode.Contains("magic=true") || cqCode.Contains("magic=1") ? "[׃�Z��]" : "[�Z��]";
                    processedContent = processedContent.Substring(0, start) + displayText +
                                       processedContent.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            // ��Qҕ�lCQ�a
            while (processedContent.IndexOf("[CQ:video") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:video");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[ҕ�l]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��Q�ļ�CQ�a
            while (processedContent.IndexOf("[CQ:file") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:file");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[�ļ�]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��Q@������
            processedContent = processedContent.Replace("[CQ:at,qq=all]", "@������");

            // ��Q@ĳ��CQ�a
            while (processedContent.IndexOf("[CQ:at,qq=") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:at,qq=");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                {
                    var qqStart = start + "[CQ:at,qq=".Length;
                    var qqEnd = end;

                    // �����Ƿ�����������
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

            // ��Q����CQ�a
            while (processedContent.IndexOf("[CQ:face") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:face");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[����]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��Q�ظ�CQ�a
            while (processedContent.IndexOf("[CQ:reply") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:reply");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[�ظ�]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��Q��һ��CQ�a
            while (processedContent.IndexOf("[CQ:poke") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:poke");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[��һ��]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��Q�Y��CQ�a
            while (processedContent.IndexOf("[CQ:gift") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:gift");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[�Y��]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��Q�D�lCQ�a
            while (processedContent.IndexOf("[CQ:forward") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:forward");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[�D�l]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��QXML��ƬCQ�a
            while (processedContent.IndexOf("[CQ:xml") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:xml");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[XML��Ƭ]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ��QJSON��ƬCQ�a
            while (processedContent.IndexOf("[CQ:json") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:json");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[JSON��Ƭ]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            // ̎������δ֪��CQ�a
            while (processedContent.IndexOf("[CQ:") >= 0)
            {
                var start = processedContent.IndexOf("[CQ:");
                var end = processedContent.IndexOf("]", start);
                if (end > start)
                    processedContent = processedContent.Substring(0, start) + "[��ý�w����]" +
                                       processedContent.Substring(end + 1);
                else
                    break;
            }

            return processedContent;
        }
    }
}