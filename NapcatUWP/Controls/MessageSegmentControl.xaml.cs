using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using NapcatUWP.Models;

namespace NapcatUWP.Controls
{
    /// <summary>
    /// 视频播放事件参数类
    /// </summary>
    public class VideoPlayEventArgs : EventArgs
    {
        public string VideoUrl { get; }
        public string Title { get; }

        public VideoPlayEventArgs(string videoUrl, string title = "视频播放")
        {
            VideoUrl = videoUrl;
            Title = title;
        }
    }

    public sealed partial class MessageSegmentControl : UserControl
    {
        public static readonly DependencyProperty SegmentsProperty =
            DependencyProperty.Register(nameof(Segments), typeof(IList<MessageSegment>), typeof(MessageSegmentControl),
                new PropertyMetadata(null, OnSegmentsChanged));

        // 视频播放事件
        public event EventHandler<VideoPlayEventArgs> VideoPlayRequested;

        public MessageSegmentControl()
        {
            InitializeComponent();
        }

        public IList<MessageSegment> Segments
        {
            get => (IList<MessageSegment>)GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, value);
        }

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
            // 复制数据
            foreach (var kvp in segment.Data)
            {
                imageSegment.Data[kvp.Key] = kvp.Value;
            }

            return imageSegment;
        }

        private AtSegment CreateAtSegmentFromData(MessageSegment segment)
        {
            var atSegment = new AtSegment();
            foreach (var kvp in segment.Data)
            {
                atSegment.Data[kvp.Key] = kvp.Value;
            }

            return atSegment;
        }

        private FaceSegment CreateFaceSegmentFromData(MessageSegment segment)
        {
            var faceSegment = new FaceSegment();
            foreach (var kvp in segment.Data)
            {
                faceSegment.Data[kvp.Key] = kvp.Value;
            }

            return faceSegment;
        }

        private RecordSegment CreateRecordSegmentFromData(MessageSegment segment)
        {
            var recordSegment = new RecordSegment();
            foreach (var kvp in segment.Data)
            {
                recordSegment.Data[kvp.Key] = kvp.Value;
            }

            return recordSegment;
        }

        private VideoSegment CreateVideoSegmentFromData(MessageSegment segment)
        {
            var videoSegment = new VideoSegment();
            foreach (var kvp in segment.Data)
            {
                videoSegment.Data[kvp.Key] = kvp.Value;
            }

            return videoSegment;
        }

        private FileSegment CreateFileSegmentFromData(MessageSegment segment)
        {
            var fileSegment = new FileSegment();
            foreach (var kvp in segment.Data)
            {
                fileSegment.Data[kvp.Key] = kvp.Value;
            }

            return fileSegment;
        }

        private ReplySegment CreateReplySegmentFromData(MessageSegment segment)
        {
            var replySegment = new ReplySegment();
            foreach (var kvp in segment.Data)
            {
                replySegment.Data[kvp.Key] = kvp.Value;
            }

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

            // 设置图片源
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
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 4, 0, 4),
                MinWidth = 120
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
                Text = segment?.Magic == true ? "變聲語音" : "語音消息",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(textBlock);
            border.Child = stackPanel;

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

                // 视频图标
                var videoIcon = new TextBlock
                {
                    Text = "🎬",
                    FontSize = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = new SolidColorBrush(Colors.White)
                };

                // 视频文本
                var videoText = new TextBlock
                {
                    Text = "点击播放视频",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Colors.LightGray)
                };

                // 播放按钮
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

                // 点击事件处理
                playButton.Click += (sender, e) =>
                {
                    try
                    {
                        var videoUrl = videoSegment.Url;
                        if (string.IsNullOrEmpty(videoUrl))
                            videoUrl = videoSegment.File;

                        if (!string.IsNullOrEmpty(videoUrl))
                        {
                            Debug.WriteLine($"MessageSegmentControl: 请求播放视频 - URL: {videoUrl}");

                            // 触发视频播放事件
                            VideoPlayRequested?.Invoke(this, new VideoPlayEventArgs(videoUrl, "视频播放"));
                        }
                        else
                        {
                            Debug.WriteLine("MessageSegmentControl: 视频URL为空，无法播放");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"MessageSegmentControl: 处理视频播放点击时发生错误: {ex.Message}");
                    }
                };

                videoContent.Children.Add(videoIcon);
                videoContent.Children.Add(videoText);
                videoContent.Children.Add(playButton);
                videoContainer.Child = videoContent;

                Debug.WriteLine($"MessageSegmentControl: 成功创建视频段UI - URL: {videoSegment.Url ?? videoSegment.File}");
                return videoContainer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MessageSegmentControl: 创建视频段UI时发生错误: {ex.Message}");
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