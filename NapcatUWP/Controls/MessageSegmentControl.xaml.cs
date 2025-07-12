using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using NapcatUWP.Models;

namespace NapcatUWP.Controls
{
    /// <summary>
    ///     视频播放事件参数类
    /// </summary>
    public class VideoPlayEventArgs : EventArgs
    {
        public VideoPlayEventArgs(string videoUrl, string title = "视频播放")
        {
            VideoUrl = videoUrl;
            Title = title;
        }

        public string VideoUrl { get; }
        public string Title { get; }
    }

    /// <summary>
    ///     圖片查看事件參數類
    /// </summary>
    public class ImageViewEventArgs : EventArgs
    {
        public ImageViewEventArgs(string imageUrl, string title = "圖片查看")
        {
            ImageUrl = imageUrl;
            Title = title;
        }

        public string ImageUrl { get; }
        public string Title { get; }
    }

    /// <summary>
    ///     音頻播放事件參數類
    /// </summary>
    public class AudioPlayRequestEventArgs : EventArgs
    {
        public AudioPlayRequestEventArgs(string audioUrl, string title = "音頻播放")
        {
            AudioUrl = audioUrl;
            Title = title;
        }

        public string AudioUrl { get; }
        public string Title { get; }
    }

    public sealed partial class MessageSegmentControl : UserControl
    {
        public static readonly DependencyProperty SegmentsProperty =
            DependencyProperty.Register(nameof(Segments), typeof(IList<MessageSegment>), typeof(MessageSegmentControl),
                new PropertyMetadata(null, OnSegmentsChanged));

        public MessageSegmentControl()
        {
            InitializeComponent();

            // 訂閱音頻播放狀態改變事件
            AudioPlayerManager.Instance.PlaybackStateChanged += AudioPlayerManager_PlaybackStateChanged;
        }

        public IList<MessageSegment> Segments
        {
            get => (IList<MessageSegment>)GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, value);
        }

        // 各種媒體播放事件
        public event EventHandler<VideoPlayEventArgs> VideoPlayRequested;
        public event EventHandler<ImageViewEventArgs> ImageViewRequested;
        public event EventHandler<AudioPlayRequestEventArgs> AudioPlayRequested;

