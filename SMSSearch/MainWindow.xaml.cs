using System;
using System.ComponentModel;
using System.Windows;
using SMS_Search.ViewModels;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.Data;
using SMS_Search.Views;
using Microsoft.Extensions.DependencyInjection;

namespace SMS_Search
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IConfigService _config;
        private UnarchiveWindow? _unarchiveWindow;

        public MainWindow(MainViewModel viewModel, IConfigService config)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _config = config;
            DataContext = viewModel;
            viewModel.RequestOpenSettings += OnRequestOpenSettings;
            viewModel.RequestToggleUnarchiveWindow += OnRequestToggleUnarchiveWindow;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (Enum.TryParse(_config.GetValue("GENERAL", "MAIN_STARTUP_LOCATION"), out StartupLocationMode mode))
            {
                // good
            }
            else
            {
                mode = StartupLocationMode.Last;
            }

            double? lastX = null;
            if (double.TryParse(_config.GetValue("MAIN", "LAST_X"), out double x)) lastX = x;

            double? lastY = null;
            if (double.TryParse(_config.GetValue("MAIN", "LAST_Y"), out double y)) lastY = y;

            WindowPositioner.ApplyStartupLocation(this, mode, lastX, lastY);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (WindowState == WindowState.Normal)
            {
                _config.SetValue("MAIN", "LAST_X", Left.ToString());
                _config.SetValue("MAIN", "LAST_Y", Top.ToString());
                _config.Save();
            }
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
