using CommunityToolkit.Mvvm.ComponentModel;

namespace SMS_Search.ViewModels.Settings
{
    public abstract class SettingsSectionViewModel : ObservableObject
    {
        public abstract string Title { get; }
        public abstract string IconData { get; } // SVG Path Data for the icon
    }
}
