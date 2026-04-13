using System.Windows;

namespace SMS_Search.Views.Gs1
{
    public partial class Gs1BarcodeWindow : Window
    {
        public Gs1BarcodeWindow()
        {
            InitializeComponent();
        }

        private void Window_LocationChanged(object? sender, System.EventArgs e)
        {
            ToastWindow.UpdateAllToastPositions(false, this);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ToastWindow.UpdateAllToastPositions(false, this);
        }
    }
}