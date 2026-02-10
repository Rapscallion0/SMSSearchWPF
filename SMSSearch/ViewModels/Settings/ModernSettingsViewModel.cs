using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace SMS_Search.ViewModels.Settings
{
    public partial class ModernSettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<SettingsSectionViewModel> _sections;

        [ObservableProperty]
        private SettingsSectionViewModel _selectedSection;

        public ModernSettingsViewModel(
            GeneralSectionViewModel general,
            ConnectionSectionViewModel connection,
            DisplaySectionViewModel display,
            CleanSqlSectionViewModel cleanSql,
            LauncherSectionViewModel launcher)
        {
            Sections = new ObservableCollection<SettingsSectionViewModel>
            {
                general,
                connection,
                display,
                cleanSql,
                launcher
            };
            SelectedSection = Sections[0];
        }
    }
}
