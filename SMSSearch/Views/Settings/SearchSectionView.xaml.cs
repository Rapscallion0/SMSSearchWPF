namespace SMS_Search.Views.Settings
{
    public partial class SearchSectionView : System.Windows.Controls.UserControl
    {
        public SearchSectionView()
        {
            InitializeComponent();
        }

        private void OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = System.Windows.UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((sender as System.Windows.Controls.Control)?.Parent as System.Windows.UIElement) ??
                             (sender is System.Windows.DependencyObject d ? System.Windows.Media.VisualTreeHelper.GetParent(d) as System.Windows.UIElement : null);
                parent?.RaiseEvent(eventArg);
            }
        }

    }
}
