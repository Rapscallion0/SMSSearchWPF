using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using SMS_Search.ViewModels.Settings;
using SMS_Search.Views;
using SMS_Search.Services.Gs1;
using SMS_Search.ViewModels.Gs1;
using SMS_Search.Views.Gs1;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
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
                new ConfigManager(PathHelper.GetSettingsPath()));

            services.AddSingleton<IStateService>(provider =>
                new StateManager(PathHelper.GetStatePath()));

            services.AddSingleton<ISettingsRepository, SettingsRepository>();

            services.AddSingleton<ILoggerService, LoggerService>();

            services.AddSingleton<IDataRepository, DataRepository>();
            services.AddSingleton<IIntellisenseService, IntellisenseService>();
            services.AddTransient<VirtualGridContext>();
            services.AddSingleton<IQueryHistoryService, QueryHistoryService>();

            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IHotkeyService, HotkeyService>();

            services.AddSingleton<UpdateChecker>();

            // GS1 Toolkit Services
            services.AddSingleton<IGs1Repository, Gs1Repository>();
            services.AddSingleton<IGs1Parser, Gs1Parser>();
            services.AddSingleton<IGs1BarcodeService, Gs1BarcodeService>();
            services.AddSingleton<Gs1ClipboardWatcher>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<ResultsViewModel>();
            services.AddTransient<EulaViewModel>();
            services.AddTransient<UnarchiveViewModel>();
            services.AddTransient<Gs1ToolkitViewModel>();

            // New Settings ViewModels
            services.AddTransient<GeneralSectionViewModel>();
            services.AddTransient<ConnectionSectionViewModel>();
            services.AddTransient<SearchSectionViewModel>();
            services.AddTransient<ResultsSectionViewModel>();
            services.AddTransient<EditorSectionViewModel>();
            services.AddTransient<CleanSqlSectionViewModel>();
            services.AddTransient<IntegrationSectionViewModel>();
            services.AddTransient<Gs1ToolkitSettingsSectionViewModel>();
            services.AddTransient<SystemSectionViewModel>();
            services.AddTransient<ModernSettingsViewModel>();

            services.AddTransient<MainWindow>();
            services.AddTransient<ModernSettingsWindow>();
            services.AddTransient<EulaWindow>();
            services.AddTransient<UnarchiveWindow>();
            services.AddTransient<Gs1ToolkitWindow>();

            return services.BuildServiceProvider();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var logger = Services.GetService<ILoggerService>();
                logger?.Dispose();

                var hotkeyService = Services.GetService<IHotkeyService>();
                if (hotkeyService is IDisposable disposableHotkey)
                {
                    disposableHotkey.Dispose();
                }
            }
            catch { }

            base.OnExit(e);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
                {
                    PdfSharp.Fonts.GlobalFontSettings.FontResolver = new SMS_Search.Utils.SystemFontResolver();
                }

                // Prevent application from exiting when EulaWindow or SettingsWindow closes
                this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Service resolution might fail (e.g. LoggerService constructor)
                var logger = Services.GetRequiredService<ILoggerService>();
                var configService = Services.GetRequiredService<IConfigService>();

                // Global Exception Handling
                this.DispatcherUnhandledException += (s, args) =>
                {
                    logger.Log(LogLevel.Critical, $"Unhandled Dispatcher Exception: {args.Exception.Message}");
                    logger.LogError("Unhandled Dispatcher Exception", args.Exception);
                    // Optional: Notify user
                    // MessageBox.Show("An unexpected error occurred. See logs for details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    args.Handled = true; // Prevent immediate crash, but app might be unstable
                };

                TaskScheduler.UnobservedTaskException += (s, args) =>
                {
                    logger.Log(LogLevel.Critical, $"Unobserved Task Exception: {args.Exception.Message}");
                    logger.LogError("Unobserved Task Exception", args.Exception);
                    args.SetObserved();
                };

                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    var ex = args.ExceptionObject as Exception;
                    logger.Log(LogLevel.Critical, $"Unhandled AppDomain Exception: {ex?.Message ?? "Unknown Error"}");
                    if (ex != null) logger.LogError("Unhandled AppDomain Exception", ex);
                };

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

                // Connection Check
                while (true)
                {
                    string server = configService.GetValue("CONNECTION", "SERVER") ?? "";
                    string database = configService.GetValue("CONNECTION", "DATABASE") ?? "";

                    if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
                    {
                        var dialogService = Services.GetRequiredService<IDialogService>();
                        System.Windows.MessageBox.Show("Required connection settings (Server, Database) are missing or invalid. Please provide them to continue.", "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);

                        var settingsWindow = Services.GetRequiredService<ModernSettingsWindow>();
                        var settingsVm = settingsWindow.DataContext as ModernSettingsViewModel;

                        // Force navigate to connection section
                        if (settingsVm != null)
                        {
                            var connSection = System.Linq.Enumerable.FirstOrDefault(settingsVm.Sections, s => s is ConnectionSectionViewModel);
                            if (connSection != null)
                            {
                                settingsVm.SelectedSection = connSection;
                            }
                        }

                        settingsWindow.ShowDialog();

                        // Re-check after window closes
                        string newServer = configService.GetValue("CONNECTION", "SERVER") ?? "";
                        string newDatabase = configService.GetValue("CONNECTION", "DATABASE") ?? "";

                        if (string.IsNullOrWhiteSpace(newServer) || string.IsNullOrWhiteSpace(newDatabase))
                        {
                            var result = System.Windows.MessageBox.Show("Settings are still invalid. The application will close. Do you want to go back and fix them?", "Invalid Settings", MessageBoxButton.YesNo, MessageBoxImage.Error);
                            if (result == MessageBoxResult.No)
                            {
                                Shutdown();
                                return;
                            }
                            // Loop continues and opens settings again
                        }
                        else
                        {
                            // Valid settings provided
                            break;
                        }
                    }
                    else
                    {
                        // Settings exist and are valid
                        break;
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
                        Width = 0,
                        Height = 0,
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
                    this.MainWindow = mainWindow;
                    this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    mainWindow.Show();

                    // Update Check after window is shown to avoid blocking startup
                    if (configService.GetValue("GENERAL", "CHECKUPDATE") == "1")
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var updateChecker = Services.GetRequiredService<UpdateChecker>();
                                var info = await updateChecker.CheckForUpdatesAsync();
                                if (info.IsNewer)
                                {
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        var updateWindow = new SMS_Search.Views.Windows.UpdateWindow(info, updateChecker);
                                        updateWindow.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? mainWindow;
                                        updateWindow.ShowDialog();
                                    });
                                }
                            }
                            catch (Exception updateEx)
                            {
                                logger.LogError("Background update check failed", updateEx);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any startup exceptions that occur within the WPF context (Async Void)
                // This ensures we catch DI resolution failures, Window creation failures, etc.
                SMS_Search.Program.HandleStartupException(ex);

                // Force exit as the app is likely in a bad state
                Environment.Exit(1);
            }
        }
    }
}
