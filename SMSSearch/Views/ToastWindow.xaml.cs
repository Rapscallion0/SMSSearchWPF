using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SMS_Search.Views
{
    public partial class ToastWindow : Window
    {
        private DispatcherTimer? _closeTimer;
        private bool _isDetailsExpanded = false;

        public ToastWindow(string message, string title, ToastType type, int timeoutSeconds, string? details)
        {
            InitializeComponent();

            lblMessage.Text = message;
            lblTitle.Text = title;

            // Handle Details
            if (!string.IsNullOrWhiteSpace(details))
            {
                txtDetails.Text = details;
                btnMoreDetail.Visibility = Visibility.Visible;
            }
            else
            {
                btnMoreDetail.Visibility = Visibility.Collapsed;
                txtDetails.Visibility = Visibility.Collapsed;
            }

            // Set Border Color
            switch (type)
            {
                case ToastType.Success:
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(57, 155, 53)); // Green
                    break;
                case ToastType.Info:
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 136, 191)); // Blue
                    break;
                case ToastType.Error:
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(227, 50, 45)); // Red
                    break;
                case ToastType.Warning:
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 171, 35)); // Orange
                    break;
            }

            // Setup Timer
            if (timeoutSeconds > 0)
            {
                _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(timeoutSeconds) };
                _closeTimer.Tick += CloseTimer_Tick;
                _closeTimer.Start();
            }

            // Position relative to Main Window
            Loaded += ToastWindow_Loaded;
        }

        private void ToastWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.Visibility == Visibility.Visible && mainWindow.WindowState != WindowState.Minimized)
            {
                 // Calculate position relative to MainWindow
                 // Bottom Right
                 // Use ActualWidth/Height if available, otherwise Width/Height
                 double toastWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                 double toastHeight = this.ActualHeight > 0 ? this.ActualHeight : this.Height;

                 double targetLeft = mainWindow.Left + mainWindow.ActualWidth - toastWidth - 20;
                 double targetTop = mainWindow.Top + mainWindow.ActualHeight - toastHeight - 20;

                 this.Left = targetLeft;
                 this.Top = targetTop;
            }
            else
            {
                // Fallback to screen bottom-right if MainWindow is not available
                var workArea = SystemParameters.WorkArea;
                this.Left = workArea.Right - this.ActualWidth - 10;
                this.Top = workArea.Bottom - this.ActualHeight - 10;
            }
        }

        private void CloseTimer_Tick(object? sender, EventArgs e)
        {
            if (_closeTimer != null) _closeTimer.Stop();
            var anim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
            anim.Completed += (s, _) => Close();
            BeginAnimation(OpacityProperty, anim);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_closeTimer != null) _closeTimer.Stop();
            Close();
        }

        private void btnMoreDetail_Click(object sender, RoutedEventArgs e)
        {
            _isDetailsExpanded = !_isDetailsExpanded;
            if (_isDetailsExpanded)
            {
                txtDetails.Visibility = Visibility.Visible;
                btnMoreDetail.Content = "Less Detail...";
            }
            else
            {
                txtDetails.Visibility = Visibility.Collapsed;
                btnMoreDetail.Content = "More Detail...";
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            // If the size changed (e.g. details expanded), adjust Top to keep Bottom fixed
            if (sizeInfo.HeightChanged && this.IsLoaded)
            {
                 this.Top -= (sizeInfo.NewSize.Height - sizeInfo.PreviousSize.Height);
            }
        }
    }

    public enum ToastType
    {
        Success,
        Info,
        Error,
        Warning
    }
}
