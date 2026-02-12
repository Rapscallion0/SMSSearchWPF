using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SMS_Search.Views.Settings
{
    public partial class LoggingSectionView : System.Windows.Controls.UserControl
    {
        public LoggingSectionView()
        {
            InitializeComponent();
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;

                UIElement? parent = null;
                if (sender is System.Windows.Controls.Control control)
                {
                    parent = control.Parent as UIElement;
                }

                if (parent == null && sender is DependencyObject d)
                {
                     parent = VisualTreeHelper.GetParent(d) as UIElement;
                }

                if (parent != null)
                {
                    parent.RaiseEvent(eventArg);
                }
            }
        }
    }
}