        private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as MessageSegmentControl;
            control?.RenderSegments();
        }

        private void RenderSegments()
        {
            ContentPanel.Children.Clear();

            if (Segments == null) return;

            foreach (var segment in Segments)
            {
                var element = CreateSegmentElement(segment);
                if (element != null) ContentPanel.Children.Add(element);
            }
        }

        private void AudioPlayerManager_PlaybackStateChanged(object sender, AudioPlayEventArgs e)
        {
            // 更新音頻控件的顯示狀態
            UpdateAudioSegmentState(e.AudioUrl, e.IsPlaying);
        }

        private void UpdateAudioSegmentState(string audioUrl, bool isPlaying)
        {
            // 遍歷所有音頻段並更新狀態
            foreach (var child in ContentPanel.Children)
                if (child is Border border && border.Tag?.ToString() == audioUrl)
                    UpdateAudioBorderState(border, isPlaying);
        }

        private void UpdateAudioBorderState(Border border, bool isPlaying)
        {
            if (border.Child is StackPanel stackPanel && stackPanel.Children.Count >= 2)
                if (stackPanel.Children[1] is TextBlock textBlock)
                    textBlock.Text = isPlaying ? "⏸️ 正在播放..." : "🎵 語音消息";
        }

        private FrameworkElement CreateSegmentElement(MessageSegment segment)
        {
            Debug.WriteLine($"創建消息段元素: Type={segment.Type}, ActualType={segment.GetType().Name}");

            switch (segment.Type)
            {
                case "text":
                    return CreateTextSegment(segment as TextSegment ??
                                             new TextSegment(segment.Data.ContainsKey("text")
                                                 ? segment.Data["text"]?.ToString() ?? ""
                                                 : ""));
                case "image":
                    return CreateImageSegment(segment as ImageSegment ?? CreateImageSegmentFromData(segment));
                case "at":
                    return CreateAtSegment(segment as AtSegment ?? CreateAtSegmentFromData(segment));
                case "face":
                    return CreateFaceSegment(segment as FaceSegment ?? CreateFaceSegmentFromData(segment));
                case "record":
                    return CreateRecordSegment(segment as RecordSegment ?? CreateRecordSegmentFromData(segment));
                case "video":
                    return CreateVideoSegment(segment as VideoSegment ?? CreateVideoSegmentFromData(segment));
                case "file":
                    return CreateFileSegment(segment as FileSegment ?? CreateFileSegmentFromData(segment));
                case "reply":
                    return CreateReplySegment(segment as ReplySegment ?? CreateReplySegmentFromData(segment));
                default:
                    return CreateDefaultSegment(segment);
            }
        }

        #region 辅助方法 - 从通用MessageSegment创建具体类型

        private ImageSegment CreateImageSegmentFromData(MessageSegment segment)
        {
            var imageSegment = new ImageSegment();
            // 複製數據
            foreach (var kvp in segment.Data) imageSegment.Data[kvp.Key] = kvp.Value;

            return imageSegment;
        }

        private AtSegment CreateAtSegmentFromData(MessageSegment segment)
        {
            var atSegment = new AtSegment();
            foreach (var kvp in segment.Data) atSegment.Data[kvp.Key] = kvp.Value;

            return atSegment;
        }

        private FaceSegment CreateFaceSegmentFromData(MessageSegment segment)
        {
            var faceSegment = new FaceSegment();
            foreach (var kvp in segment.Data) faceSegment.Data[kvp.Key] = kvp.Value;

            return faceSegment;
        }

        private RecordSegment CreateRecordSegmentFromData(MessageSegment segment)
        {
            var recordSegment = new RecordSegment();
            foreach (var kvp in segment.Data) recordSegment.Data[kvp.Key] = kvp.Value;

            return recordSegment;
        }

        private VideoSegment CreateVideoSegmentFromData(MessageSegment segment)
        {
            var videoSegment = new VideoSegment();
            foreach (var kvp in segment.Data) videoSegment.Data[kvp.Key] = kvp.Value;

            return videoSegment;
        }

        private FileSegment CreateFileSegmentFromData(MessageSegment segment)
        {
            var fileSegment = new FileSegment();
            foreach (var kvp in segment.Data) fileSegment.Data[kvp.Key] = kvp.Value;

            return fileSegment;
        }

        private ReplySegment CreateReplySegmentFromData(MessageSegment segment)
        {
            var replySegment = new ReplySegment();
            foreach (var kvp in segment.Data) replySegment.Data[kvp.Key] = kvp.Value;

            return replySegment;
        }

        #endregion

        #region 创建UI元素的方法

        private TextBlock CreateTextSegment(TextSegment segment)
        {
            return new TextBlock
            {
                Text = segment?.Text ?? "",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 4, 0)
            };
        }

        private Border CreateImageSegment(ImageSegment segment)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 65, 71)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 4, 0, 4),
                MinWidth = 200,
                MinHeight = 100,
                MaxWidth = 250,
                MaxHeight = 200
            };

            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 設置圖片源
            if (!string.IsNullOrEmpty(segment?.Url))
                try
                {
                    image.Source = new BitmapImage(new Uri(segment.Url));
                    border.Child = image;
                }
                catch
                {
                    border.Child = CreateImagePlaceholder("🖼️ 圖片");
                }
            else
                border.Child = CreateImagePlaceholder("🖼️ 圖片");

            // 添加點擊事件處理
            border.Tapped += (sender, e) =>
            {
                try
                {
                    var imageUrl = segment?.Url;
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        Debug.WriteLine($"MessageSegmentControl: 請求查看圖片 - URL: {imageUrl}");
                        ImageViewRequested?.Invoke(this, new ImageViewEventArgs(imageUrl));
                    }
                    else
                    {
                        Debug.WriteLine("MessageSegmentControl: 圖片URL為空，無法查看");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MessageSegmentControl: 處理圖片點擊時發生錯誤: {ex.Message}");
                }
            };

            // 添加指針進入和離開事件來模擬 cursor 效果
            border.PointerEntered += (sender, e) =>
            {
                try
                {
                    Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Hand, 1);
                }
                catch
                {
                }
            };

            border.PointerExited += (sender, e) =>
            {
                try
                {
                    Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
                }
                catch
                {
                }
            };

            return border;
        }

        private Border CreateAtSegment(AtSegment segment)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 126, 108, 168)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var textBlock = new TextBlock
            {
                Text = segment?.IsAtAll == true ? "@所有人" : segment?.DisplayText ?? $"@{segment?.QQ}",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };

            border.Child = textBlock;
            return border;
        }

        private Border CreateFaceSegment(FaceSegment segment)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 65, 71)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var textBlock = new TextBlock
            {
                Text = $"😀[表情{segment?.Id}]",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14
            };

            border.Child = textBlock;
            return border;
        }

        private Border CreateRecordSegment(RecordSegment segment)
        {
            var audioUrl = segment?.Url ?? segment?.File ?? "";
            var isCurrentlyPlaying = AudioPlayerManager.Instance.IsAudioPlaying(audioUrl);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 4, 0, 4),
                MinWidth = 120,
                Tag = audioUrl // 用於識別音頻段
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var icon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Glyph = segment?.Magic == true ? "\uE1D6" : "\uE189",
                FontSize = 16,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 8, 0)
            };

            var textBlock = new TextBlock
            {
                Text = isCurrentlyPlaying ? "⏸️ 正在播放..." :
                    segment?.Magic == true ? "🎵 變聲語音" : "🎵 語音消息",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(textBlock);
            border.Child = stackPanel;

            // 添加點擊事件處理
            border.Tapped += (sender, e) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(audioUrl))
                    {
                        Debug.WriteLine($"MessageSegmentControl: 請求播放音頻 - URL: {audioUrl}");

                        // 直接調用音頻管理器
                        AudioPlayerManager.Instance.PlayOrPauseAudio(audioUrl,
                            segment?.Magic == true ? "變聲語音" : "語音消息");

                        // 也可以觸發事件（如果需要的話）
                        AudioPlayRequested?.Invoke(this, new AudioPlayRequestEventArgs(audioUrl,
                            segment?.Magic == true ? "變聲語音" : "語音消息"));
                    }
                    else
                    {
                        Debug.WriteLine("MessageSegmentControl: 音頻URL為空，無法播放");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MessageSegmentControl: 處理音頻點擊時發生錯誤: {ex.Message}");
                }
            };

            // 添加指針進入和離開事件來模擬 cursor 效果
            border.PointerEntered += (sender, e) =>
            {
                try
                {
                    Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Hand, 1);
                }
                catch
                {
                }
            };

            border.PointerExited += (sender, e) =>
            {
                try
                {
                    Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
                }
                catch
                {
                }
            };

            return border;
        }

        private FrameworkElement CreateVideoSegment(VideoSegment videoSegment)
        {
            try
            {
                var videoContainer = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 60, 65, 71)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(2),
                    Width = 200,
                    Height = 120
                };

                var videoContent = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 視頻圖標
                var videoIcon = new TextBlock
                {
                    Text = "🎬",
                    FontSize = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = new SolidColorBrush(Colors.White)
                };

                // 視頻文字
                var videoText = new TextBlock
                {
                    Text = "點擊播放視頻",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Colors.LightGray)
                };

                // 播放按鈕
                var playButton = new Button
                {
                    Content = "▶ 播放",
                    Background = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 6, 12, 6),
                    FontSize = 12,
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // 點擊事件處理
                playButton.Click += (sender, e) =>
                {
                    try
                    {
                        var videoUrl = videoSegment.Url;
                        if (string.IsNullOrEmpty(videoUrl))
                            videoUrl = videoSegment.File;

                        if (!string.IsNullOrEmpty(videoUrl))
                        {
                            Debug.WriteLine($"MessageSegmentControl: 請求播放視頻 - URL: {videoUrl}");

                            // 觸發視頻播放事件
                            VideoPlayRequested?.Invoke(this, new VideoPlayEventArgs(videoUrl, "視頻播放"));
                        }
                        else
                        {
                            Debug.WriteLine("MessageSegmentControl: 視頻URL為空，無法播放");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"MessageSegmentControl: 處理視頻播放點擊時發生錯誤: {ex.Message}");
                    }
                };

                videoContent.Children.Add(videoIcon);
                videoContent.Children.Add(videoText);
                videoContent.Children.Add(playButton);
                videoContainer.Child = videoContent;

                Debug.WriteLine($"MessageSegmentControl: 成功創建視頻段UI - URL: {videoSegment.Url ?? videoSegment.File}");
                return videoContainer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MessageSegmentControl: 創建視頻段UI時發生錯誤: {ex.Message}");
                return CreateDefaultSegment(videoSegment);
            }
        }

        private Border CreateFileSegment(FileSegment segment)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 152, 0)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 4, 0, 4),
                MinWidth = 150
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var icon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Glyph = "\uE160",
                FontSize = 16,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 8, 0)
            };

            var textBlock = new TextBlock
            {
                Text = "📎 文件",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(textBlock);
            border.Child = stackPanel;

            return border;
        }

        private Border CreateReplySegment(ReplySegment segment)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 96, 125, 139)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var textBlock = new TextBlock
            {
                Text = segment?.GetReplyContent() ?? "💬 回覆",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = textBlock;
            return border;
        }

        private Border CreateDefaultSegment(MessageSegment segment)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 96, 125, 139)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var textBlock = new TextBlock
            {
                Text = $"❓ [{segment.Type}]",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14
            };

            border.Child = textBlock;
            return border;
        }

        private TextBlock CreateImagePlaceholder(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Colors.LightGray),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        #endregion
    }
}