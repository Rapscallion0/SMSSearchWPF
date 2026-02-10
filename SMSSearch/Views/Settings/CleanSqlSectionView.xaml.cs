using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SMS_Search.Views.Settings
{
    public partial class CleanSqlSectionView : System.Windows.Controls.UserControl
    {
        public CleanSqlSectionView()
        {
            InitializeComponent();
        }

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((Control)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
