using System;
using System.Windows;
using Microsoft.Win32;
using SMS_Search.Views;
using System.Drawing;
using System.Windows.Forms;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class DialogService : IDialogService
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILoggerService _loggerService;

        public DialogService(ISettingsRepository settingsRepository, ILoggerService loggerService)
        {
            _settingsRepository = settingsRepository;
            _loggerService = loggerService;
        }

        public void ShowMessage(string message, string title)
        {
            ShowToast(message, title, ToastType.Info);
        }

        public void ShowError(string message, string title)
        {
            string logPath = _loggerService.GetCurrentLogPath();
            ShowToast(message, title, ToastType.Error, null, logPath);
        }

        public void ShowWarning(string message, string title)
        {
            string logPath = _loggerService.GetCurrentLogPath();
            ShowToast(message, title, ToastType.Warning, null, logPath);
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

        public void ShowToast(string message, string title, ToastType type = ToastType.Info, string? details = null, string? filePath = null, System.Windows.Window? owner = null)
        {
            // Ensure UI thread access for creating window
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
            {
                 System.Windows.Application.Current.Dispatcher.Invoke(() =>
                 {
                     // Get Timeout
                     int timeout = 5;
                     string? val = _settingsRepository.GetValue("GENERAL", "TOAST_TIMEOUT");
                     if (!string.IsNullOrEmpty(val) && int.TryParse(val, out int t))
                     {
                         timeout = t;
                     }

                     var toast = new ToastWindow(message, title, type, timeout, details, filePath);

                     var targetWindow = owner ?? System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(x => x.IsActive) ?? System.Windows.Application.Current.MainWindow;
                     if (targetWindow != null && targetWindow.IsVisible)
                     {
                         toast.Owner = targetWindow;
                     }

                     toast.Show();
                 });
            }
        }

        public ExistingTableAction ShowTableExistsPrompt(string tableName)
        {
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var dlg = new SMS_Search.Views.Windows.ImportTableExistsDialog(tableName);
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    if (mainWindow != null && mainWindow.IsVisible)
                    {
                        dlg.Owner = mainWindow;
                    }

                    dlg.ShowDialog();
                    return dlg.Result;
                });
            }

            // Fallback if no UI thread context exists
            return ExistingTableAction.Skip;
        }
    }
}
