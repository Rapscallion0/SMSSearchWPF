using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace SMS_Search.ViewModels.Settings
{
    public abstract class SettingsSectionViewModel : ObservableObject
    {
        public abstract string Title { get; }
        public abstract Geometry Icon { get; }
    }
}
