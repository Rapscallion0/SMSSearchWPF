using System;
using System.IO;
using System.Windows;

namespace SMS_Search
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
                string errorMsg = $"[{DateTime.Now}] Critical Startup Error:\n{ex}\n\n";

                try
                {
                    File.AppendAllText(logPath, errorMsg);
                }
                catch
                {
                    // Ignore logging failure if we can't write to disk
                }

                System.Windows.MessageBox.Show($"The application failed to start.\n\nError: {ex.Message}\n\nSee '{logPath}' for details.", "SMS Search - Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
