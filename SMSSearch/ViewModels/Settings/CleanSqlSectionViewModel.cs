using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
    }

    public partial class CleanSqlSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private CancellationTokenSource? _saveCts;
        private bool _isLoading;

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

                await Task.Delay(2000, token);
                IsSaved = false;
            }
            catch (OperationCanceledException) { }
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
             _isLoading = true; // Prevent save during clear/add cycle
             try
             {
                 // Remove listeners from current items
                 foreach(var rule in Rules) rule.PropertyChanged -= OnRulePropertyChanged;
                 Rules.Clear();

                 foreach (var rule in SqlCleaner.DefaultRules)
                 {
                     Rules.Add(new CleanSqlRuleViewModel { Pattern = rule.Pattern ?? "", Replacement = rule.Replacement ?? "" });
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
