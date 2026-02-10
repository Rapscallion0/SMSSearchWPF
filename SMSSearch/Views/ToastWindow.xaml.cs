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
        private DispatcherTimer _closeTimer;

        public ToastWindow(string message, string title, ToastType type)
        {
            InitializeComponent();

            lblMessage.Text = message;
            lblTitle.Text = title;

            switch (type)
            {
                case ToastType.Success:
                    BorderBrush = new SolidColorBrush(Color.FromRgb(57, 155, 53)); // Green
                    break;
                case ToastType.Info:
                    BorderBrush = new SolidColorBrush(Color.FromRgb(18, 136, 191)); // Blue
                    break;
                case ToastType.Error:
                    BorderBrush = new SolidColorBrush(Color.FromRgb(227, 50, 45)); // Red
                    break;
                case ToastType.Warning:
                    BorderBrush = new SolidColorBrush(Color.FromRgb(245, 171, 35)); // Orange
                    break;
            }

            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _closeTimer.Tick += CloseTimer_Tick;
            _closeTimer.Start();

            // Position bottom-right
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 10;
            Top = workArea.Bottom - Height - 10;

            MouseLeftButtonUp += (s, e) => Close();
        }

        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            _closeTimer.Stop();
            var anim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
            anim.Completed += (s, _) => Close();
            BeginAnimation(OpacityProperty, anim);
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
