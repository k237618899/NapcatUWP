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
            // 註冊核心服務 - 使用簡單的實現避免依賴問題
            Mvx.LazyConstructAndRegisterSingleton<IOneBotService, OneBotService>();

            // 使用我們的簡單實現
            Mvx.RegisterSingleton<ISettingsService>(() => new SimpleSettingsService());
            Mvx.RegisterSingleton<ICacheManager>(() => new SimpleCacheManager());

            CreatableTypes()
                .EndingWith("Service")
                .AsInterfaces()
                .RegisterAsLazySingleton();

            // 從登入頁面開始，避免複雜依賴
            RegisterAppStart<LoginViewModel>();
        }
    }
}