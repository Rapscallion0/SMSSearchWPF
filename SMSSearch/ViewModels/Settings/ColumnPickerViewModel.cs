using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Models;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels.Settings
{
    public partial class ColumnPickerViewModel : ObservableObject, IDisposable
    {
        private readonly IDataRepository _dataRepository;
        private readonly IConnectionSettingsService _connectionSettingsService;
        private readonly ILoggerService _logger;
        private readonly string _tableName;
        private readonly List<string> _defaultColumns;
        private readonly HashSet<string> _requiredColumns;
        private readonly List<ColumnItem> _allColumns = new();
        private CancellationTokenSource? _loadCts;

        public event Action<bool>? RequestClose;

        [ObservableProperty]
        private string _title = "Select Columns";

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private ColumnItem? _selectedAvailableColumn;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
        private ColumnItem? _selectedChosenColumn;

        public ObservableCollection<ColumnItem> FilteredAvailableColumns { get; } = new();
        public ObservableCollection<ColumnItem> SelectedColumns { get; } = new();

        public ColumnPickerViewModel(
            IDataRepository dataRepository,
            IConnectionSettingsService connectionSettingsService,
            ILoggerService logger,
            string title,
            string tableName,
            string currentColumns,
            string defaultColumns,
            IEnumerable<string>? requiredColumns = null)
        {
            _dataRepository = dataRepository;
            _connectionSettingsService = connectionSettingsService;
            _logger = logger;
            Title = title;
            _tableName = tableName;
            _defaultColumns = ParseColumns(defaultColumns);
            _requiredColumns = new HashSet<string>(requiredColumns ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var col in ParseColumns(currentColumns))
            {
                SelectedColumns.Add(new ColumnItem(col, "", _requiredColumns.Contains(col)));
            }

            EnsureRequiredColumnsPresent();
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        public async Task LoadColumnsAsync()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var conn = _connectionSettingsService.GetConnectionSettings();
                if (string.IsNullOrWhiteSpace(conn.Server) || string.IsNullOrWhiteSpace(conn.Database))
                {
                    ErrorMessage = "Server and Database connection settings must be configured to load available columns.";
                    return;
                }

                string safeTable = _tableName.Replace("[", "").Replace("]", "");
                var schema = await _dataRepository.GetQuerySchemaAsync(
                    conn.Server, conn.Database, conn.User, conn.Password,
                    $"SELECT TOP 0 * FROM [{safeTable}]", null, token);

                var colNames = new List<string>(schema.Columns.Count);
                foreach (DataColumn col in schema.Columns)
                {
                    colNames.Add(col.ColumnName);
                }

                var descriptions = await _dataRepository.GetColumnDescriptionsAsync(
                    conn.Server, conn.Database, conn.User, conn.Password,
                    colNames, token);

                _allColumns.Clear();
                for (int i = 0; i < colNames.Count; i++)
                {
                    string desc = i < descriptions.Count ? descriptions[i] : "";
                    _allColumns.Add(new ColumnItem(colNames[i], desc, _requiredColumns.Contains(colNames[i])));
                }

                EnrichSelectedColumnDescriptions();
                ApplyFilter();
            }
            catch (OperationCanceledException)
            {
                // Expected on close/cancel
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load columns for {TableName}", _tableName);
                ErrorMessage = $"Failed to load columns: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void SetAvailableColumnsForTest(IEnumerable<ColumnItem> columns)
        {
            _allColumns.Clear();
            _allColumns.AddRange(columns);
            EnrichSelectedColumnDescriptions();
            ApplyFilter();
        }

        private void EnsureRequiredColumnsPresent()
        {
            foreach (var req in _requiredColumns)
            {
                if (!SelectedColumns.Any(c => string.Equals(c.Name, req, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedColumns.Insert(0, new ColumnItem(req, "", isRequired: true));
                }
            }
        }

        private void EnrichSelectedColumnDescriptions()
        {
            var lookup = _allColumns.ToDictionary(c => c.Name, c => c.Description, StringComparer.OrdinalIgnoreCase);
            bool hasSchema = _allColumns.Count > 0;
            foreach (var item in SelectedColumns)
            {
                if (lookup.TryGetValue(item.Name, out string? desc))
                {
                    item.IsInvalid = false;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        item.Description = desc;
                    }
                }
                else if (hasSchema)
                {
                    item.IsInvalid = true;
                }
            }
            UpdateInvalidColumnsMessage();
        }

        private void UpdateInvalidColumnsMessage()
        {
            var invalid = SelectedColumns.Where(c => c.IsInvalid).Select(c => c.Name).ToList();
            if (invalid.Count > 0)
            {
                ErrorMessage = $"Invalid column(s) not found in {_tableName}: {string.Join(", ", invalid)}";
            }
            else if (ErrorMessage != null && ErrorMessage.StartsWith("Invalid column(s) not found in ", StringComparison.Ordinal))
            {
                ErrorMessage = null;
            }
        }

        private void ApplyFilter()
        {
            var selectedSet = new HashSet<string>(SelectedColumns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            string query = SearchText?.Trim() ?? "";

            FilteredAvailableColumns.Clear();
            foreach (var col in _allColumns)
            {
                if (selectedSet.Contains(col.Name)) continue;

                if (string.IsNullOrEmpty(query) ||
                    col.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    col.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredAvailableColumns.Add(col);
                }
            }
        }

        [RelayCommand]
        private void AddSelected()
        {
            if (SelectedAvailableColumn == null) return;
            if (!SelectedColumns.Any(c => string.Equals(c.Name, SelectedAvailableColumn.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var item = SelectedAvailableColumn;
                SelectedColumns.Add(item);
                SelectedChosenColumn = item;
                ApplyFilter();
            }
        }

        private bool CanRemoveSelected()
        {
            return SelectedChosenColumn != null && !SelectedChosenColumn.IsRequired;
        }

        [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
        private void RemoveSelected()
        {
            if (SelectedChosenColumn == null || SelectedChosenColumn.IsRequired) return;
            int idx = SelectedColumns.IndexOf(SelectedChosenColumn);
            SelectedColumns.Remove(SelectedChosenColumn);
            if (SelectedColumns.Count > 0)
            {
                SelectedChosenColumn = SelectedColumns[Math.Min(idx, SelectedColumns.Count - 1)];
            }
            UpdateInvalidColumnsMessage();
            ApplyFilter();
        }

        [RelayCommand]
        private void MoveUp()
        {
            if (SelectedChosenColumn == null) return;
            int idx = SelectedColumns.IndexOf(SelectedChosenColumn);
            if (idx > 0)
            {
                MoveColumn(idx, idx - 1);
            }
        }

        [RelayCommand]
        private void MoveDown()
        {
            if (SelectedChosenColumn == null) return;
            int idx = SelectedColumns.IndexOf(SelectedChosenColumn);
            if (idx >= 0 && idx < SelectedColumns.Count - 1)
            {
                MoveColumn(idx, idx + 1);
            }
        }

        public void MoveColumn(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= SelectedColumns.Count ||
                newIndex < 0 || newIndex >= SelectedColumns.Count ||
                oldIndex == newIndex)
            {
                return;
            }

            var item = SelectedColumns[oldIndex];
            SelectedColumns.Move(oldIndex, newIndex);
            SelectedChosenColumn = item;
        }

        [RelayCommand]
        private void ResetDefaults()
        {
            var lookup = _allColumns.ToDictionary(c => c.Name, c => c.Description, StringComparer.OrdinalIgnoreCase);
            SelectedColumns.Clear();
            foreach (var colName in _defaultColumns)
            {
                lookup.TryGetValue(colName, out string? desc);
                SelectedColumns.Add(new ColumnItem(colName, desc ?? "", _requiredColumns.Contains(colName)));
            }
            EnsureRequiredColumnsPresent();
            EnrichSelectedColumnDescriptions();
            ApplyFilter();
        }

        [RelayCommand]
        private void Confirm()
        {
            RequestClose?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        public string GetSelectedColumnsString()
        {
            return string.Join(", ", SelectedColumns.Select(c => c.Name));
        }

        private static List<string> ParseColumns(string? columns)
        {
            if (string.IsNullOrWhiteSpace(columns)) return new List<string>();
            return columns
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void Dispose()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
        }
    }
}
