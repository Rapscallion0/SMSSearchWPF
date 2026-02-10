using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SMS_Search.ViewModels.Settings;

namespace SMS_Search.Views.Settings
{
    public partial class LauncherSectionView : System.Windows.Controls.UserControl
    {
        public LauncherSectionView()
        {
            InitializeComponent();
        }

        private void Hotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            if (DataContext is LauncherSectionViewModel vm)
            {
                Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
                ModifierKeys modifiers = Keyboard.Modifiers;

                vm.CaptureHotkey(key, modifiers);
            }
        }
    }
}
