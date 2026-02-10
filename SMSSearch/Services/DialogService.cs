using System;
using System.Windows;
using Microsoft.Win32;
using SMS_Search.Views;

namespace SMS_Search.Services
{
    public interface IDialogService
    {
        void ShowMessage(string message, string title);
        void ShowError(string message, string title);
        bool ShowConfirmation(string message, string title);
        string OpenFileDialog(string filter);
        string SaveFileDialog(string filter, string defaultName = "");
        void ShowToast(string message, string title, ToastType type = ToastType.Info);
    }

    public class DialogService : IDialogService
    {
        public void ShowMessage(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool ShowConfirmation(string message, string title)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public string OpenFileDialog(string filter)
        {
            var dlg = new OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() == true) return dlg.FileName;
            return null;
        }

        public string SaveFileDialog(string filter, string defaultName = "")
        {
            var dlg = new SaveFileDialog { Filter = filter, FileName = defaultName };
            if (dlg.ShowDialog() == true) return dlg.FileName;
            return null;
        }

        public void ShowToast(string message, string title, ToastType type = ToastType.Info)
        {
            // Ensure UI thread access for creating window
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                 Application.Current.Dispatcher.Invoke(() =>
                 {
                     var toast = new ToastWindow(message, title, type);
                     toast.Show();
                 });
            }
        }
    }
}
