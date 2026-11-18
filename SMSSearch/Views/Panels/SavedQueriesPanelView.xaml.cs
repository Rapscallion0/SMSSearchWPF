using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;

namespace SMS_Search.Views.Panels
{
    public partial class SavedQueriesPanelView : System.Windows.Controls.UserControl
    {
        private const string AllCategoriesLabel = "All Categories";
        private const string AllTypesLabel = "All";
        private const string SqlTypeLabel = "SQL";
        private const string AdvTypeLabel = "ADV";

        private ISavedQueriesService? _savedQueriesService;
        private IDialogService? _dialogService;
        private List<SavedQueryItem> _allItems = new();
        private bool _isUpdatingCategories;
        private bool _initialized;

        public SavedQueriesPanelView()
        {
            InitializeComponent();
            Loaded += SavedQueriesPanelView_Loaded;
            Unloaded += SavedQueriesPanelView_Unloaded;
        }

        private void SavedQueriesPanelView_Unloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void SavedQueriesPanelView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                WeakReferenceMessenger.Default.Register<SavedQueriesChangedMessage>(this, (r, m) => LoadItems());
                return;
            }

            var services = (System.Windows.Application.Current as App)?.Services;
            _savedQueriesService = services?.GetService<ISavedQueriesService>();
            _dialogService = services?.GetService<IDialogService>();

            if (_savedQueriesService == null) return;
            _initialized = true;

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

            WeakReferenceMessenger.Default.Register<SavedQueriesChangedMessage>(this, (r, m) => LoadItems());

            LoadItems();
        }

        private void LoadSqlIntoEditor(string sql)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SearchVm.CustomSqlText = sql;
                WeakReferenceMessenger.Default.Send(new ShowPanelMessage("SqlEditor"));
            }
        }

        private void LoadItems()
        {
            if (_savedQueriesService == null) return;
            _allItems = _savedQueriesService.GetAll();
            RefreshCategories();
            ApplyFilter();
        }

        private void RefreshCategories()
        {
            if (_savedQueriesService == null) return;

            _isUpdatingCategories = true;
            try
            {
                var categories = _savedQueriesService.GetCategories();
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
                filtered = filtered.Where(x => x.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase));
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
                LoadButton.Content = item.IsAdvanced ? "Load into Search" : "Load into SQL Editor";
                LoadButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
            }
            else
            {
                PreviewTextBox.Text = string.Empty;
                LoadButton.Content = "Load into SQL Editor";
                LoadButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void FilterTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingCategories) ApplyFilter();
        }

        private void FilterCategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingCategories) ApplyFilter();
        }

        private void QueriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionState();

        private void QueriesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QueriesListBox.SelectedItem is SavedQueryItem) LoadSelectedQuery();
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e) => LoadSelectedQuery();

        private void LoadSelectedQuery()
        {
            if (_savedQueriesService == null || QueriesListBox.SelectedItem is not SavedQueryItem item) return;
            _savedQueriesService.MarkUsed(item.Id);

            if (item.IsAdvanced && item.AdvancedQuery != null && DataContext is MainViewModel vm)
            {
                vm.SearchVm.LoadAdvancedQueryDefinition(item.AdvancedQuery);
                WeakReferenceMessenger.Default.Send(new ShowPanelMessage("SearchCriteria"));
            }
            else
            {
                LoadSqlIntoEditor(item.Sql);
            }

            _dialogService?.ShowToast($"Loaded '{item.Name}'", "Saved Queries", ToastType.Info);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_savedQueriesService == null || _dialogService == null || QueriesListBox.SelectedItem is not SavedQueryItem item) return;
            if (_dialogService.ShowConfirmation($"Delete saved query '{item.Name}'?", "Confirm Delete"))
            {
                _savedQueriesService.Delete(item.Id);
                LoadItems();
            }
        }
    }
}
