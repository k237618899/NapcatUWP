using System;
using System.Collections.Specialized;
using Windows.Foundation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NapcatUWP.Controls;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace NapcatUWP
{
    /// <summary>
    ///     可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public string ConnectionAddr = "http://140.83.32.184:3000";
        private NameValueCollection _settingsCollection = new NameValueCollection();

        public MainPage()
        {
            InitializeComponent();
            InitializeDB();
            UpdatePageAndSetting();
        }

        private void UpdatePageAndSetting()
        {
            ConnectionAddr = _settingsCollection.Get("Server");
            TextBoxAccount.Text = _settingsCollection.Get("Account");
            PasswordBoxToken.Password = _settingsCollection.Get("Token");
        }
        private void InitializeDB()
        {
            IAsyncAction asyncAction = Windows.System.Threading.ThreadPool.RunAsync((workItem) =>
            {
                DataAccess.InitializeDatabase();
                DataAccess.InitInsert();
                _settingsCollection = DataAccess.GetAllDatas();
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
            if (ConnectionAddr != string.Empty) ipAddr.Text = ConnectionAddr;


            panel.Children.Add(ipAddr);
            dialog.Content = panel;
            // Add Buttons
            dialog.PrimaryButtonText = "Save";
            dialog.PrimaryButtonClick += delegate
            {
                var isURL = AddressCheck(ipAddr.Text);
                if (isURL)
                {
                    ConnectionAddr = ipAddr.Text;
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
    }
}