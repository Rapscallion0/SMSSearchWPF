using System;
using System.Windows.Controls;
using System.Windows.Input;
using SMS_Search.ViewModels;

namespace SMS_Search.Views
{
    public partial class LauncherSettingsView : UserControl
    {
        public LauncherSettingsView()
        {
            InitializeComponent();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is LauncherSettingsViewModel vm)
            {
                var key = e.Key;
                if (key == Key.System) key = e.SystemKey;

                // Ignore modifier keys alone
                if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                    key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftShift || key == Key.RightShift ||
                    key == Key.LWin || key == Key.RWin)
                {
                    return;
                }

                vm.CaptureHotkey(key, Keyboard.Modifiers);
                e.Handled = true;
            }
        }
    }
}
