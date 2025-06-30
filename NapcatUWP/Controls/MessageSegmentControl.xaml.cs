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
    public sealed partial class MessageSegmentControl : UserControl
    {
        public static readonly DependencyProperty SegmentsProperty =
            DependencyProperty.Register(nameof(Segments), typeof(IList<MessageSegment>), typeof(MessageSegmentControl),
                new PropertyMetadata(null, OnSegmentsChanged));

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

        // 在 CreateSegmentElement 方法中添加更好的類型檢查
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

        // 添加輔助方法來從通用 MessageSegment 創建具體類型
        private ImageSegment CreateImageSegmentFromData(MessageSegment segment)
        {
            var imageSegment = new ImageSegment();
            imageSegment.Data = segment.Data;
            return imageSegment;
        }

        private AtSegment CreateAtSegmentFromData(MessageSegment segment)
        {
            var atSegment = new AtSegment();
            atSegment.Data = segment.Data;
            return atSegment;
        }

        private FaceSegment CreateFaceSegmentFromData(MessageSegment segment)
        {
            var faceSegment = new FaceSegment();
            faceSegment.Data = segment.Data;
            return faceSegment;
        }

        private RecordSegment CreateRecordSegmentFromData(MessageSegment segment)
        {
            var recordSegment = new RecordSegment();
            recordSegment.Data = segment.Data;
            return recordSegment;
        }

        private VideoSegment CreateVideoSegmentFromData(MessageSegment segment)
        {
            var videoSegment = new VideoSegment();
            videoSegment.Data = segment.Data;
            return videoSegment;
        }

        private FileSegment CreateFileSegmentFromData(MessageSegment segment)
        {
            var fileSegment = new FileSegment();
            fileSegment.Data = segment.Data;
            return fileSegment;
        }

        private ReplySegment CreateReplySegmentFromData(MessageSegment segment)
        {
            var replySegment = new ReplySegment();
            replySegment.Data = segment.Data;
            return replySegment;
        }

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
                    // 圖片加載失敗時顯示占位符
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
                // 使用 DisplayText 屬性來顯示成員名稱
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
                Glyph = segment?.Magic == true ? "\uE1D6" : "\uE189", // 魔法語音或普通語音圖標
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

        private Border CreateVideoSegment(VideoSegment segment)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 65, 71)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 4, 0, 4),
                MinWidth = 200,
                MinHeight = 120,
                MaxWidth = 250,
                MaxHeight = 200
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var icon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Glyph = "\uE102", // 播放圖標
                FontSize = 32,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var textBlock = new TextBlock
            {
                Text = "🎬 視頻",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(textBlock);
            border.Child = stackPanel;

            return border;
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
                Glyph = "\uE160", // 文件圖標
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
                Text = segment?.GetReplyContent() ?? "💬 回覆", // 使用新的 GetReplyContent 方法
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
    }
}