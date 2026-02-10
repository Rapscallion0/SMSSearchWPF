using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class HotkeyService : IHotkeyService
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;
        private Action _action;
        private bool _isRegistered;
        private IntPtr _hwnd;
        private readonly ILoggerService _logger;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyService(ILoggerService logger)
        {
            _logger = logger;
        }

        public void Register(IntPtr hwnd, Key key, ModifierKeys modifiers, Action action)
        {
            if (_isRegistered) Unregister();

            _hwnd = hwnd;
            _action = action;

            int vkey = KeyInterop.VirtualKeyFromKey(key);
            uint fsModifiers = 0;
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) fsModifiers |= 1;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control) fsModifiers |= 2;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) fsModifiers |= 4;
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) fsModifiers |= 8;

            if (RegisterHotKey(_hwnd, HOTKEY_ID, fsModifiers, (uint)vkey))
            {
                _isRegistered = true;
                _logger.LogInfo($"Global hotkey registered: {modifiers}+{key}");
            }
            else
            {
                _logger.LogError("Failed to register global hotkey.");
            }
        }

        public void Unregister()
        {
            if (_isRegistered && _hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, HOTKEY_ID);
                _isRegistered = false;
                _logger.LogInfo("Global hotkey unregistered.");
            }
        }

        public void ProcessMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _logger.LogInfo("Global hotkey triggered.");
                _action?.Invoke();
                handled = true;
            }
        }
    }
}
