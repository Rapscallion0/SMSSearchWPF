using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SMS_Search
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Set up a global exception handler for very early failures before Main body executes fully
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    HandleStartupException(ex);
                }
            };

            try
            {
                RunApp();
            }
            catch (Exception ex)
            {
                HandleStartupException(ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApp()
        {
            // Using full type name if necessary, but App is in the same namespace usually
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static void HandleStartupException(Exception ex)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
            string errorMsg = $"[{DateTime.Now}] Critical Startup Error:\n{ex}\n\n";
            bool logWritten = false;

            try
            {
                File.AppendAllText(logPath, errorMsg);
                logWritten = true;
            }
            catch
            {
                // Fallback to temp directory if we can't write to the application directory
                try
                {
                    logPath = Path.Combine(Path.GetTempPath(), "SMSSearch_startup_error.log");
                    File.AppendAllText(logPath, errorMsg);
                    logWritten = true;
                }
                catch
                {
                    // If both fail, we can't log to file.
                }
            }

            string userMessage = $"The application failed to start.\n\nError: {ex.Message}";
            if (logWritten)
            {
                userMessage += $"\n\nDetails have been logged to:\n{logPath}";
            }

            // Use WinForms MessageBox as it's less dependent on WPF infrastructure which might be broken
            MessageBox.Show(userMessage, "SMS Search - Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
