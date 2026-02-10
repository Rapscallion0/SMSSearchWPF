using System.Windows;
using SMS_Search.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Views;

namespace SMS_Search
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private UnarchiveWindow? _unarchiveWindow;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            viewModel.RequestOpenSettings += OnRequestOpenSettings;
            viewModel.RequestToggleUnarchiveWindow += OnRequestToggleUnarchiveWindow;
        }

        private void OnRequestToggleUnarchiveWindow(bool isVisible)
        {
            if (isVisible)
            {
                if (_unarchiveWindow == null || !_unarchiveWindow.IsLoaded)
                {
                    _unarchiveWindow = App.Current.Services.GetRequiredService<UnarchiveWindow>();
                    _unarchiveWindow.Closed += (s, e) =>
                    {
                        _viewModel.IsUnarchiveTargetVisible = false;
                        _unarchiveWindow = null;
                    };
                    _unarchiveWindow.Show();
                }
            }
            else
            {
                _unarchiveWindow?.Close();
                _unarchiveWindow = null;
            }
        }

        private void OnRequestOpenSettings()
        {
            var win = App.Current.Services.GetRequiredService<SettingsWindow>();
            win.Owner = this;
            win.ShowDialog();
        }
    }
}
