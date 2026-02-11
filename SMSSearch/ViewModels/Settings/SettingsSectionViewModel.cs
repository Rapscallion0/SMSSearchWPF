using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace SMS_Search.ViewModels.Settings
{
    public abstract class SettingsSectionViewModel : ObservableObject
    {
        public abstract string Title { get; }
        public abstract Style Icon { get; }
    }
}
