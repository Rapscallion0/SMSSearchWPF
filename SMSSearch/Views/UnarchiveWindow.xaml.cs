using SMS_Search.ViewModels;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.Data;
using System;
using System.IO;
using System.Windows;

namespace SMS_Search.Views
{
    public partial class UnarchiveWindow : Window
    {
        private UnarchiveViewModel _viewModel;
        private IConfigService _config;

        public UnarchiveWindow(UnarchiveViewModel viewModel, IConfigService config)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _config = config;
            DataContext = viewModel;

            _viewModel.RequestClose += () => Close();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            StartupLocationMode mode = StartupLocationMode.Last;
            if (Enum.TryParse(_config.GetValue("GENERAL", "UNARCHIVE_STARTUP_LOCATION"), out StartupLocationMode m))
                mode = m;

            // Load logic from VM
            WindowPositioner.ApplyStartupLocation(this, mode, _viewModel.Left, _viewModel.Top);
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                _viewModel.ProcessFiles(files);
                System.Windows.MessageBox.Show($"Processed {files.Length} items.", "Unarchive", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.SaveLocation(Left, Top);
        }
    }
}
