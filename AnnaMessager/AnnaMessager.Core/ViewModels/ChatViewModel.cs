using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AnnaMessager.Core.Models;
using AnnaMessager.Core.Services;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;

namespace AnnaMessager.Core.ViewModels
{
    /// <summary>
    ///     聊天界面 ViewModel - IM 核心功能
    /// </summary>
    public class ChatViewModel : MvxViewModel, IDisposable
    {
        private readonly ICacheManager _cacheManager;
        private readonly INotificationService _notificationService;
        private readonly IOneBotService _oneBotService;

        private string _chatAvatar;
        private long _chatId;
        private string _chatName;
        private bool _disposed;
        private string _inputMessage;
        private bool _isGroup;
        private bool _isLoading;
        private bool _isOnline;
        private bool _isSending;
        private int _memberCount;
        private ObservableCollection<MessageItem> _messages;

        public ChatViewModel(long chatId, bool isGroup, string chatName)
        {
            _chatId = chatId;
            _isGroup = isGroup;
            _chatName = chatName;

            _oneBotService = Mvx.Resolve<IOneBotService>();
            _cacheManager = Mvx.Resolve<ICacheManager>();
            _notificationService = Mvx.Resolve<INotificationService>();

            Messages = new ObservableCollection<MessageItem>();

            // 初始化命令
            SendMessageCommand = new MvxCommand(async () => await SendMessageAsync(), () => CanSendMessage());
            LoadMoreMessagesCommand = new MvxCommand(async () => await LoadMoreMessagesAsync());
            SelectMessageCommand = new MvxCommand<MessageItem>(SelectMessage);
            DeleteMessageCommand = new MvxCommand<MessageItem>(async msg => await DeleteMessageAsync(msg));
            CopyMessageCommand = new MvxCommand<MessageItem>(CopyMessage);
            ResendMessageCommand = new MvxCommand<MessageItem>(async msg => await ResendMessageAsync(msg));

            // 註冊事件
            _oneBotService.MessageReceived += OnMessageReceived;
        }

        #region Event Handlers

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            if (e?.Message == null) return;

            try
            {
                var isFromCurrentChat = IsGroup
                    ? e.Message.GroupId == ChatId
                    : e.Message.UserId == ChatId;

                if (!isFromCurrentChat) return;

                var messageItem = new MessageItem
                {
                    MessageId = e.Message.MessageId,
                    Content = e.Message.Message,
                    Time = e.Message.DateTime,
                    IsFromSelf = false,
                    SenderName = e.Message.Sender?.Nickname ?? "未知用戶",
                    SenderId = e.Message.UserId,
                    MessageType = MessageType.Text,
                    SendStatus = MessageSendStatus.Sent
                };

                Messages.Add(messageItem);
                Debug.WriteLine($"收到新消息: {messageItem.Content}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"處理接收消息失敗: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    try
                    {
                        if (_oneBotService != null)
                        {
                            _oneBotService.MessageReceived -= OnMessageReceived;
                            Debug.WriteLine("已取消消息接收事件註冊");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"清理資源時發生錯誤: {ex.Message}");
                    }

                _disposed = true;
            }
        }

        #endregion

        #region Properties

        public long ChatId
        {
            get => _chatId;
            set => SetProperty(ref _chatId, value);
        }

        public bool IsGroup
        {
            get => _isGroup;
            set => SetProperty(ref _isGroup, value);
        }

        public string ChatName
        {
            get => _chatName;
            set => SetProperty(ref _chatName, value);
        }

