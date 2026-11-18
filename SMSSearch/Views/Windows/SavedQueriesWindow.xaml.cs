using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMS_Search.Data;
using SMS_Search.Services;

namespace SMS_Search.Views.Windows
{
    public partial class SavedQueriesWindow : Window
    {
        private const string AllCategoriesLabel = "All Categories";
        private const string AllTypesLabel = "All";
        private const string SqlTypeLabel = "SQL";
        private const string AdvTypeLabel = "ADV";

        private readonly ISavedQueriesService _savedQueriesService;
        private readonly IDialogService _dialogService;
        private readonly Func<string> _getCurrentSql;
        private readonly Action<string> _onLoadSql;
        private List<SavedQueryItem> _allItems = new();
        private bool _isUpdatingCategories;
        private SavedQueryItem? _bannerDuplicateMatch;

        public SavedQueriesWindow(
            ISavedQueriesService savedQueriesService,
            IDialogService dialogService,
            Func<string> getCurrentSql,
            Action<string> onLoadSql)
        {
            InitializeComponent();
            _savedQueriesService = savedQueriesService;
            _dialogService = dialogService;
            _getCurrentSql = getCurrentSql;
            _onLoadSql = onLoadSql;

            Activated += (_, _) => UpdateDuplicateBanner();

            _isUpdatingCategories = true;
            try
            {
                FilterTypeCombo.ItemsSource = new[] { AllTypesLabel, SqlTypeLabel, AdvTypeLabel };
                FilterTypeCombo.SelectedIndex = 0;
            }
            finally
            {
                _isUpdatingCategories = false;
            }

            LoadItems();
            SuggestNameFromEditor();
            UpdateDuplicateBanner();
        }

        public void RefreshSuggestedNameIfEmpty()
        {
            if (string.IsNullOrWhiteSpace(SaveNameBox.Text))
            {
                SuggestNameFromEditor();
            }
            UpdateDuplicateBanner();
        }

        private void SuggestNameFromEditor()
        {
            string sql = _getCurrentSql();
            string suggested = SavedQueriesService.SuggestName(sql);
            if (!string.IsNullOrEmpty(suggested))
            {
                SaveNameBox.Text = suggested;
            }
        }

        private void LoadItems()
        {
            _allItems = _savedQueriesService.GetAll();
            RefreshCategories();
            ApplyFilter();
            UpdateDuplicateBanner();
        }

        private void UpdateDuplicateBanner()
        {
            string currentSql = _getCurrentSql();
            if (string.IsNullOrWhiteSpace(currentSql))
            {
                _bannerDuplicateMatch = null;
                DuplicateBanner.Visibility = Visibility.Collapsed;
                return;
            }

            var matches = _savedQueriesService.FindDuplicatesBySql(currentSql);
            if (matches.Count == 0)
            {
                _bannerDuplicateMatch = null;
                DuplicateBanner.Visibility = Visibility.Collapsed;
                return;
            }

            _bannerDuplicateMatch = matches[0];
            bool exactMatch = string.Equals(_bannerDuplicateMatch.Sql, currentSql, StringComparison.Ordinal);
            string matchDesc = exactMatch
                ? $"Matches saved query: '{_bannerDuplicateMatch.Name}' ({_bannerDuplicateMatch.Category})"
                : $"Matches saved query (different formatting): '{_bannerDuplicateMatch.Name}' ({_bannerDuplicateMatch.Category})";

            if (matches.Count > 1)
            {
                matchDesc += $" (+{matches.Count - 1} more)";
            }

            DuplicateBannerText.Text = matchDesc;
            DuplicateBanner.Visibility = Visibility.Visible;
        }

        private void JumpToSavedQuery(SavedQueryItem target)
        {
            var currentList = QueriesListBox.ItemsSource as IEnumerable<SavedQueryItem>;
            var matchInList = currentList?.FirstOrDefault(x => x.Id == target.Id);

            if (matchInList == null)
            {
                _isUpdatingCategories = true;
                try
                {
                    FilterBox.Text = string.Empty;
                    FilterCategoryCombo.SelectedIndex = 0;
                }
                finally
                {
                    _isUpdatingCategories = false;
                }
                ApplyFilter();
                currentList = QueriesListBox.ItemsSource as IEnumerable<SavedQueryItem>;
                matchInList = currentList?.FirstOrDefault(x => x.Id == target.Id);
            }

            if (matchInList != null)
            {
                QueriesListBox.SelectedItem = matchInList;
                QueriesListBox.ScrollIntoView(matchInList);
            }
        }

