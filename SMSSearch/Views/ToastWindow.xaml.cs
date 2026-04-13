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
        private string? _filePath;

        public ToastWindow(string message, string title, ToastType type, int timeoutSeconds, string? details, string? filePath = null)
        {
            InitializeComponent();

            _originalTimeoutSeconds = timeoutSeconds + (_activeToasts.Count * 2);
            _filePath = filePath;

            if (!string.IsNullOrEmpty(_filePath) && System.IO.File.Exists(_filePath))
            {
                btnOpenFolder.Visibility = Visibility.Visible;
                rectSeparator.Visibility = Visibility.Visible;

                // Check if it's a log file
                bool isLogFile = (type == ToastType.Error || type == ToastType.Warning) &&
                                 System.IO.Path.GetFileName(_filePath).StartsWith("SMSSearch_log", StringComparison.OrdinalIgnoreCase);

                if (isLogFile)
                {
                    btnOpenLog.Visibility = Visibility.Visible;
                }
                else
                {
                    btnOpenFile.Visibility = Visibility.Visible;

                    // Determine file extension
                    string ext = System.IO.Path.GetExtension(_filePath).TrimStart('.').ToUpper();

                    // Get the template parts to set text overlay
                    btnOpenFile.Loaded += (s, e) =>
                    {
                        if (btnOpenFile.Template.FindName("FileIconText", btnOpenFile) is System.Windows.Controls.TextBlock txt)
                        {
                            if (ext == "CSV" || ext == "JSON" || ext == "XML")
                                txt.Text = ext;
                        }
                    };
                }
            }


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
            UpdateAllToastPositions(true, this.Owner);
        }

        private void ToastWindow_Closed(object? sender, EventArgs e)
        {
            _activeToasts.Remove(this);
            UpdateAllToastPositions(false, this.Owner);

            if (_closeTimer != null)
            {
                _closeTimer.Stop();
                _closeTimer.Tick -= CloseTimer_Tick;
            }
        }

        public static void UpdateAllToastPositions(bool animateNewest, Window? targetWindow = null)
        {
            if (_activeToasts.Count == 0) return;

            var toastsByOwner = _activeToasts.GroupBy(t => t.Owner).ToList();

            foreach (var group in toastsByOwner)
            {
                var ownerWindow = group.Key;

                // If a specific target window was provided, only update that window's toasts.
                if (targetWindow != null && ownerWindow != targetWindow) continue;

                var toastsInGroup = group.ToList();

                double targetLeft;
                double initialBottom;
                double availableHeight;

                if (ownerWindow != null && ownerWindow.Visibility == Visibility.Visible && ownerWindow.WindowState != WindowState.Minimized)
                {
                    targetLeft = ownerWindow.Left + ownerWindow.ActualWidth - toastsInGroup[0].ActualWidth - 20;
                    initialBottom = ownerWindow.Top + ownerWindow.ActualHeight - 50;
                    availableHeight = ownerWindow.ActualHeight - 70; // 50px bottom margin + 20px top margin
                }
                else
                {
                    var workArea = SystemParameters.WorkArea;
                    targetLeft = workArea.Right - toastsInGroup[0].ActualWidth - 10;
                    initialBottom = workArea.Bottom - 10;
                    availableHeight = workArea.Height - 20;
                }

                double currentTop = initialBottom;

                for (int i = toastsInGroup.Count - 1; i >= 0; i--) // Iterate from oldest to newest
                {
                    var toast = toastsInGroup[i];
                    double toastHeight = toast.ActualHeight > 0 ? toast.ActualHeight : toast.Height;

                    currentTop -= toastHeight;

                    toast.Left = targetLeft;

                    // Handle layering if it goes too high
                    double targetTop = currentTop;
                    if (ownerWindow != null && targetTop < ownerWindow.Top + 20)
                    {
                        // If it goes above the window (plus a small margin), cap it
                        // This creates an overlapping effect
                        targetTop = ownerWindow.Top + 20 + (i * 5); // Slight offset for layered effect
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
        }

        private void CloseTimer_Tick(object? sender, EventArgs e)
        {
            CloseToast();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
                System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl))
            {
                CloseAllToasts();
            }
            else
            {
                CloseToast();
            }
        }

        private static void CloseAllToasts()
        {
            var toastsToClose = _activeToasts.ToList();
            foreach (var toast in toastsToClose)
            {
                toast.CloseToast();
            }
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

        private void Window_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            bool showAll = _activeToasts.Count > 1;
            menuCopyAllMessages.Visibility = showAll ? Visibility.Visible : Visibility.Collapsed;
            menuSeparator.Visibility = showAll ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(GetMessageText());
            }
            catch (Exception)
            {
                // Ignore clipboard errors
            }
        }

        private void CopyAllMessages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var messages = _activeToasts.Select(t => t.GetMessageText());
                string allText = string.Join(Environment.NewLine + "-----------------------" + Environment.NewLine, messages);
                System.Windows.Clipboard.SetText(allText);
            }
            catch (Exception)
            {
                // Ignore clipboard errors
            }
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_filePath) && System.IO.File.Exists(_filePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_filePath) && System.IO.File.Exists(_filePath))
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_filePath}\"");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not open folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseAllMessages_Click(object sender, RoutedEventArgs e)
        {
            CloseAllToasts();
        }

        private string GetMessageText()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(lblTitle.Text))
                parts.Add(lblTitle.Text);
            if (!string.IsNullOrWhiteSpace(lblMessage.Text))
                parts.Add(lblMessage.Text);
            if (!string.IsNullOrWhiteSpace(txtDetails.Text) && txtDetails.Text != "Details...")
                parts.Add(txtDetails.Text);

            return string.Join(Environment.NewLine, parts);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (sizeInfo.HeightChanged && this.IsLoaded)
            {
                UpdateAllToastPositions(false, this.Owner);
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
