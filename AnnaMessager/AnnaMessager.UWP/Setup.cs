using Windows.UI.Xaml.Controls;
using AnnaMessager.Core.Services;
using AnnaMessager.Core.WebSocket;
using AnnaMessager.UWP.Services;
using AnnaMessager.UWP.WebSocket;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;
using MvvmCross.Uwp.Platform;

namespace AnnaMessager.UWP
{
    public class Setup : MvxWindowsSetup
    {
        public Setup(Frame rootFrame) : base(rootFrame)
        {
        }

        protected override IMvxApplication CreateApp()
        {
            return new Core.App();
        }

        protected override void InitializeFirstChance()
        {
            // 註冊 UWP 特定的服務
            Mvx.RegisterType<IWebSocketClient, UwpWebSocketClient>();
            Mvx.RegisterType<IPlatformSettingsService, UwpSettingsService>();
            Mvx.RegisterType<INotificationService, UwpNotificationService>();
            Mvx.RegisterType<IUserInteractionService, UwpUserInteractionService>();

            base.InitializeFirstChance();
        }
    }
}