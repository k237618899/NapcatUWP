using System;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NapcatUWP.Controls;
using NapcatUWP.Tools;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace NapcatUWP
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainHubView : Page
    {

        public MainHubView()
        {
            InitializeComponent();
            InitializeAvatorAndInfo();
        }

        private void InitializeAvatorAndInfo()
        {
            MainPage.SocketClientStarter._socket.Send(JSONTools.ActionToJSON("get_login_info",new JsonObject(),"login_info"));
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        public void SetInfoSafe(double id,string name)
        {
            SetInfo( id, name);
        }

        private void SetInfo(double id, string name)
        {
            
        }
    }
}