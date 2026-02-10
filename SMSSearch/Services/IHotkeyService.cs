using System;
using System.Windows.Input;

namespace SMS_Search.Services
{
    public interface IHotkeyService
    {
        void Register(IntPtr hwnd, Key key, ModifierKeys modifiers, Action action);
        void Unregister();
        void ProcessMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
    }
}
