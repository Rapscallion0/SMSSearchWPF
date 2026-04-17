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
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UnarchiveViewModel.IsCompleted))
            {
                if (_viewModel.IsCompleted)
                {
                    // Animate the checkmark
                    System.Windows.Media.Animation.Storyboard sb = new System.Windows.Media.Animation.Storyboard();

                    // 1. Fade in immediately
                    System.Windows.Media.Animation.DoubleAnimation fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(50))
                    };
                    System.Windows.Media.Animation.Storyboard.SetTarget(fadeIn, CheckmarkPath);
                    System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));

                    // 2. Draw the path
                    System.Windows.Media.Animation.DoubleAnimation drawPath = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 120,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                        EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    System.Windows.Media.Animation.Storyboard.SetTarget(drawPath, CheckmarkPath);
                    System.Windows.Media.Animation.Storyboard.SetTargetProperty(drawPath, new PropertyPath("StrokeDashOffset"));

                    // 3. Fade out after the 3 seconds (IsCompleted duration is 4000ms now)
                    System.Windows.Media.Animation.DoubleAnimation fadeOut = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        BeginTime = TimeSpan.FromMilliseconds(3000), // Start fading out after 3 seconds
                        Duration = new Duration(TimeSpan.FromMilliseconds(1000))
                    };
                    System.Windows.Media.Animation.Storyboard.SetTarget(fadeOut, CheckmarkPath);
                    System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

                    sb.Children.Add(fadeIn);
                    sb.Children.Add(drawPath);
                    sb.Children.Add(fadeOut);

                    sb.Begin();
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            StartupLocationMode mode = StartupLocationMode.Last;
            if (Enum.TryParse(_config.GetValue(AppSettings.Sections.General, AppSettings.Keys.UnarchiveStartupLocation), out StartupLocationMode m))
                mode = m;

            // Load logic from VM
            WindowPositioner.ApplyStartupLocation(this, mode, _viewModel.Left, _viewModel.Top);
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                await _viewModel.ProcessFiles(files);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.SaveLocation(Left, Top);
        }
    }
}
