using Windows.UI.Xaml;
using AnnaMessager.Core.ViewModels;
using MvvmCross.Uwp.Views;

namespace AnnaMessager.UWP.Views
{
    public sealed partial class LoginView : MvxWindowsPage
    {
        public LoginView()
        {
            InitializeComponent();
            Loaded += LoginView_Loaded;
        }

        public new LoginViewModel ViewModel => (LoginViewModel)DataContext;

        private void LoginView_Loaded(object sender, RoutedEventArgs e)
        {
            // 頁面載入時讓賬號輸入框獲得焦點
            AccountTextBox.Focus(FocusState.Programmatic);
        }
    }
}