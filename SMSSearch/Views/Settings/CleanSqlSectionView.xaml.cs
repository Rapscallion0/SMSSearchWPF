using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SMS_Search.ViewModels.Settings;

namespace SMS_Search.Views.Settings
{
    public partial class CleanSqlSectionView : System.Windows.Controls.UserControl
    {
        public CleanSqlSectionView()
        {
            InitializeComponent();
        }

        private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var rule = e.Row.Item as CleanSqlRuleViewModel;
                if (rule != null && string.IsNullOrWhiteSpace(rule.Pattern))
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        var viewModel = DataContext as CleanSqlSectionViewModel;
                        if (viewModel != null && viewModel.Rules.Contains(rule))
                        {
                            viewModel.Rules.Remove(rule);
                        }
                    });
                }
            }
        }

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((System.Windows.Controls.Control)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
