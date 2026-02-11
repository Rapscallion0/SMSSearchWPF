using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((sender as System.Windows.Controls.Control)?.Parent as UIElement) ??
                             (sender is DependencyObject d ? System.Windows.Media.VisualTreeHelper.GetParent(d) as UIElement : null);
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
