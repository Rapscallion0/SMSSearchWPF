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
        private ObservableSetting<bool> _beautifySql = null!;

        [ObservableProperty]
        private ObservableSetting<int> _indentStringSpaces = null!;

        [ObservableProperty]
        private ObservableSetting<bool> _expandCommaLists = null!;

        [ObservableProperty]
        private ObservableSetting<bool> _expandBooleanExpressions = null!;

        [ObservableProperty]
        private ObservableSetting<bool> _expandCaseExpressions = null!;

        [ObservableProperty]
        private ObservableSetting<bool> _expandBetweenConditions = null!;

        [ObservableProperty]
        private ObservableSetting<bool> _breakJoinOnSections = null!;

        [ObservableProperty]
        private ObservableSetting<bool> _uppercaseKeywords = null!;

        [ObservableProperty]
        private ObservableSetting<bool> _enableKeywordStandardization = null!;

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
                bool beautifyDefault = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.BeautifySql) != "0"; // Default true
                BeautifySql = new ObservableSetting<bool>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.BeautifySql, beautifyDefault, v => v ? "1" : "0");

                int indentSpacesDefault = int.TryParse(_repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.IndentStringSpaces), out int val) ? val : 2;
                IndentStringSpaces = new ObservableSetting<int>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.IndentStringSpaces, indentSpacesDefault, v => v.ToString(), v => v >= 0);

                bool expandCommaListsDefault = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCommaLists) != null ? _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCommaLists) == "1" : true;
                ExpandCommaLists = new ObservableSetting<bool>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCommaLists, expandCommaListsDefault, v => v ? "1" : "0");

                bool expandBooleanExpressionsDefault = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBooleanExpressions) != null ? _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBooleanExpressions) == "1" : true;
                ExpandBooleanExpressions = new ObservableSetting<bool>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBooleanExpressions, expandBooleanExpressionsDefault, v => v ? "1" : "0");

                bool expandCaseExpressionsDefault = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCaseExpressions) != null ? _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCaseExpressions) == "1" : true;
                ExpandCaseExpressions = new ObservableSetting<bool>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandCaseExpressions, expandCaseExpressionsDefault, v => v ? "1" : "0");

                bool expandBetweenConditionsDefault = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBetweenConditions) != null ? _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBetweenConditions) == "1" : true;
                ExpandBetweenConditions = new ObservableSetting<bool>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.ExpandBetweenConditions, expandBetweenConditionsDefault, v => v ? "1" : "0");

                bool breakJoinOnSectionsDefault = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.BreakJoinOnSections) != null ? _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.BreakJoinOnSections) == "1" : false;
                BreakJoinOnSections = new ObservableSetting<bool>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.BreakJoinOnSections, breakJoinOnSectionsDefault, v => v ? "1" : "0");

                bool uppercaseKeywordsDefault = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.UppercaseKeywords) != null ? _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.UppercaseKeywords) == "1" : true;
                UppercaseKeywords = new ObservableSetting<bool>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.UppercaseKeywords, uppercaseKeywordsDefault, v => v ? "1" : "0");

                bool keywordStandardizationDefault = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.KeywordStandardization) != null ? _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.KeywordStandardization) == "1" : false;
                EnableKeywordStandardization = new ObservableSetting<bool>(_repository, AppSettings.Sections.CleanSql, AppSettings.Keys.KeywordStandardization, keywordStandardizationDefault, v => v ? "1" : "0");

                Rules.Clear();
                string? countStr = _repository.GetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.CleanSqlCount);
                if (int.TryParse(countStr, out int count) && count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        string? pattern = _repository.GetValue(AppSettings.Sections.CleanSql, "Rule_" + i + "_Regex");
                        string? replacement = _repository.GetValue(AppSettings.Sections.CleanSql, "Rule_" + i + "_Replace");
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
                _repository.ClearSection(AppSettings.Sections.CleanSql);

                // Save Logic
                _repository.SetValue(AppSettings.Sections.CleanSql, AppSettings.Keys.CleanSqlCount, Rules.Count.ToString());
                for (int i = 0; i < Rules.Count; i++)
                {
                    var rule = Rules[i];
                    if (rule == null) continue;

                    _repository.SetValue(AppSettings.Sections.CleanSql, "Rule_" + i + "_Regex", rule.Pattern ?? "");
                    _repository.SetValue(AppSettings.Sections.CleanSql, "Rule_" + i + "_Replace", rule.Replacement ?? "");
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
