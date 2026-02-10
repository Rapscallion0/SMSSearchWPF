using System.Windows;
using System.Windows.Controls;
using SMS_Search.ViewModels.Settings;

namespace SMS_Search.Views.Settings
{
    public partial class ConnectionSectionView : System.Windows.Controls.UserControl
    {
        public ConnectionSectionView()
        {
            InitializeComponent();
        }

        private void SqlPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb && DataContext is ConnectionSectionViewModel vm)
            {
                vm.Password = pb.Password;
            }
        }
    }
}
