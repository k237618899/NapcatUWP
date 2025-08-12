using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NapcatUWP.Controls;
using NapcatUWP.Pages;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace NapcatUWP
{
    /// <summary>
    ///     可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public static WebSocketClientStarter SocketClientStarter = new WebSocketClientStarter();
        private NameValueCollection _settingsCollection = new NameValueCollection();
        public string ConnectionAddr = "http://140.83.32.184:3000";

        public MainPage()
        {
            InitializeComponent();
            InitializeDB();
        }

        private void UpdatePageAndSetting()
        {
            _settingsCollection = DataAccess.GetAllDatas();
            if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
            {
                // 確保 ConnectionAddr 不會被設為 null
                var serverSetting = _settingsCollection.Get("Server");
                ConnectionAddr = !string.IsNullOrEmpty(serverSetting) ? serverSetting : "http://140.83.32.184:3000";

                TextBoxAccount.Text = _settingsCollection.Get("Account") ?? "";
                PasswordBoxToken.Password = _settingsCollection.Get("Token") ?? "";
            }
        }

        private void InitializeDB()
        {
            var asyncAction = ThreadPool.RunAsync(workItem =>
            {
                try
                {
                    DataAccess.InitializeDatabase();
                    DataAccess.InitInsert();
                    // 在應用初始化時升級數據庫結構
                    DataAccess.UpgradeChatListCacheTable();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"數據庫初始化失敗: {ex.Message}");
                }
            });
        }

        /// <summary>
        ///     Handles the Click event of the SettingBars control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private async void SettingBars_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var dialog = new ContentDialog
            {
                Title = "NapCat Settings",
                RequestedTheme = ElementTheme.Dark,
                //FullSizeDesired = true,
                MaxWidth = ActualWidth // Required for Mobile!
            };
            var panel = new StackPanel();
            var ipAddr = new TextBox
            {
                Header = "HTTP Server Address"
            };
            var isWsCheck = new CheckBox
            {
                IsChecked = true,
                Content = "WebSocket"
            };
            isWsCheck.Checked += delegate
            {
                if (isWsCheck.IsChecked == true) ipAddr.Text = ipAddr.Text.Replace("http", "ws");
            };
            if (!string.IsNullOrEmpty(ConnectionAddr)) ipAddr.Text = ConnectionAddr;


            panel.Children.Add(ipAddr);
            panel.Children.Add(isWsCheck);
            dialog.Content = panel;
            // Add Buttons
            dialog.PrimaryButtonText = "Save";
            dialog.PrimaryButtonClick += delegate
            {
                var isURL = AddressCheck(ipAddr.Text);
                if (isURL)
                {
                    if (isWsCheck.IsChecked == true) ipAddr.Text = ipAddr.Text.Replace("http", "ws");
                    ConnectionAddr = ipAddr.Text;
                    DataAccess.UpdateSetting("Server", ConnectionAddr);
                    btn.Content = "Setting Saved";
                }
                else
                {
                    new MessageDialog("Not a valid URL!", "Warning").ShowAsync();
                }
            };

            dialog.SecondaryButtonText = "Cancel";
            dialog.SecondaryButtonClick += delegate { btn.Content = "Cancel"; };

            // Show Dialog
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
                btn.Content = "Result: NONE";
            else
                btn.Content = "Settings";
        }

        /// <summary>
        ///     Addresses the check.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>isURL</returns>
        private bool AddressCheck(string text)
        {
            var isUri = Uri.IsWellFormedUriString(text, UriKind.Absolute);
            return isUri;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePageAndSetting();
        }

        private void ButtonLogin_Click(object sender, RoutedEventArgs e)
        {
            Progress_R.IsActive = true;
            WebSocketStart();
        }

        private async Task WebSocketStart()
        {
            try
            {
                DataAccess.UpdateSetting("Account", TextBoxAccount.Text);
                DataAccess.UpdateSetting("Token", PasswordBoxToken.Password);

                // 添加連線前的檢查
                Debug.WriteLine($"嘗試連線到: {ConnectionAddr}");
                Debug.WriteLine(
                    $"使用 Token: {PasswordBoxToken.Password.Substring(0, Math.Min(10, PasswordBoxToken.Password.Length))}...");

                SocketClientStarter.WebSocketConnet(ConnectionAddr, PasswordBoxToken.Password);

                // 改善等待邏輯
                for (var i = 0; i < 30; i++)
                {
                    await Task.Delay(1000);
                    if (SocketClientStarter.IsConnected)
                    {
                        Progress_R.IsActive = false;
                        Frame.Navigate(typeof(MainView));
                        return;
                    }

                    // 每5秒檢查一次連線狀態
                    if (i % 5 == 0) Debug.WriteLine($"連線等待中... {i}/30 秒");
                }

                // 連接失敗處理
                Progress_R.IsActive = false;
                await ShowConnectionFailedDialog();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebSocket 連線發生異常: {ex.Message}");
                Progress_R.IsActive = false;
                await ShowConnectionFailedDialog();
            }
        }

        private async Task ShowConnectionFailedDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "連線失敗",
                RequestedTheme = ElementTheme.Dark,
                MaxWidth = ActualWidth
            };

            var panel = new StackPanel();
            var textBlock = new TextBlock
            {
                Text = $"無法連線到伺服器 {ConnectionAddr}\n\n可能的原因：\n" +
                       "• 伺服器未運行或地址不正確\n" +
                       "• Token 無效\n" +
                       "• 防火牆或網路設定問題\n" +
                       "• 伺服器拒絕連線",
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(textBlock);
            dialog.Content = panel;
            dialog.PrimaryButtonText = "確定";

            await dialog.ShowAsync();
        }
    }
}