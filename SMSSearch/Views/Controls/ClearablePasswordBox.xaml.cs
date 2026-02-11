using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SMS_Search.Views.Controls
{
    public partial class ClearablePasswordBox : System.Windows.Controls.UserControl
    {
        private bool _isUpdating;

        public ClearablePasswordBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register("Password", typeof(string), typeof(ClearablePasswordBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordPropertyChanged));

        public string Password
        {
            get { return (string)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }

        private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ClearablePasswordBox)d;
            if (control._isUpdating) return;

            control._isUpdating = true;
            control.InputBox.Password = (string)e.NewValue;
            control._isUpdating = false;
        }

        private void InputBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            Password = InputBox.Password;
            _isUpdating = false;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputBox.Password = string.Empty;
            InputBox.Focus();
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
                             (sender is DependencyObject d ? VisualTreeHelper.GetParent(d) as UIElement : null);
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
