using SMS_Search.ViewModels;
using System;
using System.IO;
using System.Windows;

namespace SMS_Search.Views
{
    public partial class UnarchiveWindow : Window
    {
        private UnarchiveViewModel _viewModel;

        public UnarchiveWindow(UnarchiveViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;

            _viewModel.RequestClose += () => Close();

            // Initial positioning
            Left = _viewModel.Left;
            Top = _viewModel.Top;
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                _viewModel.ProcessFiles(files);
                MessageBox.Show($"Processed {files.Length} items.", "Unarchive", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.SaveLocation(Left, Top);
        }
    }
}
