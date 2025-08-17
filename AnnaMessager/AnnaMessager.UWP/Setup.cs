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
            // 註冊數據庫服務（最優先）
            Mvx.RegisterSingleton<IPlatformDatabaseService>(new UwpDatabaseService());

            // 先註冊平台設定服務 (供跨平台緩存 / 其他服務解析)
            Mvx.RegisterSingleton<IPlatformSettingsService>(new UwpPlatformSettingsService());

            // 註冊核心業務服務
            Mvx.RegisterSingleton<ISettingsService>(new UwpSettingsService());
            Mvx.RegisterSingleton<ICacheManager>(new UwpCacheManager());
            Mvx.RegisterSingleton<IOneBotService>(new OneBotService());

            // 其餘平台服務
            Mvx.RegisterType<IWebSocketClient, UwpWebSocketClient>();
            Mvx.RegisterType<INotificationService, UwpNotificationService>();
            Mvx.RegisterType<IUserInteractionService, UwpUserInteractionService>();

            // 註冊頭像快取服務
            Mvx.RegisterSingleton<IAvatarCacheService>(new UwpAvatarCacheService());

            base.InitializeFirstChance();
        }

        protected override void InitializeLastChance()
        {
            // 最後再次強制覆蓋（確保未被其它掃描出的服務替換）
            if (!Mvx.CanResolve<IPlatformDatabaseService>())
                Mvx.RegisterSingleton<IPlatformDatabaseService>(new UwpDatabaseService());
            if (!Mvx.CanResolve<IPlatformSettingsService>())
                Mvx.RegisterSingleton<IPlatformSettingsService>(new UwpPlatformSettingsService());
            if (!Mvx.CanResolve<ISettingsService>())
                Mvx.RegisterSingleton<ISettingsService>(new UwpSettingsService());
            if (!Mvx.CanResolve<ICacheManager>())
                Mvx.RegisterSingleton<ICacheManager>(new UwpCacheManager());
            if (!Mvx.CanResolve<IOneBotService>())
                Mvx.RegisterSingleton<IOneBotService>(new OneBotService());
            if (!Mvx.CanResolve<IAvatarCacheService>())
                Mvx.RegisterSingleton<IAvatarCacheService>(new UwpAvatarCacheService());
            base.InitializeLastChance();
        }
    }
}