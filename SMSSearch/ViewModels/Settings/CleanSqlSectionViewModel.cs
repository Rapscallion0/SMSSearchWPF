using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;

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
        private bool _isLoading;
        private readonly HashSet<CleanSqlRuleViewModel> _modifiedRules = new();

        public override string Title => "Clean SQL";
        public override Geometry Icon => (Geometry)Application.Current.FindResource("Icon_Nav_CleanSql");

        [ObservableProperty]
        private ObservableCollection<CleanSqlRuleViewModel> _rules = new ObservableCollection<CleanSqlRuleViewModel>();

        [ObservableProperty]
        private CleanSqlRuleViewModel? _selectedRule;

        [ObservableProperty]
        private bool _isSaved;

        [ObservableProperty]
        private bool _isSaving;

        public CleanSqlSectionViewModel(ISettingsRepository repository)
        {
            _repository = repository;

            Rules.CollectionChanged += OnRulesCollectionChanged;

            Load();
        }

        private void Load()
        {
            _isLoading = true;
            try
            {
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
                System.Diagnostics.Debug.WriteLine($"Error in SaveRules: {ex}");
            }
        }

        [RelayCommand]
        private void AddRule()
        {
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
                Rules.Remove(rule);
                _modifiedRules.Remove(rule); // Ensure we don't try to access removed rule
                await SaveRules();
            }
        }

        [RelayCommand]
        private async Task RestoreDefaults()
        {
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
    }
}
