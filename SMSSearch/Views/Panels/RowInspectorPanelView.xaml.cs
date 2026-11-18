using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SMS_Search.Models;
using SMS_Search.ViewModels;

namespace SMS_Search.Views.Panels
{
    public partial class RowInspectorPanelView : System.Windows.Controls.UserControl
    {
        public RowInspectorPanelView()
        {
            InitializeComponent();
        }

        private void InspectorGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.Item is RowInspectorField field && DataContext is MainViewModel mainVm)
            {
                mainVm.ResultsVm.CopyInspectorValueCommand.Execute(field);
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}

