using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using NapcatUWP.Controls;
using NapcatUWP.Pages;
using NapcatUWP.Tools;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace NapcatUWP
{
    /// <summary>
    ///     可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private NameValueCollection _settingsCollection = new NameValueCollection();
        public string ConnectionAddr = "http://140.83.32.184:3000";
        public static WebSocketClientStarter SocketClientStarter = new WebSocketClientStarter();

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
                ConnectionAddr = _settingsCollection.Get("Server");
                TextBoxAccount.Text = _settingsCollection.Get("Account") ?? "";
                PasswordBoxToken.Password = _settingsCollection.Get("Token") ?? "";
            }
        }

        private void InitializeDB()
        {
            var asyncAction = ThreadPool.RunAsync(workItem =>
            {
                DataAccess.InitializeDatabase();
                DataAccess.InitInsert();
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
            if (ConnectionAddr != string.Empty) ipAddr.Text = ConnectionAddr;


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
            DataAccess.UpdateSetting("Account", TextBoxAccount.Text);
            DataAccess.UpdateSetting("Token", PasswordBoxToken.Password);
            SocketClientStarter.WebSocketConnet(ConnectionAddr, PasswordBoxToken.Password);
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1000);
                if (SocketClientStarter.IsConnected)
                {
                    Frame.Navigate(typeof(MainView));
                    return;
                }
            }
            var dialog = new ContentDialog
            {
                Title = "Connected Failed",
                RequestedTheme = ElementTheme.Dark,
                //FullSizeDesired = true,
                MaxWidth = ActualWidth // Required for Mobile!
            };
            var panel = new StackPanel();
            
            var textBlock = new TextBlock
            {
                Text = "Connect to Server "+ConnectionAddr+" failed! Please check the connection and Token!"
            };
            panel.Children.Add(textBlock);
            dialog.Content = panel;
            // Add Buttons
            dialog.PrimaryButtonText = "OK";
            Progress_R.IsActive = false;
            await dialog.ShowAsync();
        }
    }
}