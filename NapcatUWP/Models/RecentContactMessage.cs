using System.Collections.Generic;
using System.ComponentModel;

namespace NapcatUWP.Models
{
    public class RecentContactMessage : INotifyPropertyChanged
    {
        // 基本消息信息
        public long SelfId { get; set; }
        public long UserId { get; set; }
        public long Time { get; set; }
        public long MessageId { get; set; }
        public long MessageSeq { get; set; }
        public long RealId { get; set; }
        public string MessageType { get; set; }
        public string RawMessage { get; set; }
        public long Font { get; set; }
        public string SubType { get; set; }
        public string PostType { get; set; }
        public string MessageSentType { get; set; }
        public long GroupId { get; set; }
        public string PeerUin { get; set; }
        public string Remark { get; set; }
        public string MsgTime { get; set; }
        public long ChatType { get; set; }
        public string MsgId { get; set; }
        public string SendNickName { get; set; }
        public string SendMemberName { get; set; }
        public string PeerName { get; set; }
        public string Message { get; set; }
        public string Wording { get; set; }
        public string ParsedMessage { get; set; }

        // 發送者信息
        public MessageSender Sender { get; set; }

        // 添加消息段支持
        public List<MessageSegment> MessageSegments { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MessageSender
    {
        public long UserId { get; set; }
        public string Nickname { get; set; }
        public string Card { get; set; }
        public string Sex { get; set; }
        public int Age { get; set; }
        public string Area { get; set; }
        public string Level { get; set; }
        public string Role { get; set; }
        public string Title { get; set; }
    }
}