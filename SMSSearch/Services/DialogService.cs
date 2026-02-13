using System;
using System.Windows;
using Microsoft.Win32;
using SMS_Search.Views;
using System.Drawing;
using System.Windows.Forms;

namespace SMS_Search.Services
{
    public class DialogService : IDialogService
    {
        public void ShowMessage(string message, string title)
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title)
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool ShowConfirmation(string message, string title)
        {
            return System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public string? OpenFileDialog(string filter)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() == true) return dlg.FileName;
            return null;
        }

        public string? SaveFileDialog(string filter, string defaultName = "")
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = defaultName };
            if (dlg.ShowDialog() == true) return dlg.FileName;
            return null;
        }

        public string? PickColor(string? defaultColor = null)
        {
             using (var dlg = new ColorDialog())
             {
                 if (!string.IsNullOrEmpty(defaultColor))
                 {
                     try
                     {
                         dlg.Color = ColorTranslator.FromHtml(defaultColor);
                     }
                     catch { }
                 }

                 if (dlg.ShowDialog() == DialogResult.OK)
                 {
                     return ColorTranslator.ToHtml(dlg.Color);
                 }
             }
             return null;
        }

        public void ShowToast(string message, string title, ToastType type = ToastType.Info)
        {
            // Ensure UI thread access for creating window
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
            {
                 System.Windows.Application.Current.Dispatcher.Invoke(() =>
                 {
                     var toast = new ToastWindow(message, title, type);
                     toast.Show();
                 });
            }
        }
    }
}
