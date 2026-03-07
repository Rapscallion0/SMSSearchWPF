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

        public static readonly DependencyProperty ShowEyeIconProperty =
            DependencyProperty.Register("ShowEyeIcon", typeof(bool), typeof(ClearablePasswordBox), new PropertyMetadata(false));

        public bool ShowEyeIcon
        {
            get { return (bool)GetValue(ShowEyeIconProperty); }
            set { SetValue(ShowEyeIconProperty, value); }
        }

        private bool _isPasswordVisible = false;

        private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ClearablePasswordBox)d;
            if (control._isUpdating) return;

            control._isUpdating = true;
            string newPassword = (string)e.NewValue ?? string.Empty;
            control.InputBox.Password = newPassword;
            control.VisibleInputBox.Text = newPassword;
            control._isUpdating = false;
        }

        private void InputBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            Password = InputBox.Password;
            VisibleInputBox.Text = InputBox.Password;
            _isUpdating = false;
        }

        private void VisibleInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            Password = VisibleInputBox.Text;
            InputBox.Password = VisibleInputBox.Text;
            _isUpdating = false;
        }

        private void InputBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.PasswordBox pb)
            {
                pb.SelectAll();
            }
            else if (sender is System.Windows.Controls.TextBox tb)
            {
                tb.SelectAll();
            }
        }

        private void InputBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.PasswordBox pb && !pb.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                pb.Focus();
            }
            else if (sender is System.Windows.Controls.TextBox tb && !tb.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                tb.Focus();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputBox.Password = string.Empty;
            VisibleInputBox.Text = string.Empty;
            if (_isPasswordVisible)
                VisibleInputBox.Focus();
            else
                InputBox.Focus();
        }

        private void EyeButton_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                InputBox.Visibility = Visibility.Collapsed;
                VisibleInputBox.Visibility = Visibility.Visible;
                EyeIcon.Template = (ControlTemplate)FindResource("Icon_EyeSlash");
                VisibleInputBox.Focus();
                VisibleInputBox.CaretIndex = VisibleInputBox.Text.Length;
            }
            else
            {
                VisibleInputBox.Visibility = Visibility.Collapsed;
                InputBox.Visibility = Visibility.Visible;
                EyeIcon.Template = (ControlTemplate)FindResource("Icon_Eye");
                InputBox.Focus();
                InputBox.GetType().GetMethod("Select", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    ?.Invoke(InputBox, new object[] { InputBox.Password.Length, 0 });
            }
        }

        public new bool Focus()
        {
            return _isPasswordVisible ? VisibleInputBox.Focus() : InputBox.Focus();
        }

        public void SelectAll()
        {
            if (_isPasswordVisible)
            {
                VisibleInputBox.SelectAll();
            }
            else
            {
                InputBox.SelectAll();
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
                             (sender is DependencyObject d ? VisualTreeHelper.GetParent(d) as UIElement : null);
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
