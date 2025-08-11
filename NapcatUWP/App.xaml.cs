using NapcatUWP.Tools;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace NapcatUWP
{
    /// <summary>
    ///     提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        ///     初始化单一实例应用程序对象。这是执行的创作代码的第一行，
        ///     已执行，逻辑上等同于 main() 或 WinMain()。
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        /// <summary>
        ///     在应用程序由最终用户正常启动时进行调用。
        ///     将在启动应用程序以打开特定文件等情况下使用。
        /// </summary>
        /// <param name="e">有关启动请求和过程的详细信息。</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            var rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;

                // 添加全局异常处理
                this.UnhandledException += App_UnhandledException;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    try
                    {
                        // 清理可能损坏的缓存
                        await CleanupCorruptedCacheAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"清理缓存时发生错误: {ex.Message}");
                    }
                }

                // 安全初始化头像管理器
                _ = InitializeAvatarManagerSafeAsync();

                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// 全局异常处理器 - 防止应用崩溃
        /// </summary>
        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"未处理异常: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"异常类型: {e.Exception.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"堆栈: {e.Exception.StackTrace}");

            // 标记异常已处理，防止应用崩溃
            e.Handled = true;

            // 对于集合访问异常，尝试恢复
            if (e.Exception is ArgumentOutOfRangeException ||
                e.Exception is InvalidOperationException ||
                e.Exception.Message.Contains("Collection was modified"))
            {
                System.Diagnostics.Debug.WriteLine("检测到集合操作异常，尝试恢复应用状态");

                // 可以在这里添加恢复逻辑
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    // 重新初始化关键组件
                });
            }
        }

        private async Task CleanupCorruptedCacheAsync()
        {
            try
            {
                await AvatarManager.CleanExpiredCacheAsync(0); // 清理所有过期缓存
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理缓存失败: {ex.Message}");
            }
        }

        private async Task InitializeAvatarManagerSafeAsync()
        {
            try
            {
                await AvatarManager.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("头像管理器初始化成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"头像管理器初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     导航到特定页失败时调用
        /// </summary>
        /// <param name="sender">导航失败的框架</param>
        /// <param name="e">有关导航失败的详细信息</param>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        ///     在将要挂起应用程序执行时调用。  在不知道应用程序
        ///     无需知道应用程序会被终止还是会恢复，
        ///     并让内存内容保持不变。
        /// </summary>
        /// <param name="sender">挂起的请求的源。</param>
        /// <param name="e">有关挂起请求的详细信息。</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            try
            {
                // 清理过期的头像缓存
                await AvatarManager.CleanExpiredCacheAsync(30);
                System.Diagnostics.Debug.WriteLine("应用挂起时清理头像缓存完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理头像缓存时发生错误: {ex.Message}");
            }

            //TODO: 保存应用程序状态并停止任何后台活动
            deferral.Complete();
        }
    }
}