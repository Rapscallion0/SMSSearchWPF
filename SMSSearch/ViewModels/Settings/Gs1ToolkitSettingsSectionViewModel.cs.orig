using CommunityToolkit.Mvvm.ComponentModel;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels.Settings
{
    public partial class Gs1ToolkitSettingsSectionViewModel : SettingsSectionViewModel
    {
        public override string Title => "GS1 Toolkit";
        public override System.Windows.Controls.ControlTemplate Icon => (System.Windows.Controls.ControlTemplate)System.Windows.Application.Current.Resources["Icon_Nav_Gs1"];

        public Gs1ToolkitSettingsSectionViewModel(ISettingsRepository settingsRepository) : base()
        {
            MonitorClipboard = new ObservableSetting<bool>(settingsRepository, "GS1", "MONITOR_CLIPBOARD", false);
        }


        public ObservableSetting<bool> MonitorClipboard { get; }
    }
}
