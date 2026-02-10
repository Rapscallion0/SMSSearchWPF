using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Utils;
using System.Collections.ObjectModel;
using System.Linq;

namespace SMS_Search.ViewModels
{
    public partial class CleanSqlRuleViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pattern;

        [ObservableProperty]
        private string _replacement;
    }

    public partial class CleanSqlSettingsViewModel : ObservableObject
    {
        private readonly IConfigService _config;

        public CleanSqlSettingsViewModel(IConfigService config)
        {
            _config = config;
            Load();
        }

        [ObservableProperty]
        private ObservableCollection<CleanSqlRuleViewModel> _rules = new ObservableCollection<CleanSqlRuleViewModel>();

        [ObservableProperty]
        private CleanSqlRuleViewModel _selectedRule;

        public void Load()
        {
            Rules.Clear();
            string countStr = _config.GetValue("CLEAN_SQL", "Count");
            if (int.TryParse(countStr, out int count) && count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    string pattern = _config.GetValue("CLEAN_SQL", "Rule_" + i + "_Regex");
                    string replacement = _config.GetValue("CLEAN_SQL", "Rule_" + i + "_Replace");
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        Rules.Add(new CleanSqlRuleViewModel { Pattern = pattern, Replacement = replacement });
                    }
                }
            }
            else
            {
                // Default
                foreach (var rule in SqlCleaner.DefaultRules)
                {
                    Rules.Add(new CleanSqlRuleViewModel { Pattern = rule.Pattern, Replacement = rule.Replacement });
                }
            }
        }

        public void Save()
        {
            _config.SetValue("CLEAN_SQL", "Count", Rules.Count.ToString());
            for (int i = 0; i < Rules.Count; i++)
            {
                _config.SetValue("CLEAN_SQL", "Rule_" + i + "_Regex", Rules[i].Pattern);
                _config.SetValue("CLEAN_SQL", "Rule_" + i + "_Replace", Rules[i].Replacement);
            }
        }

        [RelayCommand]
        private void AddRule()
        {
            var rule = new CleanSqlRuleViewModel { Pattern = "New Pattern", Replacement = "" };
            Rules.Add(rule);
            SelectedRule = rule;
        }

        [RelayCommand]
        private void RemoveRule()
        {
            if (SelectedRule != null)
            {
                Rules.Remove(SelectedRule);
            }
        }

        [RelayCommand]
        private void RestoreDefaults()
        {
             Rules.Clear();
             foreach (var rule in SqlCleaner.DefaultRules)
             {
                 Rules.Add(new CleanSqlRuleViewModel { Pattern = rule.Pattern, Replacement = rule.Replacement });
             }
        }
    }
}
