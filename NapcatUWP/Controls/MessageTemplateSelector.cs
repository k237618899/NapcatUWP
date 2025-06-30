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
                // ���M���Д�߉݋���z���Ƿ��з��ı�����Ϣ��
                if (message.Segments != null && message.Segments.Count > 0)
                {
                    // �z���Ƿ�������ı���
                    foreach (var segment in message.Segments)
                        if (segment.Type != "text")
                        {
                            Debug.WriteLine($"ʹ�ø���Ϣģ�壬�z�y�� {segment.Type} ��");
                            return RichMessageTemplate ?? SimpleMessageTemplate;
                        }

                    // ���ֻ���ı��Σ����Δ����^1��Ҳʹ�ø���Ϣģ�壨�����ж����ı�Ƭ�Σ�
                    if (message.Segments.Count > 1)
                    {
                        Debug.WriteLine($"ʹ�ø���Ϣģ�壬�z�y�� {message.Segments.Count} ����");
                        return RichMessageTemplate ?? SimpleMessageTemplate;
                    }
                }

                Debug.WriteLine("ʹ�ú�����Ϣģ��");
            }

            return SimpleMessageTemplate;
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}