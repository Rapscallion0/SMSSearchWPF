using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SMS_Search.Views.Controls
{
    public partial class SplitButton : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(SplitButton), new PropertyMetadata("Save"));

        public static readonly DependencyProperty PrimaryCommandProperty =
            DependencyProperty.Register(nameof(PrimaryCommand), typeof(ICommand), typeof(SplitButton));

        public static readonly DependencyProperty PrimaryToolTipProperty =
            DependencyProperty.Register(nameof(PrimaryToolTip), typeof(string), typeof(SplitButton), new PropertyMetadata(null));

        public static readonly DependencyProperty SecondaryTextProperty =
            DependencyProperty.Register(nameof(SecondaryText), typeof(string), typeof(SplitButton), new PropertyMetadata("Show Saved Queries"));

        public static readonly DependencyProperty SecondaryCommandProperty =
            DependencyProperty.Register(nameof(SecondaryCommand), typeof(ICommand), typeof(SplitButton));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public ICommand? PrimaryCommand
        {
            get => (ICommand?)GetValue(PrimaryCommandProperty);
            set => SetValue(PrimaryCommandProperty, value);
        }

        public string? PrimaryToolTip
        {
            get => (string?)GetValue(PrimaryToolTipProperty);
            set => SetValue(PrimaryToolTipProperty, value);
        }

        public string SecondaryText
        {
            get => (string)GetValue(SecondaryTextProperty);
            set => SetValue(SecondaryTextProperty, value);
        }

        public ICommand? SecondaryCommand
        {
            get => (ICommand?)GetValue(SecondaryCommandProperty);
            set => SetValue(SecondaryCommandProperty, value);
        }

        public SplitButton()
        {
            InitializeComponent();
        }

        private void DropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (DropdownButton.ContextMenu == null) return;
            DropdownButton.ContextMenu.PlacementTarget = OuterBorder;
            DropdownButton.ContextMenu.Placement = PlacementMode.Bottom;
            DropdownButton.ContextMenu.DataContext = this;
            DropdownButton.ContextMenu.IsOpen = true;
        }
    }
}

