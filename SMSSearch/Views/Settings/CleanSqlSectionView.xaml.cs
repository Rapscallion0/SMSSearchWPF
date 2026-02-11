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

                // Use Dispatcher to let the commit finish before we act
                Dispatcher.InvokeAsync(async () =>
                {
                    var viewModel = DataContext as CleanSqlSectionViewModel;
                    if (viewModel != null)
                    {
                        if (rule != null && string.IsNullOrWhiteSpace(rule.Pattern))
                        {
                            // Only remove if it's not the currently selected item.
                            // This prevents removal while the user is still interacting with the row (e.g. double clicking to edit).
                            if (viewModel.Rules.Contains(rule) && viewModel.SelectedRule != rule)
                            {
                                viewModel.Rules.Remove(rule);
                            }
                        }
                        // Always save on commit (whether added, modified, or removed empty row)
                        await viewModel.SaveRules();
                    }
                });
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
                var parent = ((sender as System.Windows.Controls.Control)?.Parent as UIElement) ??
                             (sender is DependencyObject d ? System.Windows.Media.VisualTreeHelper.GetParent(d) as UIElement : null);
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
