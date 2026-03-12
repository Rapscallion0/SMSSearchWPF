using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SMS_Search.Views.Controls
{
    public partial class ImportOverlay : System.Windows.Controls.UserControl
    {
        public ImportOverlay()
        {
            InitializeComponent();
        }

        private void UserControl_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (DataContext is ViewModels.ImportViewModel vm)
                {
                    vm.HandleDroppedFilesCommand.Execute(files);
                }
            }
        }

        private void UserControl_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                e.Effects = System.Windows.DragDropEffects.Copy;
            else
                e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
        }
    }
}