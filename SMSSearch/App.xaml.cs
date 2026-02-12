using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using SMS_Search.ViewModels.Settings;
using SMS_Search.Views;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SMS_Search
{
    public partial class App : System.Windows.Application
    {
        public new static App Current => (App)System.Windows.Application.Current;
        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IConfigService>(provider =>
                new ConfigManager(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SMSSearch_settings.json")));

            services.AddSingleton<ISettingsRepository, SettingsRepository>();

            services.AddSingleton<ILoggerService, LoggerService>();

            services.AddSingleton<IDataRepository, DataRepository>();
            services.AddTransient<VirtualGridContext>();
            services.AddSingleton<IQueryHistoryService, QueryHistoryService>();

            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IHotkeyService, HotkeyService>();

            services.AddSingleton<UpdateChecker>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<ResultsViewModel>();
            services.AddTransient<EulaViewModel>();
            services.AddTransient<UnarchiveViewModel>();

            // New Settings ViewModels
            services.AddTransient<GeneralSectionViewModel>();
            services.AddTransient<ConnectionSectionViewModel>();
            services.AddTransient<DisplaySectionViewModel>();
            services.AddTransient<CleanSqlSectionViewModel>();
            services.AddTransient<LauncherSectionViewModel>();
            services.AddTransient<LoggingSectionViewModel>();
            services.AddTransient<ModernSettingsViewModel>();

            services.AddTransient<MainWindow>();
            services.AddTransient<ModernSettingsWindow>();
            services.AddTransient<EulaWindow>();
            services.AddTransient<UnarchiveWindow>();

            return services.BuildServiceProvider();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var logger = Services.GetRequiredService<ILoggerService>();
            var configService = Services.GetRequiredService<IConfigService>();

            // EULA Check
            if (configService.GetValue("GENERAL", "EULA") != "1")
            {
                var eulaWindow = Services.GetRequiredService<EulaWindow>();
                bool? result = eulaWindow.ShowDialog();
                if (result != true)
                {
                    Shutdown();
                    return;
                }
            }

            // Update Check
            if (configService.GetValue("GENERAL", "CHECKUPDATE") == "1")
            {
                var updateChecker = Services.GetRequiredService<UpdateChecker>();
                var info = await updateChecker.CheckForUpdatesAsync();
                if (info.IsNewer)
                {
                    var msg = $"There is an update available for download.\n\nCurrent Version: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version}\nNew Version: {info.Version}\n\nWould you like to update now?";
                    if (System.Windows.MessageBox.Show(msg, "SMS Search Update", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
                    {
                        await updateChecker.PerformUpdate(info);
                        return; // Should have shut down in PerformUpdate, but just in case
                    }
                }
            }

            logger.LogInfo("Application starting...");

            bool isListener = false;
            foreach (var arg in e.Args)
            {
                if (arg == "--listener")
                {
                    isListener = true;
                    break;
                }
            }

            if (isListener)
            {
                var hiddenWindow = new Window
                {
                    Title = "SMS_Search_Listener_Hidden_Window",
                    Width = 0, Height = 0,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    Visibility = Visibility.Hidden
                };
                hiddenWindow.Show();
                hiddenWindow.Hide();

                var hotkeyService = Services.GetRequiredService<IHotkeyService>();
                var config = Services.GetRequiredService<IConfigService>();

                string? hotkeyStr = config.GetValue("LAUNCHER", "HOTKEY");
                if (!string.IsNullOrEmpty(hotkeyStr))
                {
                    try
                    {
                        var (key, modifiers) = HotkeyUtils.Parse(hotkeyStr);
                        if (key != Key.None)
                        {
                            var helper = new WindowInteropHelper(hiddenWindow);
                            HwndSource.FromHwnd(helper.Handle).AddHook(hotkeyService.ProcessMessage);
                            hotkeyService.Register(helper.Handle, key, modifiers, () =>
                            {
                                try
                                {
                                    string? fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                                    if (fileName != null)
                                    {
                                        System.Diagnostics.Process.Start(fileName);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError("Failed to launch application", ex);
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error registering hotkey", ex);
                    }
                }

                logger.LogInfo("Listener mode started.");
            }
            else
            {
                var mainWindow = Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
        }
    }
}
