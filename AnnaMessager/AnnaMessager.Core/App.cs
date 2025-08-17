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
            // OneBotService 延由平台 Setup 顯式註冊，這裡僅保底
            if (!Mvx.CanResolve<IOneBotService>())
                Mvx.RegisterSingleton<IOneBotService>(new OneBotService());

            // 不再使用 CreatableTypes().EndingWith("Service") 自動掃描，避免覆蓋 UWP 平台專用服務
            RegisterAppStart<LoginViewModel>();
        }
    }
}