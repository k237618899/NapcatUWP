using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using AnnaMessager.Core.ViewModels;
using AnnaMessager.Core.Models;
using MvvmCross.Uwp.Views;
using Windows.Media.Playback;
using Windows.Media.Core;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class ChatView : MvxWindowsPage
    {
        public ChatView()
        {
            InitializeComponent();
            Loaded += ChatView_Loaded;
        }

        public new ChatViewModel ViewModel => (ChatViewModel)DataContext;

        private MediaPlayer _mediaPlayer = new MediaPlayer();
        private DispatcherTimer _mediaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        private MessageSegment _currentPlayingSegment;
        private bool _isPanning; private Windows.Foundation.Point _lastPoint; private double _startH; private double _startV;

        private void ChatView_Loaded(object sender, RoutedEventArgs e)
        {
            InputBox?.Focus(FocusState.Programmatic);
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                if (ViewModel.Messages != null) ViewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            }
            
            this.AddHandler(UIElement.TappedEvent, new TappedEventHandler(OnAnyTapped), true);
            _mediaTimer.Tick += _mediaTimer_Tick;
            _mediaPlayer.CurrentStateChanged += _mediaPlayer_CurrentStateChanged;
        }

        // 新增: 處理段內圖片載入失敗 (避免 XAML 綁定錯誤)
        private void SegmentImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            try
            {
                var img = sender as Image;
                if (img?.DataContext is MessageSegment seg)
                {
                    object fileObj; seg.Data.TryGetValue("file", out fileObj);
                    var fileId = fileObj?.ToString();
                    object urlObj; seg.Data.TryGetValue("url", out urlObj);
                    var originalUrl = urlObj?.ToString();
                    Debug.WriteLine($"[ChatView] Segment image load failed file={fileId} url={originalUrl} error={e.ErrorMessage}");
                    if (!string.IsNullOrEmpty(fileId) && (string.IsNullOrEmpty(originalUrl) || originalUrl.IndexOf("http", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        // 嘗試組合常見 QQ 圖片備援 URL (僅猜測性)
                        var fallback = $"https://gchat.qpic.cn/gchatpic_new/0/0-0-{fileId}/0";
                        try { img.Source = new BitmapImage(new Uri(fallback)); Debug.WriteLine("[ChatView] Try fallback url: " + fallback); }
                        catch (Exception ex2) { Debug.WriteLine("[ChatView] Fallback failed: " + ex2.Message); }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ChatView] SegmentImage_ImageFailed handler exception: " + ex.Message);
            }
        }

        // 新增: 處理發送者頭像載入失敗
        private void SenderAvatarImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            try
            {
                var img = sender as Image;
                if (img?.DataContext is MessageItem msg && msg.SenderId > 0)
                {
                    Debug.WriteLine($"[ChatView] Sender avatar load failed for {msg.SenderId}, trying to reload");
                    
                    // 重新構建頭像 URL
                    var newAvatarUrl = BuildAvatarUrl(msg.SenderId);
                    if (!string.IsNullOrEmpty(newAvatarUrl) && newAvatarUrl != msg.SenderAvatar)
                    {
                        msg.SenderAvatar = newAvatarUrl;
                        Debug.WriteLine($"[ChatView] Updated sender avatar URL: {msg.SenderId} -> {newAvatarUrl}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatView] SenderAvatarImage_ImageFailed handler exception: {ex.Message}");
            }
        }

        // 輔助方法：構建頭像 URL
        private string BuildAvatarUrl(long senderId)
        {
            if (senderId <= 0) return null;
            return $"https://q1.qlogo.cn/g?b=qq&nk={senderId}&s=100";
        }

        private async void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    // 修复：改善自动滚动逻辑
                    if (MessageScrollViewer != null)
                    {
                        // 如果用户接近底部（距离底部不超过200像素），则自动滚动
                        var threshold = 200;
                        var isNearBottom = MessageScrollViewer.VerticalOffset + threshold >= MessageScrollViewer.ScrollableHeight;
                        
                        if (isNearBottom || MessageScrollViewer.ScrollableHeight == 0)
                        {
                            // 延迟一点时间，确保新消息已经渲染
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(100);
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    ScrollToBottom();
                                });
                            });
                        }
                    }
                });
            }
            // 修复：处理重置操作（如批量加载历史消息后)
            else if (e.Action == NotifyCollectionChangedAction.Reset || 
                     (e.Action == NotifyCollectionChangedAction.Replace && e.NewItems?.Count > 1))
            {
                // 延迟滚动，确保所有消息渲染完成
                _ = Task.Run(async () =>
                {
                    await Task.Delay(800); // 增加延迟时间，确保缓存消息加载完成
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        ScrollToBottom();
                        Debug.WriteLine("[ChatView] 批量消息加载完成，滚动到底部");
                    });
                });
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatViewModel.ScrollToMessageRequestId))
            {
                var id = ViewModel.ScrollToMessageRequestId;
                if (id > 0)
                {
                    TryScrollToMessage(id);
                }
            }
            // 修复：监听消息列表初始化完成
            else if (e.PropertyName == nameof(ChatViewModel.Messages))
            {
                if (ViewModel.Messages != null)
                {
                    ViewModel.Messages.CollectionChanged -= Messages_CollectionChanged; // 避免重复订阅
                    ViewModel.Messages.CollectionChanged += Messages_CollectionChanged;
                    
                    // 延迟滚动到底部
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            ScrollToBottom();
                        });
                    });
                }
            }
            // 修复：监听加载状态变化
            else if (e.PropertyName == nameof(ChatViewModel.IsLoading))
            {
                // 当加载完成时滚动到底部
                if (!ViewModel.IsLoading)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            ScrollToBottom();
                            Debug.WriteLine("[ChatView] 加载完成，滚动到底部");
                        });
                    });
                }
            }
        }

        private void ScrollToBottom()
        {
            try 
            { 
                if (MessageScrollViewer != null && MessageScrollViewer.ScrollableHeight > 0)
                {
                    MessageScrollViewer.ChangeView(null, MessageScrollViewer.ScrollableHeight, null, false);
                    Debug.WriteLine($"[ChatView] 滚动到底部: ScrollableHeight={MessageScrollViewer.ScrollableHeight}");
                }
            } 
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatView] 滚动失败: {ex.Message}");
            }
        }

        private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                if (!shift)
                {
                    e.Handled = true;
                    if (ViewModel?.SendMessageCommand?.CanExecute(null) == true)
                        ViewModel.SendMessageCommand.Execute(null);
                }
            }
        }

        private async void AttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary, ViewMode = PickerViewMode.Thumbnail };
                picker.FileTypeFilter.Add(".jpg"); picker.FileTypeFilter.Add(".jpeg"); picker.FileTypeFilter.Add(".png"); picker.FileTypeFilter.Add(".gif"); picker.FileTypeFilter.Add(".bmp");
                var file = await picker.PickSingleFileAsync(); if (file == null) return;
                var buffer = await FileIO.ReadBufferAsync(file);
                byte[] bytes; using (var reader = DataReader.FromBuffer(buffer)) { bytes = new byte[buffer.Length]; reader.ReadBytes(bytes); }
                var base64 = Convert.ToBase64String(bytes);
                ViewModel?.UploadImageCommand?.Execute(new MediaUploadPayload { FileName = file.Name, Base64Data = base64 });
            }
            catch (Exception ex) { Debug.WriteLine("選擇或發送圖片失敗: " + ex.Message); }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack(); else Frame.Navigate(typeof(MainView));
        }

        private void ToggleEmojiBtn_Click(object sender, RoutedEventArgs e) => ToggleEmojiPanel();
        private void ToggleEmojiPanel()
        {
            if (EmojiPanel == null) return;
            EmojiPanel.Visibility = EmojiPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnAnyTapped(object sender, TappedRoutedEventArgs e)
        {
            var fe = e.OriginalSource as FrameworkElement;
            if (fe is Image img && img.Source is BitmapImage bi && bi.UriSource != null)
            {
                _ = ShowImagePreviewAsync(bi.UriSource.ToString());
            }
            // Emoji 點選插入 (Panel 中 TextBlock)
            var tb = fe as TextBlock;
            if (tb != null && EmojiPanel != null && EmojiPanel.Visibility == Visibility.Visible && tb.Parent is Panel)
            {
                try { InputBox.Text += tb.Text; InputBox.Focus(FocusState.Programmatic); } catch { }
            }
        }

        private async Task ShowImagePreviewAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                ImagePreviewLayer.Visibility = Visibility.Visible;
                PreviewLoading.IsActive = true; PreviewImage.Source = null;
                using (var http = new System.Net.Http.HttpClient())
                {
                    var bytes = await http.GetByteArrayAsync(url);
                    var bi = new BitmapImage();
                    using (var ms = new InMemoryRandomAccessStream())
                    {
                        var dw = new DataWriter(ms); dw.WriteBytes(bytes); await dw.StoreAsync(); ms.Seek(0); await bi.SetSourceAsync(ms);
                    }
                    PreviewImage.Source = bi;
                }
            }
            catch (Exception ex) { Debug.WriteLine("圖片預覽失敗: " + ex.Message); }
            finally { PreviewLoading.IsActive = false; }
        }

        private void ClosePreview_Click(object sender, RoutedEventArgs e)
        { ImagePreviewLayer.Visibility = Visibility.Collapsed; PreviewImage.Source = null; }
        private void PreviewLayer_Tapped(object sender, TappedRoutedEventArgs e)
        { if (e.OriginalSource == ImagePreviewLayer) ClosePreview_Click(null, null); }
        private void PreviewLayer_KeyDown(object sender, KeyRoutedEventArgs e)
        { if (e.Key == VirtualKey.Escape) ClosePreview_Click(null, null); }

        private void SegmentPlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button; if (btn == null) return; var segment = btn.DataContext as MessageSegment; if (segment == null) return;
                var t = segment.Type?.ToLower(); if (t != "record" && t != "video") return;
                if (_currentPlayingSegment == segment && _mediaPlayer.CurrentState == MediaPlayerState.Playing)
                { _mediaPlayer.Pause(); return; }
                object urlObj; if (!segment.Data.TryGetValue("url", out urlObj)) segment.Data.TryGetValue("file", out urlObj);
                var url = urlObj?.ToString(); if (string.IsNullOrEmpty(url)) return;
                _currentPlayingSegment = segment; _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(url)); _mediaPlayer.Play();
            }
            catch (Exception ex) { Debug.WriteLine("播放失敗: " + ex.Message); }
        }

        private void _mediaTimer_Tick(object sender, object e)
        { try { var pos = _mediaPlayer.PlaybackSession.Position; } catch { } }
        private void _mediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        { if (sender.CurrentState == MediaPlayerState.Playing) _mediaTimer.Start(); else _mediaTimer.Stop(); }

        private void ReplyBorder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var fe = sender as FrameworkElement; if (fe?.Tag == null) return;
            if (long.TryParse(fe.Tag.ToString(), out var targetId))
            {
                ViewModel?.RequestScrollToMessage(targetId);
                // 嘗試直接定位 (若已存在於列表)
                TryScrollToMessage(targetId);
            }
        }

        private void TryScrollToMessage(long messageId)
        {
            try
            {
                var itemsControl = MessageScrollViewer?.Content as ItemsControl; if (itemsControl == null) return;
                for (int i = 0; i < itemsControl.Items.Count; i++)
                {
                    var data = itemsControl.Items[i] as MessageItem; if (data == null) continue;
                    if (data.MessageId == messageId)
                    {
                        var container = itemsControl.ContainerFromIndex(i) as FrameworkElement;
                        if (container != null)
                        {
                            try
                            {
                                var transform = container.TransformToVisual(MessageScrollViewer.Content as UIElement);
                                var point = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                                MessageScrollViewer.ChangeView(null, point.Y - 40, null, true);
                            }
                            catch { container.StartBringIntoView(); }
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        private async void VoiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.MusicLibrary, ViewMode = PickerViewMode.List };
                picker.FileTypeFilter.Add(".mp3"); picker.FileTypeFilter.Add(".wav"); picker.FileTypeFilter.Add(".m4a"); picker.FileTypeFilter.Add(".amr");
                var file = await picker.PickSingleFileAsync(); if (file == null) return;
                var buffer = await FileIO.ReadBufferAsync(file); byte[] bytes; using (var reader = DataReader.FromBuffer(buffer)) { bytes = new byte[buffer.Length]; reader.ReadBytes(bytes); }
                var base64 = Convert.ToBase64String(bytes);
                ViewModel?.UploadVoiceCommand?.Execute(new MediaUploadPayload { FileName = file.Name, Base64Data = base64, MediaType = MessageType.Voice });
            }
            catch (Exception ex) { Debug.WriteLine("語音檔選擇失敗: " + ex.Message); }
        }

        private async void VideoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary, ViewMode = PickerViewMode.Thumbnail };
                picker.FileTypeFilter.Add(".mp4"); picker.FileTypeFilter.Add(".mov"); picker.FileTypeFilter.Add(".wmv"); picker.FileTypeFilter.Add(".avi");
                var file = await picker.PickSingleFileAsync(); if (file == null) return;
                var buffer = await FileIO.ReadBufferAsync(file); byte[] bytes; using (var reader = DataReader.FromBuffer(buffer)) { bytes = new byte[buffer.Length]; reader.ReadBytes(bytes); }
                var base64 = Convert.ToBase64String(bytes);
                ViewModel?.UploadVideoCommand?.Execute(new MediaUploadPayload { FileName = file.Name, Base64Data = base64, MediaType = MessageType.Video });
            }
            catch (Exception ex) { Debug.WriteLine("影片檔選擇失敗: " + ex.Message); }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (ViewModel?.Messages != null) ViewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            if (ViewModel != null) ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            base.OnNavigatedFrom(e);
        }
    }
}