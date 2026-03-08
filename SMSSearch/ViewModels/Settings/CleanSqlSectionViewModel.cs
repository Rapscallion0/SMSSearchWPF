using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels.Settings
{
    public partial class CleanSqlRuleViewModel : ObservableObject
    {
        private string _pattern = "";

        public string Pattern
        {
            get => _pattern;
            set => SetProperty(ref _pattern, value ?? "");
        }

        private string _replacement = "";

        public string Replacement
        {
            get => _replacement;
            set => SetProperty(ref _replacement, value ?? "");
        }

        [ObservableProperty]
        private bool _isSaved;
    }

    public partial class CleanSqlSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly ILoggerService _logger;
        private bool _isLoading;
        private readonly HashSet<CleanSqlRuleViewModel> _modifiedRules = new();

        public override string Title => "Clean SQL";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_CleanSql");

        [ObservableProperty]
        private ObservableCollection<CleanSqlRuleViewModel> _rules = new ObservableCollection<CleanSqlRuleViewModel>();

        [ObservableProperty]
        private CleanSqlRuleViewModel? _selectedRule;

        [ObservableProperty]
        private bool _isSaved;

        [ObservableProperty]
        private bool _isSaving;

        [ObservableProperty]
        private ObservableSetting<bool> _beautifySql;

        [ObservableProperty]
        private ObservableSetting<int> _indentStringSpaces;

        [ObservableProperty]
        private ObservableSetting<bool> _expandCommaLists;

        [ObservableProperty]
        private ObservableSetting<bool> _expandBooleanExpressions;

        [ObservableProperty]
        private ObservableSetting<bool> _expandCaseExpressions;

        [ObservableProperty]
        private ObservableSetting<bool> _expandBetweenConditions;

        [ObservableProperty]
        private ObservableSetting<bool> _breakJoinOnSections;

        [ObservableProperty]
        private ObservableSetting<bool> _uppercaseKeywords;

        [ObservableProperty]
        private ObservableSetting<bool> _enableKeywordStandardization;

        public CleanSqlSectionViewModel(ISettingsRepository repository, ILoggerService logger)
        {
            _repository = repository;
            _logger = logger;

            Rules.CollectionChanged += OnRulesCollectionChanged;

            Load();
        }

        private void Load()
        {
            _isLoading = true;
            try
            {
                // Formatter Options
                bool beautifyDefault = _repository.GetValue("CLEAN_SQL", "BEAUTIFY_SQL") != "0"; // Default true
                BeautifySql = new ObservableSetting<bool>(_repository, "CLEAN_SQL", "BEAUTIFY_SQL", beautifyDefault, v => v ? "1" : "0");

                int indentSpacesDefault = int.TryParse(_repository.GetValue("CLEAN_SQL", "INDENT_STRING_SPACES"), out int val) ? val : 2;
                IndentStringSpaces = new ObservableSetting<int>(_repository, "CLEAN_SQL", "INDENT_STRING_SPACES", indentSpacesDefault, v => v.ToString(), v => v >= 0);

                bool expandCommaListsDefault = _repository.GetValue("CLEAN_SQL", "EXPAND_COMMA_LISTS") != null ? _repository.GetValue("CLEAN_SQL", "EXPAND_COMMA_LISTS") == "1" : true;
                ExpandCommaLists = new ObservableSetting<bool>(_repository, "CLEAN_SQL", "EXPAND_COMMA_LISTS", expandCommaListsDefault, v => v ? "1" : "0");

                bool expandBooleanExpressionsDefault = _repository.GetValue("CLEAN_SQL", "EXPAND_BOOLEAN_EXPRESSIONS") != null ? _repository.GetValue("CLEAN_SQL", "EXPAND_BOOLEAN_EXPRESSIONS") == "1" : true;
                ExpandBooleanExpressions = new ObservableSetting<bool>(_repository, "CLEAN_SQL", "EXPAND_BOOLEAN_EXPRESSIONS", expandBooleanExpressionsDefault, v => v ? "1" : "0");

                bool expandCaseExpressionsDefault = _repository.GetValue("CLEAN_SQL", "EXPAND_CASE_EXPRESSIONS") != null ? _repository.GetValue("CLEAN_SQL", "EXPAND_CASE_EXPRESSIONS") == "1" : true;
                ExpandCaseExpressions = new ObservableSetting<bool>(_repository, "CLEAN_SQL", "EXPAND_CASE_EXPRESSIONS", expandCaseExpressionsDefault, v => v ? "1" : "0");

                bool expandBetweenConditionsDefault = _repository.GetValue("CLEAN_SQL", "EXPAND_BETWEEN_CONDITIONS") != null ? _repository.GetValue("CLEAN_SQL", "EXPAND_BETWEEN_CONDITIONS") == "1" : true;
                ExpandBetweenConditions = new ObservableSetting<bool>(_repository, "CLEAN_SQL", "EXPAND_BETWEEN_CONDITIONS", expandBetweenConditionsDefault, v => v ? "1" : "0");

                bool breakJoinOnSectionsDefault = _repository.GetValue("CLEAN_SQL", "BREAK_JOIN_ON_SECTIONS") != null ? _repository.GetValue("CLEAN_SQL", "BREAK_JOIN_ON_SECTIONS") == "1" : false;
                BreakJoinOnSections = new ObservableSetting<bool>(_repository, "CLEAN_SQL", "BREAK_JOIN_ON_SECTIONS", breakJoinOnSectionsDefault, v => v ? "1" : "0");

                bool uppercaseKeywordsDefault = _repository.GetValue("CLEAN_SQL", "UPPERCASE_KEYWORDS") != null ? _repository.GetValue("CLEAN_SQL", "UPPERCASE_KEYWORDS") == "1" : true;
                UppercaseKeywords = new ObservableSetting<bool>(_repository, "CLEAN_SQL", "UPPERCASE_KEYWORDS", uppercaseKeywordsDefault, v => v ? "1" : "0");

                bool keywordStandardizationDefault = _repository.GetValue("CLEAN_SQL", "KEYWORD_STANDARDIZATION") != null ? _repository.GetValue("CLEAN_SQL", "KEYWORD_STANDARDIZATION") == "1" : false;
                EnableKeywordStandardization = new ObservableSetting<bool>(_repository, "CLEAN_SQL", "KEYWORD_STANDARDIZATION", keywordStandardizationDefault, v => v ? "1" : "0");

                Rules.Clear();
                string? countStr = _repository.GetValue("CLEAN_SQL", "Count");
                if (int.TryParse(countStr, out int count) && count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        string? pattern = _repository.GetValue("CLEAN_SQL", "Rule_" + i + "_Regex");
                        string? replacement = _repository.GetValue("CLEAN_SQL", "Rule_" + i + "_Replace");
                        if (!string.IsNullOrEmpty(pattern))
                        {
                            Rules.Add(new CleanSqlRuleViewModel { Pattern = pattern!, Replacement = replacement ?? "" });
                        }
                    }
                }
                else
                {
                    // Default
                    foreach (var rule in SqlCleaner.DefaultRules)
                    {
                        Rules.Add(new CleanSqlRuleViewModel { Pattern = rule.Pattern ?? "", Replacement = rule.Replacement ?? "" });
                    }
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnRulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CleanSqlRuleViewModel item in e.NewItems)
                {
                    if (item == null) continue;
                    item.PropertyChanged += OnRulePropertyChanged;
                    if (!_isLoading) _modifiedRules.Add(item); // Mark new items as modified
                }
            }
            if (e.OldItems != null)
            {
                foreach (CleanSqlRuleViewModel item in e.OldItems)
                {
                    if (item == null) continue;
                    item.PropertyChanged -= OnRulePropertyChanged;
                }
            }
        }

        private void OnRulePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
             if (e.PropertyName == nameof(CleanSqlRuleViewModel.IsSaved)) return;

             if (sender is CleanSqlRuleViewModel rule)
             {
                 if (!_isLoading)
                 {
                     _modifiedRules.Add(rule);
                     rule.IsSaved = false;
                 }
             }
        }

        [RelayCommand]
        public async Task SaveRules()
        {
            if (IsSaving) return;
            IsSaving = true;
            IsSaved = false;

            try
            {
                // Clear existing section first to remove stale keys
                _repository.ClearSection("CLEAN_SQL");

                // Save Logic
                _repository.SetValue("CLEAN_SQL", "Count", Rules.Count.ToString());
                for (int i = 0; i < Rules.Count; i++)
                {
                    var rule = Rules[i];
                    if (rule == null) continue;

                    _repository.SetValue("CLEAN_SQL", "Rule_" + i + "_Regex", rule.Pattern ?? "");
                    _repository.SetValue("CLEAN_SQL", "Rule_" + i + "_Replace", rule.Replacement ?? "");
                }

                // Force commit to disk
                await _repository.SaveAsync();
                _logger.LogInfo($"Saved {Rules.Count} clean SQL rules.");

                IsSaving = false;
                IsSaved = true;

                // Flash modified rules
                var rulesToFlash = _modifiedRules.ToList();
                _modifiedRules.Clear();

                foreach (var rule in rulesToFlash)
                {
                    if (rule == null) continue;
                    rule.IsSaved = true;
                }

                await Task.Delay(2000);
                IsSaved = false;
                foreach (var rule in rulesToFlash)
                {
                    if (rule == null) continue;
                    rule.IsSaved = false;
                }
            }
            catch (Exception ex)
            {
                // Prevent crash
                IsSaving = false;
                _logger.LogError("Failed to save clean SQL rules", ex);
            }
        }

        [RelayCommand]
        private void AddRule()
        {
            _logger.LogInfo("Adding new clean SQL rule.");
            var rule = new CleanSqlRuleViewModel { Pattern = "", Replacement = "" };
            Rules.Add(rule);
            SelectedRule = rule;
        }

        [RelayCommand]
        private async Task RemoveRule(object? parameter)
        {
            var rule = parameter as CleanSqlRuleViewModel;
            if (rule == null && parameter == null) rule = SelectedRule;

            if (rule != null)
            {
                _logger.LogInfo($"Removing clean SQL rule: {rule.Pattern}");
                Rules.Remove(rule);
                _modifiedRules.Remove(rule); // Ensure we don't try to access removed rule
                await SaveRules();
            }
        }

        [RelayCommand]
        private async Task RestoreDefaults()
        {
             _logger.LogInfo("Restoring default clean SQL rules.");
             _isLoading = true; // Prevent save during clear/add cycle
             try
             {
                 // Remove listeners from current items
                 foreach(var rule in Rules) rule.PropertyChanged -= OnRulePropertyChanged;
                 Rules.Clear();
                 _modifiedRules.Clear();

                 foreach (var rule in SqlCleaner.DefaultRules)
                 {
                     var vm = new CleanSqlRuleViewModel { Pattern = rule.Pattern ?? "", Replacement = rule.Replacement ?? "" };
                     Rules.Add(vm);
                     _modifiedRules.Add(vm);
                 }
             }
             finally
             {
                 _isLoading = false;
             }
             // Trigger save explicitly after restore
             await SaveRules();
        }

        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             if ("Regex".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Rule".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Pattern".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Replace".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Beautify".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Uppercase".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Keyword".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Expand".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Indent".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
