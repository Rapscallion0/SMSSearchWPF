using System;
using System.IO;

namespace SMS_Search.Utils
{
    public static class PathHelper
    {
        private static string? _appDirectory;

        /// <summary>
        /// Gets the best directory for storing application data (settings, logs).
        /// Prioritizes the application's base directory (where the .exe is) if it is writable (for portability).
        /// Falls back to LocalAppData if the base directory is read-only.
        /// </summary>
        public static string GetApplicationDirectory()
        {
            if (_appDirectory != null)
                return _appDirectory;

            string baseDir;
            try
            {
                // Crucial for SingleFile: AppDomain.CurrentDomain.BaseDirectory returns the temp extraction folder.
                // We want the directory where the user actually ran the .exe from.
                var processModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                if (processModule?.FileName != null)
                {
                    baseDir = Path.GetDirectoryName(processModule.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
                }
                else
                {
                    baseDir = AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            catch
            {
                // Fallback if we can't get the process module for some reason
                baseDir = AppDomain.CurrentDomain.BaseDirectory;
            }

            // Check if we can write to the base directory
            if (IsDirectoryWritable(baseDir))
            {
                _appDirectory = baseDir;
            }
            else
            {
                // Fallback to LocalAppData
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(appData, "SMS Search");

                if (!Directory.Exists(appFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(appFolder);
                    }
                    catch
                    {
                        // If we can't create in AppData, fallback to Temp
                        appFolder = Path.Combine(Path.GetTempPath(), "SMS Search");
                        if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
                    }
                }

                _appDirectory = appFolder;
            }

            return _appDirectory;
        }

        private static bool IsDirectoryWritable(string path)
        {
            try
            {
                // If directory doesn't exist, we can't write to it (in this context, we assume base dir exists)
                if (!Directory.Exists(path)) return false;

                // Try to create a dummy file
                string testFile = Path.Combine(path, $".write_test_{Guid.NewGuid()}");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetSettingsPath()
        {
            return Path.Combine(GetApplicationDirectory(), "SMSSearch_settings.json");
        }

        public static string GetStatePath()
        {
            return Path.Combine(GetApplicationDirectory(), "SMSSearch_state.json");
        }

        public static string GetLogDirectory()
        {
            string logDir = Path.Combine(GetApplicationDirectory(), "logs");
            if (!Directory.Exists(logDir))
            {
                try
                {
                    Directory.CreateDirectory(logDir);
                }
                catch
                {
                    // If creating 'logs' fails, just return the base app dir
                    return GetApplicationDirectory();
                }
            }
            return logDir;
        }
    }
}
