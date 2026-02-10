using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;

namespace SMS_Search.ViewModels.Settings
{
    public partial class CleanSqlRuleViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pattern = "";

        [ObservableProperty]
        private string _replacement = "";

        [ObservableProperty]
        private bool _isSaved;
    }

    public partial class CleanSqlSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private CancellationTokenSource? _saveCts;
        private bool _isLoading;
        private readonly HashSet<CleanSqlRuleViewModel> _modifiedRules = new();

        public override string Title => "Clean SQL";
        public override string IconData => "M14,10H2V12H14M14,6H2V8H14M2,16H10V14H2M21.5,11.5L23,13L16,20L11.5,15.5L13,14L16,17L21.5,11.5Z"; // Check list or similar

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
                    item.PropertyChanged += OnRulePropertyChanged;
                    if (!_isLoading) _modifiedRules.Add(item); // Mark new items as modified
                }
            }
            if (e.OldItems != null)
            {
                foreach (CleanSqlRuleViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnRulePropertyChanged;
                }
            }

            if (!_isLoading) DebounceSave();
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
             if (!_isLoading) DebounceSave();
        }

        private async void DebounceSave()
        {
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;

            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                IsSaving = true;
                IsSaved = false;

                // Save Logic
                await _repository.SaveAsync("CLEAN_SQL", "Count", Rules.Count.ToString());
                for (int i = 0; i < Rules.Count; i++)
                {
                    await _repository.SaveAsync("CLEAN_SQL", "Rule_" + i + "_Regex", Rules[i].Pattern);
                    await _repository.SaveAsync("CLEAN_SQL", "Rule_" + i + "_Replace", Rules[i].Replacement);
                }

                if (token.IsCancellationRequested) return;

                IsSaving = false;
                IsSaved = true;

                // Flash modified rules
                var rulesToFlash = _modifiedRules.ToList();
                foreach (var rule in rulesToFlash)
                {
                    rule.IsSaved = true;
                }
                _modifiedRules.Clear();

                await Task.Delay(2000, token);
                IsSaved = false;
                foreach (var rule in rulesToFlash)
                {
                    rule.IsSaved = false;
                }
            }
            catch (OperationCanceledException) { }
        }

        [RelayCommand]
        private void AddRule()
        {
            var rule = new CleanSqlRuleViewModel { Pattern = "", Replacement = "" };
            Rules.Add(rule);
            SelectedRule = rule;
        }

        [RelayCommand]
        private void RemoveRule(CleanSqlRuleViewModel? rule)
        {
            if (rule == null) rule = SelectedRule;
            if (rule != null)
            {
                Rules.Remove(rule);
                _modifiedRules.Remove(rule); // Ensure we don't try to access removed rule
            }
        }

        [RelayCommand]
        private void RestoreDefaults()
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
                     _modifiedRules.Add(vm); // Mark restored defaults as modified so they save? Or not?
                     // If we restore, we probably want to save immediately.
                 }
             }
             finally
             {
                 _isLoading = false;
             }
             // Trigger save explicitly after restore
             DebounceSave();
        }
    }
}
