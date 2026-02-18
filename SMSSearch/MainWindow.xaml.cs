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

            // Restore Split Position
            if (double.TryParse(_config.GetValue("MAIN", "SEARCH_HEIGHT"), out double h))
            {
                SearchRow.Height = new GridLength(h);
            }

            // Restore Size
            bool rememberSize = _config.GetValue("GENERAL", "MAIN_REMEMBER_SIZE") == "1";
            if (rememberSize)
            {
                if (double.TryParse(_config.GetValue("MAIN", "LAST_W"), out double w)) Width = w;
                if (double.TryParse(_config.GetValue("MAIN", "LAST_H"), out double hVal)) Height = hVal;
            }

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

            _config.SetValue("MAIN", "SEARCH_HEIGHT", SearchRow.Height.Value.ToString());
            _config.SetValue("MAIN", "LAST_TAB", _viewModel.SearchVm.SelectedMode.ToString());

            if (WindowState == WindowState.Normal)
            {
                _config.SetValue("MAIN", "LAST_X", Left.ToString());
                _config.SetValue("MAIN", "LAST_Y", Top.ToString());

                if (_config.GetValue("GENERAL", "MAIN_REMEMBER_SIZE") == "1")
                {
                    _config.SetValue("MAIN", "LAST_W", ActualWidth.ToString());
                    _config.SetValue("MAIN", "LAST_H", ActualHeight.ToString());
                }
                _config.Save();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _unarchiveWindow?.Close();
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
            MaskOverlay.Visibility = Visibility.Visible;
            try
            {
                var win = App.Current.Services.GetRequiredService<ModernSettingsWindow>();
                win.Owner = this;
                win.ShowDialog();
            }
            finally
            {
                MaskOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }
}
