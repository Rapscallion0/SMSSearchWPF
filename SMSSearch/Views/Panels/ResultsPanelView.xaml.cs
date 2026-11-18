using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SMS_Search.ViewModels;

namespace SMS_Search.Views.Panels
{
    public partial class ResultsPanelView : System.Windows.Controls.UserControl
    {
        public ResultsPanelView()
        {
            InitializeComponent();
        }

        private void TabHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is ResultTabViewModel tab)
            {
                e.Handled = true;
                tab.BeginRename();
            }
        }

        private void RenameTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is ResultTabViewModel tab)
            {
                tab.BeginRename();
            }
        }

        private void TabRenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb && tb.IsVisible)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    tb.Focus();
                    tb.SelectAll();
                }));
            }
        }

        private void TabRenameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb || tb.DataContext is not ResultTabViewModel tab) return;

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                tab.CommitRename();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                tab.CancelRename();
            }
        }

        private void TabRenameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is ResultTabViewModel tab)
            {
                tab.CommitRename();
            }
        }
    }
}
