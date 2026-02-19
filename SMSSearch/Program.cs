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
            // Use the actual EXE directory to ensure visibility
            string logDir = GetBestLogDir();
            try
            {
                string bootLog = Path.Combine(logDir, "SMSSearch_Boot.log");
                File.AppendAllText(bootLog, $"[{DateTime.Now}] SMS Search process starting...\n");
            }
            catch
            {
                 // Fallback to temp if we can't write to exe dir
                 try
                 {
                     string bootLog = Path.Combine(Path.GetTempPath(), "SMSSearch_Boot.log");
                     File.AppendAllText(bootLog, $"[{DateTime.Now}] SMS Search process starting (Temp fallback)...\n");
                 }
                 catch {}
            }

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

        private static string GetBestLogDir()
        {
            try
            {
                var processModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                if (processModule?.FileName != null)
                {
                    return Path.GetDirectoryName(processModule.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            catch {}
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static void HandleStartupException(Exception ex)
        {
            string bestDir = GetBestLogDir();
            string logPath = Path.Combine(bestDir, "startup_error.log");
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
