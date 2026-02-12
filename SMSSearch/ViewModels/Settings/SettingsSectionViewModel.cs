using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Controls;

namespace SMS_Search.ViewModels.Settings
{
    public abstract class SettingsSectionViewModel : ObservableObject
    {
        public abstract string Title { get; }
        public abstract ControlTemplate Icon { get; }
    }
}
