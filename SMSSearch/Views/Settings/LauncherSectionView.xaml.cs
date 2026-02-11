using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SMS_Search.ViewModels.Settings;

namespace SMS_Search.Views.Settings
{
    public partial class LauncherSectionView : System.Windows.Controls.UserControl
    {
        public LauncherSectionView()
        {
            InitializeComponent();
        }

        private void Hotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            if (DataContext is LauncherSectionViewModel vm)
            {
                Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
                ModifierKeys modifiers = Keyboard.Modifiers;

                vm.CaptureHotkey(key, modifiers);
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
