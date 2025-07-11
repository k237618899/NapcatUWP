using System;
using System.Collections.Generic;
using System.ComponentModel;
using NapcatUWP.Tools;

namespace NapcatUWP.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _content;
        private bool _isFromMe;
        private string _messageType;
        private string _richTextContent;
        private List<MessageSegment> _segments;
        private long _senderId;
        private string _senderName;
        private DateTime _timestamp;

        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                OnPropertyChanged(nameof(Content));
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
                OnPropertyChanged(nameof(TimeString));
            }
        }

        public bool IsFromMe
        {
            get => _isFromMe;
            set
            {
                _isFromMe = value;
                OnPropertyChanged(nameof(IsFromMe));
            }
        }

        public string SenderName
        {
            get => _senderName;
            set
            {
                _senderName = value;
                OnPropertyChanged(nameof(SenderName));
            }
        }

        public long SenderId
        {
            get => _senderId;
            set
            {
                _senderId = value;
                OnPropertyChanged(nameof(SenderId));
            }
        }

        public string MessageType
        {
            get => _messageType;
            set
            {
                _messageType = value;
                OnPropertyChanged(nameof(MessageType));
            }
        }

        /// <summary>
        ///     消息段列表，包含完整的消息結構
        /// </summary>
        public List<MessageSegment> Segments
        {
            get => _segments ?? (_segments = new List<MessageSegment>());
            set
            {
                _segments = value;
                OnPropertyChanged(nameof(Segments));
                OnPropertyChanged(nameof(HasRichContent));
                // 當段落更新時，重新生成富文本內容
                GenerateRichTextContent();
            }
        }

        /// <summary>
        ///     富文本內容，用於UI展示
        /// </summary>
        public string RichTextContent
        {
            get => _richTextContent ?? Content;
            set
            {
                _richTextContent = value;
                OnPropertyChanged(nameof(RichTextContent));
            }
        }

        /// <summary>
        ///     是否包含富內容（圖片、語音等）
        /// </summary>
        public bool HasRichContent
        {
            get
            {
                if (Segments == null || Segments.Count == 0) return false;

                foreach (var segment in Segments)
                    if (segment.Type != "text")
                        return true;
                return false;
            }
        }

        /// <summary>
        ///     是否包含圖片
        /// </summary>
        public bool HasImages => MessageSegmentParser.HasSegmentType(Segments, "image");

        /// <summary>
        ///     是否包含語音
        /// </summary>
        public bool HasRecord => MessageSegmentParser.HasSegmentType(Segments, "record");

        /// <summary>
        ///     是否包含視頻
        /// </summary>
        public bool HasVideo => MessageSegmentParser.HasSegmentType(Segments, "video");

        /// <summary>
        ///     是否包含@提及
        /// </summary>
        public bool HasAt => MessageSegmentParser.HasSegmentType(Segments, "at");

        /// <summary>
        ///     是否是回覆消息
        /// </summary>
        public bool IsReply => MessageSegmentParser.HasSegmentType(Segments, "reply");

        public string TimeString => Timestamp.ToString("HH:mm");

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     根據消息段生成富文本內容
        /// </summary>
        private void GenerateRichTextContent()
        {
            if (Segments == null || Segments.Count == 0)
            {
                RichTextContent = Content;
                return;
            }

            var richContent = MessageSegmentParser.GenerateRichTextFromSegments(Segments);
            RichTextContent = richContent;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}