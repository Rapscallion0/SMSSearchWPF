using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels.Gs1;
using Microsoft.Extensions.DependencyInjection;

namespace SMS_Search.Services.Gs1
{
    public class Gs1ClipboardWatcher : IDisposable
    {
        private HwndSource? _hwndSource;
        private readonly IConfigService _config;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService _logger;
        private string _lastClipboardText = "";

        // P/Invoke for clipboard listening
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        public Gs1ClipboardWatcher(IConfigService config, IDialogService dialogService, IServiceProvider serviceProvider, ILoggerService logger)
        {
            _config = config;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Start(Window window)
        {
            if (_hwndSource != null) return;

            var helper = new WindowInteropHelper(window);
            _hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
            _hwndSource.AddHook(HwndHook);
            AddClipboardFormatListener(_hwndSource.Handle);
        }

        public void Stop()
        {
            if (_hwndSource != null)
            {
                RemoveClipboardFormatListener(_hwndSource.Handle);
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource = null;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                var monitorSetting = _config.GetValue(AppSettings.Sections.Gs1, AppSettings.Keys.MonitorClipboard);
                if (string.Equals(monitorSetting, "True", StringComparison.OrdinalIgnoreCase) || monitorSetting == "1")
                {
                    CheckClipboardForGs1();
                }
            }
            return IntPtr.Zero;
        }

        private void CheckClipboardForGs1()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText();
                    if (string.IsNullOrWhiteSpace(text) || text == _lastClipboardText) return;

                    // Simple heuristic for GS1 codes: starts with ]C1 or looks like (01)
                    if (text.StartsWith("]C1") || (text.StartsWith("(01)") && text.Length > 10) || (text.StartsWith("(00)") && text.Length > 10))
                    {
                        _lastClipboardText = text;
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (_dialogService.ShowConfirmation($"GS1 code detected on clipboard:\n\n{text}\n\nDo you want to open it in the GS1 Toolkit?", "GS1 Code Detected"))
                            {
                                var window = _serviceProvider.GetRequiredService<SMS_Search.Views.Gs1.Gs1ToolkitWindow>();
                                var vm = _serviceProvider.GetRequiredService<Gs1ToolkitViewModel>();
                                window.DataContext = vm;
                                window.Owner = System.Windows.Application.Current.MainWindow;

                                vm.RawBarcode = text;
                                window.ShowDialog();
                            }
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error reading clipboard in GS1 Watcher.", ex);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
