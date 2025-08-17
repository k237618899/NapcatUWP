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
            ScrollToBottom();
            this.AddHandler(UIElement.TappedEvent, new TappedEventHandler(OnAnyTapped), true);
            _mediaTimer.Tick += _mediaTimer_Tick;
            _mediaPlayer.CurrentStateChanged += _mediaPlayer_CurrentStateChanged;
        }

        private async void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (MessageScrollViewer != null && MessageScrollViewer.VerticalOffset + 120 >= MessageScrollViewer.ScrollableHeight)
                        ScrollToBottom();
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
        }

        private void ScrollToBottom()
        {
            try { MessageScrollViewer?.ChangeView(null, MessageScrollViewer.ScrollableHeight, null, true); } catch { }
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