        private void SelectDuplicateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_bannerDuplicateMatch != null)
            {
                JumpToSavedQuery(_bannerDuplicateMatch);
            }
        }

        private void RefreshCategories()
        {
            _isUpdatingCategories = true;
            try
            {
                var categories = _savedQueriesService.GetCategories();

                string currentSaveCat = SaveCategoryCombo.Text;
                SaveCategoryCombo.ItemsSource = categories;
                SaveCategoryCombo.Text = string.IsNullOrWhiteSpace(currentSaveCat) ? "General" : currentSaveCat;

                string? previousFilter = FilterCategoryCombo.SelectedItem as string;
                var filterList = new List<string> { AllCategoriesLabel };
                filterList.AddRange(categories);
                FilterCategoryCombo.ItemsSource = filterList;

                if (!string.IsNullOrEmpty(previousFilter) && filterList.Contains(previousFilter))
                {
                    FilterCategoryCombo.SelectedItem = previousFilter;
                }
                else
                {
                    FilterCategoryCombo.SelectedIndex = 0;
                }
            }
            finally
            {
                _isUpdatingCategories = false;
            }
        }

        private void ApplyFilter()
        {
            string filter = FilterBox.Text?.Trim() ?? string.Empty;
            string? selectedCategory = FilterCategoryCombo.SelectedItem as string;
            string? selectedType = FilterTypeCombo.SelectedItem as string;

            IEnumerable<SavedQueryItem> filtered = _allItems;

            if (selectedType == SqlTypeLabel)
            {
                filtered = filtered.Where(x => !x.IsAdvanced);
            }
            else if (selectedType == AdvTypeLabel)
            {
                filtered = filtered.Where(x => x.IsAdvanced);
            }

            if (!string.IsNullOrEmpty(selectedCategory) && selectedCategory != AllCategoriesLabel)
            {
                filtered = filtered.Where(x =>
                    x.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filter))
            {
                filtered = filtered.Where(x =>
                    x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    x.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    x.QueryKindLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    x.Sql.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            var list = filtered.ToList();
            QueriesListBox.ItemsSource = list;
            if (list.Count > 0 && QueriesListBox.SelectedItem == null)
            {
                QueriesListBox.SelectedIndex = 0;
            }
            else
            {
                UpdateSelectionState();
            }
        }

        private void UpdateSelectionState()
        {
            if (QueriesListBox.SelectedItem is SavedQueryItem item)
            {
                PreviewTextBox.Text = item.Sql;
                LoadButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
            }
            else
            {
                PreviewTextBox.Text = string.Empty;
                LoadButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void FilterTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingCategories)
            {
                ApplyFilter();
            }
        }

        private void FilterCategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingCategories)
            {
                ApplyFilter();
            }
        }

        private void QueriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectionState();
        }

        private void QueriesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QueriesListBox.SelectedItem is SavedQueryItem)
            {
                LoadSelectedQuery();
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSelectedQuery();
        }

        private void LoadSelectedQuery()
        {
            if (QueriesListBox.SelectedItem is not SavedQueryItem item) return;
            _savedQueriesService.MarkUsed(item.Id);
            if (item.IsAdvanced && item.AdvancedQuery != null &&
                System.Windows.Application.Current?.MainWindow?.DataContext is ViewModels.MainViewModel vm)
            {
                vm.SearchVm.LoadAdvancedQueryDefinition(item.AdvancedQuery);
                WeakReferenceMessenger.Default.Send(new Utils.ShowPanelMessage("SearchCriteria"));
            }
            else
            {
                _onLoadSql(item.Sql);
            }
            _dialogService.ShowToast($"Loaded '{item.Name}'", "Saved Queries", ToastType.Info);
        }

        private void SuggestNameButton_Click(object sender, RoutedEventArgs e)
        {
            string sql = _getCurrentSql();
            if (string.IsNullOrWhiteSpace(sql))
            {
                UpdateDuplicateBanner();
                _dialogService.ShowWarning("The active SQL editor is empty.", "Suggest Name");
                return;
            }

            SaveNameBox.Text = SavedQueriesService.SuggestName(sql);
            UpdateDuplicateBanner();
        }

        private void GrabEditorButton_Click(object sender, RoutedEventArgs e)
        {
            string sql = _getCurrentSql();
            UpdateDuplicateBanner();

            if (string.IsNullOrWhiteSpace(sql))
            {
                _dialogService.ShowWarning("The active SQL editor is empty.", "Grab from Editor");
                return;
            }

            SaveNameBox.Text = SavedQueriesService.SuggestName(sql);
            _dialogService.ShowToast("Grabbed SQL from active editor", "Saved Queries", ToastType.Info);
        }

        private void SaveCurrentButton_Click(object sender, RoutedEventArgs e)
        {
            string currentSql = _getCurrentSql();
            if (string.IsNullOrWhiteSpace(currentSql))
            {
                _dialogService.ShowWarning("The active SQL editor is currently empty.", "Save Query");
                return;
            }

            string name = SaveNameBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                name = SavedQueriesService.SuggestName(currentSql);
                if (string.IsNullOrEmpty(name))
                {
                    _dialogService.ShowWarning("Please enter a name for this query.", "Save Query");
                    SaveNameBox.Focus();
                    return;
                }
            }

            string category = SaveCategoryCombo.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(category))
            {
                category = "General";
            }

            // 1. Check Name + Category collision
            var nameMatch = _savedQueriesService.FindDuplicateByNameAndCategory(name, category);
            if (nameMatch != null)
            {
                JumpToSavedQuery(nameMatch);

                if (string.Equals(nameMatch.Sql, currentSql, StringComparison.Ordinal))
                {
                    _dialogService.ShowMessage(
                        $"This exact query is already saved as '{nameMatch.Name}' in '{nameMatch.Category}'.",
                        "Duplicate Query");
                    return;
                }

                bool sameNormalized = SavedQueriesService.NormalizeSql(nameMatch.Sql)
                    .Equals(SavedQueriesService.NormalizeSql(currentSql), StringComparison.OrdinalIgnoreCase);

                if (sameNormalized)
                {
                    var diffDlg = new QueryDiffWindow(
                        $"'{nameMatch.Name}' ({nameMatch.Category}) already exists with different formatting. Choose which formatting to keep:",
                        nameMatch.Sql,
                        currentSql,
                        allowSaveAsNewCopy: false)
                    {
                        Owner = this
                    };
                    diffDlg.ShowDialog();
                    if (diffDlg.Resolution != QueryDiffResolution.UseNewFormatting)
                    {
                        return;
                    }
                }
                else
                {
                    if (!_dialogService.ShowConfirmation(
                        $"A saved query named '{nameMatch.Name}' already exists in '{nameMatch.Category}' with different SQL.\n\nDo you want to overwrite it?",
                        "Overwrite Saved Query"))
                    {
                        return;
                    }
                }

                var updated = _savedQueriesService.Save(nameMatch.Name, nameMatch.Category, currentSql);
                SaveNameBox.Text = string.Empty;
                LoadItems();
                JumpToSavedQuery(updated);
                _dialogService.ShowToast($"Updated '{updated.Name}' ({updated.Category})", "Saved Queries", ToastType.Success);
                return;
            }

            // 2. Check Duplicate SQL content across other names/categories
            var sqlMatches = _savedQueriesService.FindDuplicatesBySql(currentSql);
            if (sqlMatches.Count > 0)
            {
                var firstMatch = sqlMatches[0];
                JumpToSavedQuery(firstMatch);

                if (string.Equals(firstMatch.Sql, currentSql, StringComparison.Ordinal))
                {
                    if (!_dialogService.ShowConfirmation(
                        $"This exact SQL is already saved as '{firstMatch.Name}' in '{firstMatch.Category}'.\n\nSave a duplicate copy anyway?",
                        "Duplicate SQL Detected"))
                    {
                        return;
                    }
                }
                else
                {
                    var diffDlg = new QueryDiffWindow(
                        $"A matching query is already saved as '{firstMatch.Name}' ({firstMatch.Category}) with different formatting:",
                        firstMatch.Sql,
                        currentSql,
                        allowSaveAsNewCopy: true)
                    {
                        Owner = this
                    };
                    diffDlg.ShowDialog();

                    if (diffDlg.Resolution == QueryDiffResolution.KeepExisting)
                    {
                        return;
                    }
                    else if (diffDlg.Resolution == QueryDiffResolution.UseNewFormatting)
                    {
                        var updatedExisting = _savedQueriesService.Save(firstMatch.Name, firstMatch.Category, currentSql);
                        SaveNameBox.Text = string.Empty;
                        LoadItems();
                        JumpToSavedQuery(updatedExisting);
                        _dialogService.ShowToast($"Updated formatting for '{updatedExisting.Name}' ({updatedExisting.Category})", "Saved Queries", ToastType.Success);
                        return;
                    }
                    // SaveAsNewCopy falls through to save under new name/category
                }
            }

            var saved = _savedQueriesService.Save(name, category, currentSql);
            SaveNameBox.Text = string.Empty;
            LoadItems();
            JumpToSavedQuery(saved);
            _dialogService.ShowToast($"Saved '{saved.Name}' ({saved.Category})", "Saved Queries", ToastType.Success);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (QueriesListBox.SelectedItem is not SavedQueryItem item) return;
            if (_dialogService.ShowConfirmation($"Delete saved query '{item.Name}'?", "Confirm Delete"))
            {
                _savedQueriesService.Delete(item.Id);
                LoadItems();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