        public string InputMessage
        {
            get => _inputMessage;
            set
            {
                SetProperty(ref _inputMessage, value);
                RaisePropertyChanged(() => CanSendMessage());
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsSending
        {
            get => _isSending;
            set => SetProperty(ref _isSending, value);
        }

        public ObservableCollection<MessageItem> Messages
        {
            get => _messages;
            set => SetProperty(ref _messages, value);
        }

        public string ChatAvatar
        {
            get => _chatAvatar;
            set => SetProperty(ref _chatAvatar, value);
        }

        public int MemberCount
        {
            get => _memberCount;
            set => SetProperty(ref _memberCount, value);
        }

        public bool IsOnline
        {
            get => _isOnline;
            set => SetProperty(ref _isOnline, value);
        }

        // 顯示用屬性
        public string ChatTitle => IsGroup ? $"{ChatName} ({MemberCount})" : ChatName;
        public string OnlineStatus => IsGroup ? $"{MemberCount} 位成員" : IsOnline ? "在線" : "離線";

        #endregion

        #region Commands

        public ICommand SendMessageCommand { get; }
        public ICommand LoadMoreMessagesCommand { get; }
        public ICommand SelectMessageCommand { get; }
        public ICommand DeleteMessageCommand { get; }
        public ICommand CopyMessageCommand { get; }
        public ICommand ResendMessageCommand { get; }

        #endregion

        #region Initialization

        public override async Task Initialize()
        {
            await base.Initialize();
            await LoadChatInfoAsync();
            await LoadMessagesAsync();

            // 清除該聊天的未讀通知
            try
            {
                await _notificationService.ClearChatNotificationsAsync(ChatId, IsGroup);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清除聊天通知失敗: {ex.Message}");
            }
        }

        private async Task LoadChatInfoAsync()
        {
            try
            {
                if (IsGroup)
                {
                    // 載入群組信息
                    var groupInfo = await _oneBotService.GetGroupInfoAsync(ChatId);
                    if (groupInfo?.Status == "ok" && groupInfo.Data != null)
                    {
                        ChatName = groupInfo.Data.GroupName;
                        MemberCount = groupInfo.Data.MemberCount;
                        IsOnline = true; // 群組總是在線
                    }
                }
                else
                {
                    // 載入好友信息
                    var strangerInfo = await _oneBotService.GetStrangerInfoAsync(ChatId);
                    if (strangerInfo?.Status == "ok" && strangerInfo.Data != null)
                        IsOnline = true; // 簡化處理，假設在線
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入聊天信息失敗: {ex.Message}");
            }
        }

        #endregion

        #region Message Operations

        private async Task LoadMessagesAsync()
        {
            try
            {
                IsLoading = true;
                await Task.Delay(500);
                Debug.WriteLine($"聊天界面已初始化: {ChatTitle}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入消息失敗: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadMoreMessagesAsync()
        {
            try
            {
                Debug.WriteLine("載入更多消息...");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入更多消息失敗: {ex.Message}");
            }
        }

        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(InputMessage) &&
                   !IsSending &&
                   _oneBotService.IsConnected;
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage()) return;

            try
            {
                IsSending = true;
                var messageContent = InputMessage.Trim();
                InputMessage = string.Empty;

                var messageItem = new MessageItem
                {
                    MessageId = DateTime.Now.Ticks,
                    Content = messageContent,
                    Time = DateTime.Now,
                    IsFromSelf = true,
                    SendStatus = MessageSendStatus.Sending,
                    MessageType = MessageType.Text
                };

                Messages.Add(messageItem);

                OneBotResponse<object> result;
                if (IsGroup)
                    result = await _oneBotService.SendGroupMsgAsync(ChatId, messageContent);
                else
                    result = await _oneBotService.SendPrivateMsgAsync(ChatId, messageContent);

                messageItem.SendStatus = result?.Status == "ok" ? MessageSendStatus.Sent : MessageSendStatus.Failed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"發送消息失敗: {ex.Message}");
                var lastMessage = Messages.LastOrDefault(m => m.IsFromSelf);
                if (lastMessage != null) lastMessage.SendStatus = MessageSendStatus.Failed;
            }
            finally
            {
                IsSending = false;
            }
        }

        private Task DeleteMessageAsync(MessageItem message)
        {
            if (message == null)
            {
                // PCL Profile111 相容的方式
                var tcs = new TaskCompletionSource<bool>();
                tcs.SetResult(true);
                return tcs.Task;
            }

            Messages.Remove(message);
            Debug.WriteLine($"已刪除消息: {message.MessageId}");

            // PCL Profile111 相容的方式
            var completedTcs = new TaskCompletionSource<bool>();
            completedTcs.SetResult(true);
            return completedTcs.Task;
        }

        private void CopyMessage(MessageItem message)
        {
            if (message?.Content != null) Debug.WriteLine($"已複製消息: {message.Content}");
        }

        private async Task ResendMessageAsync(MessageItem message)
        {
            if (message == null) return;

            try
            {
                message.SendStatus = MessageSendStatus.Sending;

                OneBotResponse<object> result;
                if (IsGroup)
                    result = await _oneBotService.SendGroupMsgAsync(ChatId, message.Content);
                else
                    result = await _oneBotService.SendPrivateMsgAsync(ChatId, message.Content);

                message.SendStatus = result?.Status == "ok" ? MessageSendStatus.Sent : MessageSendStatus.Failed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重發消息失敗: {ex.Message}");
                message.SendStatus = MessageSendStatus.Failed;
            }
        }

        private void SelectMessage(MessageItem message)
        {
            if (message != null) message.IsSelected = !message.IsSelected;
        }

        #endregion
    }
}