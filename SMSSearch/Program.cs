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
            // Immediate boot logging to confirm execution start
            try
            {
                string bootLog = Path.Combine(Path.GetTempPath(), "SMSSearch_Boot.log");
                File.AppendAllText(bootLog, $"[{DateTime.Now}] SMS Search process starting...\n");
            }
            catch { /* Best effort */ }

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

        public static void HandleStartupException(Exception ex)
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
                // Fallback 1: LocalAppData
                try
                {
                    string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMS Search");
                    if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);

                    logPath = Path.Combine(appDataPath, "startup_error.log");
                    File.AppendAllText(logPath, errorMsg);
                    logWritten = true;
                }
                catch
                {
                    // Fallback 2: Temp directory if we can't write to AppData
                    try
                    {
                        logPath = Path.Combine(Path.GetTempPath(), "SMSSearch_startup_error.log");
                        File.AppendAllText(logPath, errorMsg);
                        logWritten = true;
                    }
                    catch
                    {
                        // If all fail, we can't log to file.
                    }
                }
            }

            string userMessage = $"The application failed to start.\n\nError: {ex.Message}";
            if (logWritten)
            {
                userMessage += $"\n\nDetails have been logged to:\n{logPath}";
            }

            // Use WinForms MessageBox as it's less dependent on WPF infrastructure which might be broken
            try
            {
                MessageBox.Show(userMessage, "SMS Search - Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Absolute last resort: write to console if attached (unlikely in WinExe)
                // or just die silently if we can't show UI.
            }
        }
    }
}
