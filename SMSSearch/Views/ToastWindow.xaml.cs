using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

using System.Collections.Generic;
using System.Linq;

namespace SMS_Search.Views
{
    public partial class ToastWindow : Window
    {
        private static readonly List<ToastWindow> _activeToasts = new List<ToastWindow>();
        private DispatcherTimer? _closeTimer;
        private bool _isDetailsExpanded = false;
        private bool _isClosing = false;
        private int _originalTimeoutSeconds;

        public ToastWindow(string message, string title, ToastType type, int timeoutSeconds, string? details)
        {
            InitializeComponent();

            _originalTimeoutSeconds = timeoutSeconds + (_activeToasts.Count * 2);


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
            if (_originalTimeoutSeconds > 0)
            {
                _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_originalTimeoutSeconds) };
                _closeTimer.Tick += CloseTimer_Tick;
                _closeTimer.Start();
            }

            _activeToasts.Insert(0, this); // Add newest to the top (index 0)

            // Position relative to Main Window
            Loaded += ToastWindow_Loaded;
            Closed += ToastWindow_Closed;
        }

        private void ToastWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAllToastPositions(true);
        }

        private void ToastWindow_Closed(object? sender, EventArgs e)
        {
            _activeToasts.Remove(this);
            UpdateAllToastPositions(false);
        }

        private static void UpdateAllToastPositions(bool animateNewest)
        {
            if (_activeToasts.Count == 0) return;

            var mainWindow = System.Windows.Application.Current.MainWindow;
            double targetLeft;
            double initialBottom;
            double availableHeight;

            if (mainWindow != null && mainWindow.Visibility == Visibility.Visible && mainWindow.WindowState != WindowState.Minimized)
            {
                targetLeft = mainWindow.Left + mainWindow.ActualWidth - _activeToasts[0].ActualWidth - 20;
                initialBottom = mainWindow.Top + mainWindow.ActualHeight - 50;
                availableHeight = mainWindow.ActualHeight - 70; // 50px bottom margin + 20px top margin
            }
            else
            {
                var workArea = SystemParameters.WorkArea;
                targetLeft = workArea.Right - _activeToasts[0].ActualWidth - 10;
                initialBottom = workArea.Bottom - 10;
                availableHeight = workArea.Height - 20;
            }

            double currentTop = initialBottom;

            for (int i = _activeToasts.Count - 1; i >= 0; i--) // Iterate from oldest to newest
            {
                var toast = _activeToasts[i];
                double toastHeight = toast.ActualHeight > 0 ? toast.ActualHeight : toast.Height;

                currentTop -= toastHeight;

                toast.Left = targetLeft;

                // Handle layering if it goes too high
                double targetTop = currentTop;
                if (mainWindow != null && targetTop < mainWindow.Top + 20)
                {
                    // If it goes above the window (plus a small margin), cap it
                    // This creates an overlapping effect
                    targetTop = mainWindow.Top + 20 + (i * 5); // Slight offset for layered effect
                }

                if (i == 0 && animateNewest)
                {
                    // Animate newest from bottom up
                    toast.Top = initialBottom;
                    var anim = new DoubleAnimation(toast.Top, targetTop, TimeSpan.FromSeconds(0.3))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    toast.BeginAnimation(TopProperty, anim);
                }
                else
                {
                    // Animate existing toasts sliding down (or up, depending on the recalculation)
                    if (toast.Top != targetTop && toast.IsLoaded && !toast._isClosing)
                    {
                         var anim = new DoubleAnimation(toast.Top, targetTop, TimeSpan.FromSeconds(0.3))
                         {
                             EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                         };
                         toast.BeginAnimation(TopProperty, anim);
                    }
                    else if (!toast.IsLoaded)
                    {
                         toast.Top = targetTop;
                    }
                }

                // Add spacing between toasts
                currentTop -= 10;
            }
        }

        private void CloseTimer_Tick(object? sender, EventArgs e)
        {
            CloseToast();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseToast();
        }

        private void CloseToast()
        {
            if (_isClosing) return;
            _isClosing = true;

            if (_closeTimer != null) _closeTimer.Stop();

            var anim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.3));
            anim.Completed += (s, _) => Close();
            BeginAnimation(OpacityProperty, anim);
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

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            PauseAllTimers();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RestartAllTimers();
        }

        private static void PauseAllTimers()
        {
            foreach (var toast in _activeToasts)
            {
                if (toast._closeTimer != null)
                {
                    toast._closeTimer.Stop();
                }
            }
        }

        private static void RestartAllTimers()
        {
            foreach (var toast in _activeToasts)
            {
                if (toast._closeTimer != null && !toast._isClosing)
                {
                    // Reset interval to original duration
                    toast._closeTimer.Interval = TimeSpan.FromSeconds(toast._originalTimeoutSeconds);
                    toast._closeTimer.Start();
                }
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (sizeInfo.HeightChanged && this.IsLoaded)
            {
                UpdateAllToastPositions(false);
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
