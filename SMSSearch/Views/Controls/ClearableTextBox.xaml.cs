using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SMS_Search.Views.Controls
{
    public partial class ClearableTextBox : System.Windows.Controls.UserControl
    {
        public ClearableTextBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(ClearableTextBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(ClearableTextBox), new PropertyMetadata(false));

        public bool IsReadOnly
        {
            get { return (bool)GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.Register("MaxLength", typeof(int), typeof(ClearableTextBox), new PropertyMetadata(0));

        public int MaxLength
        {
            get { return (int)GetValue(MaxLengthProperty); }
            set { SetValue(MaxLengthProperty, value); }
        }

        public static readonly DependencyProperty ClearCommandProperty =
            DependencyProperty.Register("ClearCommand", typeof(ICommand), typeof(ClearableTextBox), new PropertyMetadata(null));

        public ICommand ClearCommand
        {
            get { return (ICommand)GetValue(ClearCommandProperty); }
            set { SetValue(ClearCommandProperty, value); }
        }

        public static readonly DependencyProperty ClearCommandParameterProperty =
            DependencyProperty.Register("ClearCommandParameter", typeof(object), typeof(ClearableTextBox), new PropertyMetadata(null));

        public object ClearCommandParameter
        {
            get { return GetValue(ClearCommandParameterProperty); }
            set { SetValue(ClearCommandParameterProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register("Placeholder", typeof(string), typeof(ClearableTextBox), new PropertyMetadata(string.Empty));

        public string Placeholder
        {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        public static readonly DependencyProperty PrefixTextProperty =
            DependencyProperty.Register("PrefixText", typeof(string), typeof(ClearableTextBox), new PropertyMetadata(string.Empty, OnPrefixTextChanged));

        public string PrefixText
        {
            get { return (string)GetValue(PrefixTextProperty); }
            set { SetValue(PrefixTextProperty, value); }
        }

        public static readonly DependencyProperty TextBoxPaddingProperty =
            DependencyProperty.Register("TextBoxPadding", typeof(Thickness), typeof(ClearableTextBox), new PropertyMetadata(new Thickness(2, 0, 20, 0)));

        public Thickness TextBoxPadding
        {
            get { return (Thickness)GetValue(TextBoxPaddingProperty); }
            set { SetValue(TextBoxPaddingProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderMarginProperty =
            DependencyProperty.Register("PlaceholderMargin", typeof(Thickness), typeof(ClearableTextBox), new PropertyMetadata(new Thickness(4, 0, 20, 0)));

        public Thickness PlaceholderMargin
        {
            get { return (Thickness)GetValue(PlaceholderMarginProperty); }
            set { SetValue(PlaceholderMarginProperty, value); }
        }

        private static void OnPrefixTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClearableTextBox ctb)
            {
                if (!string.IsNullOrEmpty(ctb.PrefixText))
                {
                    // Assuming character width of ~7 pixels for Prefix text plus 6 padding
                    double offset = ctb.PrefixText.Length * 7 + 6;
                    ctb.TextBoxPadding = new Thickness(offset, 0, 20, 0);
                    ctb.PlaceholderMargin = new Thickness(offset + 2, 0, 20, 0);
                }
                else
                {
                    ctb.TextBoxPadding = new Thickness(2, 0, 20, 0);
                    ctb.PlaceholderMargin = new Thickness(4, 0, 20, 0);
                }
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClearCommand == null)
            {
                Text = string.Empty;
            }
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

        public new bool Focus()
        {
            return InputBox.Focus();
        }

        public void SelectAll()
        {
            InputBox.SelectAll();
        }

        public event TextChangedEventHandler TextChanged
        {
            add { InputBox.TextChanged += value; }
            remove { InputBox.TextChanged -= value; }
        }
    }
}
