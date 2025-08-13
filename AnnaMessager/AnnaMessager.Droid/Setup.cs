using Android.Content;
using AnnaMessager.Core;
using AnnaMessager.Core.Services;
using AnnaMessager.Core.WebSocket;
using AnnaMessager.Droid.Services;
using AnnaMessager.Droid.WebSocket;
using MvvmCross.Core.ViewModels;
using MvvmCross.Droid.Platform;
using MvvmCross.Platform;

namespace AnnaMessager.Droid
{
    public class Setup : MvxAndroidSetup
    {
        public Setup(Context applicationContext) : base(applicationContext)
        {
        }

        protected override IMvxApplication CreateApp()
        {
            return new App();
        }

        protected override void InitializeFirstChance()
        {
            // 註冊 Android 特定的服務
            Mvx.RegisterType<IWebSocketClient, AndroidWebSocketClient>();
            Mvx.RegisterType<IPlatformSettingsService, AndroidSettingsService>();
            Mvx.RegisterType<INotificationService, AndroidNotificationService>();

            base.InitializeFirstChance();
        }
    }
}