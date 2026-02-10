using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using SMS_Search.Views;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SMS_Search
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
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

            services.AddSingleton<ILoggerService>(provider => new LoggerService("App"));

            services.AddSingleton<IDataRepository, DataRepository>();
            services.AddTransient<VirtualGridContext>();
            services.AddSingleton<IQueryHistoryService, QueryHistoryService>();

            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IHotkeyService, HotkeyService>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<ResultsViewModel>();
            services.AddTransient<SettingsViewModel>();

            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindow>();

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var logger = Services.GetRequiredService<ILoggerService>();
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
                    Width = 0, Height = 0,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    Visibility = Visibility.Hidden
                };
                hiddenWindow.Show();
                hiddenWindow.Hide();

                var hotkeyService = Services.GetRequiredService<IHotkeyService>();
                var config = Services.GetRequiredService<IConfigService>();

                string hotkeyStr = config.GetValue("LAUNCHER", "HOTKEY");
                if (!string.IsNullOrEmpty(hotkeyStr))
                {
                    try
                    {
                        var (key, modifiers) = HotkeyUtils.Parse(hotkeyStr);
                        if (key != Key.None)
                        {
                            var helper = new WindowInteropHelper(hiddenWindow);
                            hotkeyService.Register(helper.Handle, key, modifiers, () =>
                            {
                                try
                                {
                                    System.Diagnostics.Process.Start(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
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
