using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace SMS_Search.ViewModels.Settings
{
    public partial class ModernSettingsViewModel : ObservableObject
    {
        private readonly ObservableCollection<SettingsSectionViewModel> _allSections;

        [ObservableProperty]
        private ObservableCollection<SettingsSectionViewModel> _sections;

        [ObservableProperty]
        private SettingsSectionViewModel? _selectedSection;

        [ObservableProperty]
        private string _searchText = "";

        partial void OnSearchTextChanged(string value)
        {
            FilterSections();
        }

        public ModernSettingsViewModel(
            GeneralSectionViewModel general,
            ConnectionSectionViewModel connection,
            SearchSectionViewModel search,
            ResultsSectionViewModel results,
            EditorSectionViewModel editor,
            CleanSqlSectionViewModel cleanSql,
            IntegrationSectionViewModel integration,
            SystemSectionViewModel system)
        {
            _allSections = new ObservableCollection<SettingsSectionViewModel>
            {
                general,
                connection,
                search,
                results,
                editor,
                cleanSql,
                integration,
                system
            };

            Sections = new ObservableCollection<SettingsSectionViewModel>(_allSections);
            SelectedSection = Sections[0];
        }

        private void FilterSections()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                if (Sections.Count != _allSections.Count)
                {
                    Sections.Clear();
                    foreach (var section in _allSections) Sections.Add(section);
                }

                if (SelectedSection == null && Sections.Count > 0) SelectedSection = Sections[0];
                return;
            }

            var matching = _allSections.Where(s => s.Matches(SearchText)).ToList();

            Sections.Clear();
            foreach(var section in matching) Sections.Add(section);

            if (Sections.Count > 0 && (SelectedSection == null || !Sections.Contains(SelectedSection)))
            {
                SelectedSection = Sections[0];
            }
        }
    }
}
