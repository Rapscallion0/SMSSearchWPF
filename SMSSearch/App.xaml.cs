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
using System.Net.Http;
using System.Threading;
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

        private static System.Threading.Mutex? _mutex = null;
        private static System.Threading.EventWaitHandle? _showEvent = null;
        private CancellationTokenSource? _shutdownCts = null;
        private HwndSource? _listenerHwndSource = null;
        private Window? _hiddenWindow = null;
        private bool _isMinimizedToTray = false;
        private bool _isRunningInBackgroundOnly = false;
        private bool _isExplicitExit = false;
        private bool _trayWired = false;

        public bool IsHotkeyActive => Services.GetService<IHotkeyService>()?.IsRegistered ?? false;


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
            services.AddSingleton<IConnectionSettingsService, ConnectionSettingsService>();

            services.AddSingleton<ILoggerService, LoggerService>();

            services.AddSingleton<IDataRepository, DataRepository>();
            services.AddSingleton<IFieldDictionaryService, FieldDictionaryService>();
            services.AddSingleton<IIntellisenseService, IntellisenseService>();
            services.AddTransient<VirtualGridContext>();
            services.AddSingleton<IQueryHistoryService, QueryHistoryService>();
            services.AddSingleton<ISavedQueriesService, SavedQueriesService>();

            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IHotkeyService, HotkeyService>();
            services.AddSingleton<ITrayIconService, TrayIconService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IWorkspaceLayoutService, WorkspaceLayoutService>();

            services.AddSingleton<HttpClient>(_ => new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            }));
            services.AddSingleton<UpdateChecker>();

            // GS1 Toolkit Services
            services.AddSingleton<IGs1Repository, Gs1Repository>();
            services.AddSingleton<IGs1Parser, Gs1Parser>();
            services.AddSingleton<IGs1BarcodeService, Gs1BarcodeService>();
            services.AddSingleton<Gs1ClipboardWatcher>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<ResultsViewModel>();
            services.AddTransient<ImportViewModel>();
            services.AddTransient<EulaViewModel>();
            services.AddTransient<UnarchiveViewModel>();
            services.AddTransient<EncryptionUtilityViewModel>();
            services.AddSingleton<Gs1ToolkitViewModel>();

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
            services.AddTransient<SMS_Search.Views.Windows.StartupSetupWindow>();
            services.AddTransient<EulaWindow>();
            services.AddTransient<UnarchiveWindow>();
            services.AddTransient<Gs1ToolkitWindow>();
            services.AddTransient<EncryptionUtilityWindow>();

            services.AddSingleton<Func<UnarchiveWindow>>(sp => () => sp.GetRequiredService<UnarchiveWindow>());
            services.AddSingleton<Func<Gs1ToolkitWindow>>(sp => () => sp.GetRequiredService<Gs1ToolkitWindow>());
            services.AddSingleton<Func<Gs1ToolkitViewModel>>(sp => () => sp.GetRequiredService<Gs1ToolkitViewModel>());
            services.AddSingleton<Func<ModernSettingsWindow>>(sp => () => sp.GetRequiredService<ModernSettingsWindow>());
            services.AddSingleton<Func<EncryptionUtilityWindow>>(sp => () => sp.GetRequiredService<EncryptionUtilityWindow>());
            services.AddSingleton<Func<EncryptionUtilityViewModel>>(sp => () => sp.GetRequiredService<EncryptionUtilityViewModel>());
            services.AddSingleton<Func<ResultsViewModel>>(sp => () => sp.GetRequiredService<ResultsViewModel>());

            return services.BuildServiceProvider();
        }


        public bool IsCloseToTrayEnabled()
        {
            var config = Services.GetRequiredService<IConfigService>();
            string? closeToTray = config.GetValue(AppSettings.Sections.General, AppSettings.Keys.CloseToTray);
            if (closeToTray != null)
            {
                return closeToTray == "1";
            }

            // Migrate legacy LISTENER_ENABLED setting if CLOSE_TO_TRAY has not been explicitly set
            return config.GetValue(AppSettings.Sections.Launcher, AppSettings.Keys.ListenerEnabled) == "1";
        }

        public string GetConfiguredHotkeyRaw()
        {
            var config = Services.GetRequiredService<IConfigService>();
            string? hotkeyStr = config.GetValue(AppSettings.Sections.Launcher, AppSettings.Keys.Hotkey);
            return hotkeyStr ?? AppSettings.Defaults.Hotkey;
        }

        private string? GetFriendlyHotkeyDisplay()
        {
            string hotkeyStr = GetConfiguredHotkeyRaw();
            if (string.IsNullOrWhiteSpace(hotkeyStr)) return null;
            var (key, modifiers) = HotkeyUtils.Parse(hotkeyStr);
            return key != Key.None ? HotkeyUtils.GetFriendlyString(key, modifiers) : null;
        }

        private void EnsureHotkeyHostWindow()
        {
            if (_hiddenWindow == null)
            {
                _hiddenWindow = new Window
                {
                    Title = "SMS_Search_Hotkey_Host_Window",
                    Width = 0,
                    Height = 0,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    Visibility = Visibility.Hidden
                };
                _hiddenWindow.Show();
                _hiddenWindow.Hide();
            }

            if (_listenerHwndSource == null)
            {
                var helper = new WindowInteropHelper(_hiddenWindow);
                _listenerHwndSource = HwndSource.FromHwnd(helper.Handle);
                var hotkeyService = Services.GetRequiredService<IHotkeyService>();
                _listenerHwndSource?.AddHook(hotkeyService.ProcessMessage);
            }
        }

        public bool ApplyGlobalHotkey()
        {
            var hotkeyService = Services.GetRequiredService<IHotkeyService>();
            var logger = Services.GetRequiredService<ILoggerService>();

            hotkeyService.Unregister();

            string hotkeyStr = GetConfiguredHotkeyRaw();
            if (!string.IsNullOrWhiteSpace(hotkeyStr))
            {
                try
                {
                    var (key, modifiers) = HotkeyUtils.Parse(hotkeyStr);
                    if (key != Key.None)
                    {
                        EnsureHotkeyHostWindow();
                        var helper = new WindowInteropHelper(_hiddenWindow!);
                        hotkeyService.Register(helper.Handle, key, modifiers, () =>
                        {
                            try
                            {
                                ShowMainWindow();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError("Failed to launch application from hotkey", ex);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Error registering global hotkey", ex);
                }
            }

            UpdateTrayIconVisibility();
            return hotkeyService.IsRegistered;
        }

        public void RefreshTrayAndHotkeyState()
        {
            UpdateTrayIconVisibility();
        }

        private void EnsureTrayIconWired()
        {
            if (_trayWired) return;
            _trayWired = true;

            var trayIcon = Services.GetRequiredService<ITrayIconService>();
            trayIcon.OpenRequested += () => Dispatcher.InvokeAsync(ShowMainWindow);
            trayIcon.OpenGs1ToolkitRequested += () => Dispatcher.InvokeAsync(OpenGs1ToolkitFromTray);
            trayIcon.OpenUnarchiveRequested += () => Dispatcher.InvokeAsync(OpenUnarchiveFromTray);
            trayIcon.OpenEncryptionUtilityRequested += () => Dispatcher.InvokeAsync(OpenEncryptionUtilityFromTray);
            trayIcon.OpenSettingsRequested += () => Dispatcher.InvokeAsync(OpenSettingsFromTray);
            trayIcon.ExitRequested += () => Dispatcher.InvokeAsync(ExitFromTray);
        }

        public void UpdateTrayIconVisibility()
        {
            var trayIcon = Services.GetRequiredService<ITrayIconService>();
            bool shouldShow = IsCloseToTrayEnabled() || _isMinimizedToTray || _isRunningInBackgroundOnly;
            if (shouldShow)
            {
                EnsureTrayIconWired();
                TrayIconState state = _isMinimizedToTray
                    ? TrayIconState.Minimized
                    : (_isRunningInBackgroundOnly ? TrayIconState.Background : TrayIconState.Open);
                string? hotkeyDisplay = IsHotkeyActive ? GetFriendlyHotkeyDisplay() : null;
                trayIcon.Show(state, hotkeyDisplay);
            }
            else
            {
                trayIcon.Hide();
            }
        }

        public void MinimizeMainWindowToTray(Window mainWindow)
        {
            _isMinimizedToTray = true;
            mainWindow.ShowInTaskbar = false;
            mainWindow.Hide();
            UpdateTrayIconVisibility();
        }

        private void OpenGs1ToolkitFromTray()
        {
            var existing = Windows.OfType<Gs1ToolkitWindow>().FirstOrDefault();
            if (existing != null && existing.IsLoaded)
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }

            var win = Services.GetRequiredService<Func<Gs1ToolkitWindow>>()();
            win.DataContext = Services.GetRequiredService<Func<Gs1ToolkitViewModel>>()();
            win.Show();
            win.Activate();
        }

        private void OpenUnarchiveFromTray()
        {
            var existing = Windows.OfType<UnarchiveWindow>().FirstOrDefault();
            if (existing != null && existing.IsLoaded)
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }

            var win = Services.GetRequiredService<Func<UnarchiveWindow>>()();
            win.Show();
            win.Activate();
        }

        private void OpenEncryptionUtilityFromTray()
        {
            var existing = Windows.OfType<EncryptionUtilityWindow>().FirstOrDefault();
            if (existing != null && existing.IsLoaded)
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }

            var win = Services.GetRequiredService<Func<EncryptionUtilityWindow>>()();
            win.DataContext = Services.GetRequiredService<Func<EncryptionUtilityViewModel>>()();
            win.Show();
            win.Activate();
        }

        private void OpenSettingsFromTray()
        {
            var existing = Windows.OfType<ModernSettingsWindow>().FirstOrDefault();
            if (existing != null && existing.IsLoaded)
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }

            var win = Services.GetRequiredService<Func<ModernSettingsWindow>>()();
            win.Show();
            win.Activate();
        }

        private void ExitFromTray()
        {
            _isExplicitExit = true;
            _isMinimizedToTray = false;
            _isRunningInBackgroundOnly = false;

            var hotkeyService = Services.GetService<IHotkeyService>();
            if (_listenerHwndSource != null && hotkeyService != null)
            {
                _listenerHwndSource.RemoveHook(hotkeyService.ProcessMessage);
                _listenerHwndSource = null;
            }
            hotkeyService?.Unregister();

            if (_hiddenWindow != null)
            {
                _hiddenWindow.Close();
                _hiddenWindow = null;
            }

            Services.GetService<ITrayIconService>()?.Hide();
            Shutdown();
        }

        public void ShowMainWindow()
        {
            if (this.MainWindow is MainWindow actualMainWindow && actualMainWindow.IsLoaded)
            {
                _isMinimizedToTray = false;
                _isRunningInBackgroundOnly = false;
                actualMainWindow.ShowInTaskbar = true;
                if (!actualMainWindow.IsVisible)
                {
                    actualMainWindow.Show();
                }
                if (actualMainWindow.WindowState == WindowState.Minimized)
                {
                    actualMainWindow.WindowState = WindowState.Normal;
                }
                actualMainWindow.Activate();
                actualMainWindow.Focus();
                UpdateTrayIconVisibility();
            }
            else
            {
                _isMinimizedToTray = false;
                _isRunningInBackgroundOnly = false;
                var mainWindow = Services.GetRequiredService<MainWindow>();
                this.MainWindow = mainWindow;
                mainWindow.Closed += (s, e) =>
                {
                    _isMinimizedToTray = false;
                    if (!_isExplicitExit && IsCloseToTrayEnabled())
                    {
                        _isRunningInBackgroundOnly = true;
                        UpdateTrayIconVisibility();
                    }
                    else
                    {
                        _isRunningInBackgroundOnly = false;
                        UpdateTrayIconVisibility();
                        Shutdown();
                    }
                };
                mainWindow.Show();
                UpdateTrayIconVisibility();

                // Update Check
                var configService = Services.GetRequiredService<IConfigService>();
                var logger = Services.GetRequiredService<ILoggerService>();
                if (configService.GetValue(AppSettings.Sections.System, AppSettings.Keys.CheckUpdate) == "1")
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
                                    updateWindow.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive && !(x is SMS_Search.Views.ToastWindow)) ?? mainWindow;
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

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _shutdownCts?.Cancel();
                _shutdownCts?.Dispose();
                _shutdownCts = null;

                _showEvent?.Dispose();
                _showEvent = null;

                _mutex?.Dispose();
                _mutex = null;

                var logger = Services.GetService<ILoggerService>();
                logger?.Dispose();

                var trayIcon = Services.GetService<ITrayIconService>();
                trayIcon?.Dispose();

                var hotkeyService = Services.GetService<IHotkeyService>();
                if (hotkeyService is IDisposable disposableHotkey)
                {
                    disposableHotkey.Dispose();
                }

                if (Services is IDisposable disposableServices)
                {
                    disposableServices.Dispose();
                }
            }
            catch { }

            base.OnExit(e);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "Local\\SMS_Search_App_Mutex";
            const string eventName = "Local\\SMS_Search_Show_Event";

            bool createdNew;
            _mutex = new System.Threading.Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // App is already running. Signal it to wake up.
                try
                {
                    var showEvent = System.Threading.EventWaitHandle.OpenExisting(eventName);
                    showEvent.Set();
                }
                catch { }

                // Exit this instance
                Environment.Exit(0);
                return;
            }

            // We are the first instance. Create the event listener for future instances.
            _showEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, eventName);
            _shutdownCts = new CancellationTokenSource();
            var shutdownToken = _shutdownCts.Token;
            _ = Task.Run(() =>
            {
                var handles = new WaitHandle[] { _showEvent, shutdownToken.WaitHandle };
                while (!shutdownToken.IsCancellationRequested)
                {
                    int index = WaitHandle.WaitAny(handles);
                    if (index == 1 || shutdownToken.IsCancellationRequested) break;
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        ShowMainWindow();
                    });
                }
            }, shutdownToken);

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
                Services.GetRequiredService<IThemeService>().Initialize();

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
                if (configService.GetValue(AppSettings.Sections.General, AppSettings.Keys.Eula) != "1")
                {
                    var eulaWindow = Services.GetRequiredService<EulaWindow>();
                    bool? result = eulaWindow.ShowDialog();
                    if (result != true)
                    {
                        Shutdown();
                        return;
                    }
                }

                // Connection / Dictionary Check
                var fieldDictionaryService = Services.GetRequiredService<IFieldDictionaryService>();
                while (!StartupConditionEvaluator.CanStart(configService, fieldDictionaryService))
                {
                    var setupWindow = Services.GetRequiredService<SMS_Search.Views.Windows.StartupSetupWindow>();
                    bool? setupResult = setupWindow.ShowDialog();
                    if (setupResult != true)
                    {
                        Shutdown();
                        return;
                    }
                }

                logger.LogInfo("Application starting...");

                ApplyGlobalHotkey();

                // If started with --listener (e.g. Windows startup), stay in system tray until summoned.
                bool isListenerArg = e.Args.Contains("--listener");
                if (isListenerArg)
                {
                    _isRunningInBackgroundOnly = true;
                    UpdateTrayIconVisibility();
                }
                else
                {
                    ShowMainWindow();
                }

            }
            catch (Exception ex)
            {
                // Handle any startup exceptions that occur within the WPF context
                // This ensures we catch DI resolution failures, Window creation failures, etc.
                SMS_Search.Program.HandleStartupException(ex);

                // Force exit as the app is likely in a bad state
                Environment.Exit(1);
            }
        }
    }
}
