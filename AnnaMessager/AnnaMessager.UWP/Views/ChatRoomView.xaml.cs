using System.Diagnostics;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    // 已廢棄: 保留空類防止舊導航引用編譯錯誤。請改用 ChatView。
    public sealed partial class ChatRoomView : MvxWindowsPage
    {
        public ChatRoomView()
        {
            Debug.WriteLine("[ChatRoomView] Deprecated view instantiated. Replace navigation with ChatView.");
        }
    }
}
