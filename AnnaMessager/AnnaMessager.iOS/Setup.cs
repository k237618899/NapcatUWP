using AnnaMessager.Core;
using AnnaMessager.Core.WebSocket;
using AnnaMessager.iOS.WebSocket;
using MvvmCross.Core.ViewModels;
using MvvmCross.iOS.Platform;
using MvvmCross.Platform;
using UIKit;

namespace AnnaMessager.iOS
{
    public class Setup : MvxIosSetup
    {
        public Setup(IMvxApplicationDelegate applicationDelegate, UIWindow window)
            : base(applicationDelegate, window)
        {
        }

        protected override IMvxApplication CreateApp()
        {
            return new App();
        }

        protected override void InitializeFirstChance()
        {
            // 註冊 iOS 特定的 WebSocket 實作
            Mvx.RegisterType<IWebSocketClient, iOSWebSocketClient>();

            base.InitializeFirstChance();
        }
    }
}