using AnnaMessager.Core.Services;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;
using MvvmCross.Platform.IoC;

namespace AnnaMessager.Core
{
    public class App : MvxApplication
    {
        public override void Initialize()
        {
            // 註冊核心服務
            Mvx.RegisterSingleton<IOneBotService>(() => new OneBotService());
            Mvx.RegisterSingleton<ISettingsService>(() => new CrossPlatformSettingsService());
            Mvx.RegisterSingleton<ICacheManager>(() => new CrossPlatformCacheManager());
            Mvx.RegisterSingleton<ISearchService>(() => new InMemorySearchService());

            // 注意：INotificationService 和 IUserInteractionService 在各平台的 Setup.cs 中註冊
            // 因為需要平台特定的實現

            CreatableTypes()
                .EndingWith("Service")
                .AsInterfaces()
                .RegisterAsLazySingleton();

            RegisterAppStart<ChatListViewModel>();
        }
    }
}