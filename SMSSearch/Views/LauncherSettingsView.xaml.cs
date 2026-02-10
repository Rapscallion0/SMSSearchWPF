using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SMS_Search.ViewModels;

namespace SMS_Search.Views
{
    public partial class LauncherSettingsView : System.Windows.Controls.UserControl
    {
        public LauncherSettingsView()
        {
            InitializeComponent();
            this.Unloaded += (s, e) =>
            {
                if (DataContext is LauncherSettingsViewModel vm)
                {
                    vm.StopMonitoring();
                }
            };
        }

        private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is LauncherSettingsViewModel vm)
            {
                var key = e.Key;
                if (key == Key.System) key = e.SystemKey;

                // Pass all keys to VM to support building display
                vm.CaptureHotkey(key, Keyboard.Modifiers);
                e.Handled = true;
            }
        }

        private void TextBox_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is LauncherSettingsViewModel vm)
            {
                vm.ResetPreview();
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is LauncherSettingsViewModel vm)
            {
                vm.ResetPreview();
            }
        }
    }
}
