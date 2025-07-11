using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NapcatUWP.Models;

namespace NapcatUWP.Controls
{
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate RichMessageTemplate { get; set; }
        public DataTemplate SimpleMessageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is ChatMessage message)
            {
                // 改進的判斷邏輯：檢查是否有非文本的消息段
                if (message.Segments != null && message.Segments.Count > 0)
                {
                    // 檢查是否包含非文本段
                    foreach (var segment in message.Segments)
                        if (segment.Type != "text")
                        {
                            Debug.WriteLine($"使用富消息模板，檢測到 {segment.Type} 段");
                            return RichMessageTemplate ?? SimpleMessageTemplate;
                        }

                    // 如果只有文本段，但段數超過1，也使用富消息模板（可能有多個文本片段）
                    if (message.Segments.Count > 1)
                    {
                        Debug.WriteLine($"使用富消息模板，檢測到 {message.Segments.Count} 個段");
                        return RichMessageTemplate ?? SimpleMessageTemplate;
                    }
                }

                Debug.WriteLine("使用簡單消息模板");
            }

            return SimpleMessageTemplate;
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}