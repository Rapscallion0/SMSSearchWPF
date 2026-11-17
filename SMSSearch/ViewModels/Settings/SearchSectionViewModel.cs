using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace SMS_Search.ViewModels.Settings
{
    public partial class SearchSectionViewModel : SettingsSectionViewModel
    {
        private const string DefaultFunctionColumns = "F1063, F1064, F1051, F1050, F1081";
        private const string DefaultTotalizerColumns = "F1034, F1039, F1128, F1129, F1179, F1253, F1710, F1131, F1048, F1709";
        private const string RequiredFunctionColumn = "F1063";
        private const string RequiredTotalizerColumn = "F1034";
        private static readonly System.Text.RegularExpressions.Regex IdentifierRegex =
            new(@"^\[?[A-Za-z_][A-Za-z0-9_]*\]?$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private readonly ISettingsRepository _repository;
        private readonly IDataRepository _dataRepository;
        private readonly IConnectionSettingsService _connectionSettingsService;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IFieldDictionaryService? _fieldDictionaryService;

        public override string Title => "Search";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_Search");

        [ObservableProperty]
        private string _functionColumns;

        [ObservableProperty]
        private string _totalizerColumns;

        [ObservableProperty]
        private bool _hasFunctionError;

        [ObservableProperty]
        private bool _hasTotalizerError;

        [ObservableProperty]
        private System.Collections.Generic.IReadOnlySet<string> _invalidFunctionColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [ObservableProperty]
        private System.Collections.Generic.IReadOnlySet<string> _invalidTotalizerColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [ObservableProperty]
        private bool _isSaved;

        [ObservableProperty]
        private bool _isSaving;

        [ObservableProperty]
        private bool _isFunctionSaved;

        [ObservableProperty]
        private bool _isTotalizerSaved;

        public ObservableSetting<bool> SelectCustomSqlOnBuild { get; }
        public ObservableSetting<bool> AnyMatchDefault { get; }
        public ObservableSetting<DefaultSearchTabMode> DefaultSearchTab { get; }
        public ObservableSetting<SMS_Search.Data.DefaultTableAction> DefaultTableAction { get; }

        public System.Collections.Generic.IEnumerable<DefaultSearchTabMode> SearchModes => Enum.GetValues<DefaultSearchTabMode>();
        public System.Collections.Generic.IEnumerable<SMS_Search.Data.DefaultTableAction> TableActions => Enum.GetValues<SMS_Search.Data.DefaultTableAction>();


        public SearchSectionViewModel(
            ISettingsRepository repository,
            IDataRepository dataRepository,
            IConnectionSettingsService connectionSettingsService,
            ILoggerService logger,
            IDialogService dialogService,
            IFieldDictionaryService? fieldDictionaryService = null)
        {
            _repository = repository;
            _dataRepository = dataRepository;
            _connectionSettingsService = connectionSettingsService;
            _logger = logger;
            _dialogService = dialogService;
            _fieldDictionaryService = fieldDictionaryService;

            FunctionColumns = _repository.GetValue(AppSettings.Sections.Query, AppSettings.Keys.Function) ?? DefaultFunctionColumns;
            TotalizerColumns = _repository.GetValue(AppSettings.Sections.Query, AppSettings.Keys.Totalizer) ?? DefaultTotalizerColumns;

            var selectCustomSqlStr = repository.GetValue(AppSettings.Sections.Search, AppSettings.Keys.SelectCustomSqlOnBuild);
            SelectCustomSqlOnBuild = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Search, AppSettings.Keys.SelectCustomSqlOnBuild,
                selectCustomSqlStr != "0", // Default true
                v => v ? "1" : "0");

            var anyMatchDefaultStr = repository.GetValue(AppSettings.Sections.Search, AppSettings.Keys.AnyMatchDefault);
            AnyMatchDefault = new ObservableSetting<bool>(
                repository, AppSettings.Sections.Search, AppSettings.Keys.AnyMatchDefault,
                anyMatchDefaultStr != "False", // Default true
                v => v.ToString());

            // Default Search Tab
            var defaultTabStr = repository.GetValue(AppSettings.Sections.Search, AppSettings.Keys.DefaultTab);
            DefaultSearchTabMode defaultTab;
            if (!Enum.TryParse(defaultTabStr, out defaultTab)) defaultTab = DefaultSearchTabMode.Function;
            DefaultSearchTab = new ObservableSetting<DefaultSearchTabMode>(
                repository, AppSettings.Sections.Search, AppSettings.Keys.DefaultTab,
                defaultTab,
                v => v.ToString());

            // Default Table Action
            var defaultActionStr = repository.GetValue(AppSettings.Sections.Search, AppSettings.Keys.DefaultTableAction);
            SMS_Search.Data.DefaultTableAction defaultAction;
            if (!Enum.TryParse(defaultActionStr, out defaultAction)) defaultAction = SMS_Search.Data.DefaultTableAction.QueryFields;
            DefaultTableAction = new ObservableSetting<SMS_Search.Data.DefaultTableAction>(
                repository, AppSettings.Sections.Search, AppSettings.Keys.DefaultTableAction,
                defaultAction,
                v => v.ToString());

        }

        partial void OnFunctionColumnsChanged(string value)
        {
            HasFunctionError = !ValidateColumnList(value, RequiredFunctionColumn, out _);
            InvalidFunctionColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IsSaved = false;
        }

        partial void OnTotalizerColumnsChanged(string value)
        {
            HasTotalizerError = !ValidateColumnList(value, RequiredTotalizerColumn, out _);
            InvalidTotalizerColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IsSaved = false;
        }

        public static bool ValidateColumnList(string? columns, string requiredColumn, out string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(columns))
            {
                errorMessage = $"Column list cannot be empty and must include required column {requiredColumn}.";
                return false;
            }

            var parts = columns.Split(',');
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in parts)
            {
                string trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed) || !IdentifierRegex.IsMatch(trimmed))
                {
                    errorMessage = $"Invalid column name '{trimmed}'. Use comma-separated column identifiers (e.g. {requiredColumn}, F1064).";
                    return false;
                }

                seen.Add(trimmed.Trim('[', ']'));
            }

            if (!seen.Contains(requiredColumn))
            {
                errorMessage = $"Required column {requiredColumn} must be included.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        [RelayCommand]
        private void PickFunctionColumns()
        {
            using var vm = new ColumnPickerViewModel(
                _dataRepository,
                _connectionSettingsService,
                _logger,
                "Select Function Columns (FCT_TAB)",
                "FCT_TAB",
                FunctionColumns,
                DefaultFunctionColumns,
                new[] { RequiredFunctionColumn },
                _fieldDictionaryService);

            if (_dialogService.ShowColumnPicker(vm) == true)
            {
                FunctionColumns = vm.GetSelectedColumnsString();
            }
        }

        [RelayCommand]
        private void PickTotalizerColumns()
        {
            using var vm = new ColumnPickerViewModel(
                _dataRepository,
                _connectionSettingsService,
                _logger,
                "Select Totalizer Columns (TLZ_TAB)",
                "TLZ_TAB",
                TotalizerColumns,
                DefaultTotalizerColumns,
                new[] { RequiredTotalizerColumn },
                _fieldDictionaryService);

            if (_dialogService.ShowColumnPicker(vm) == true)
            {
                TotalizerColumns = vm.GetSelectedColumnsString();
            }
        }

        [RelayCommand]
        public async Task SaveColumnsAsync()
        {
            if (IsSaving) return;
            IsSaving = true;
            IsSaved = false;
            HasFunctionError = false;
            HasTotalizerError = false;

            try
            {
                bool fnSyntaxOk = ValidateColumnList(FunctionColumns, RequiredFunctionColumn, out string? fnError);
                bool tlzSyntaxOk = ValidateColumnList(TotalizerColumns, RequiredTotalizerColumn, out string? tlzError);

                if (!fnSyntaxOk || !tlzSyntaxOk)
                {
                    HasFunctionError = !fnSyntaxOk;
                    HasTotalizerError = !tlzSyntaxOk;

                    var errSb = new StringBuilder();
                    if (!fnSyntaxOk) errSb.AppendLine($"- Function: {fnError}");
                    if (!tlzSyntaxOk) errSb.AppendLine($"- Totalizer: {tlzError}");
                    _dialogService.ShowError(errSb.ToString().TrimEnd(), "Validation Error");
                    return;
                }
                var conn = _connectionSettingsService.GetConnectionSettings();
                var server = conn.Server;
                var database = conn.Database;

                if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database))
                {
                    _dialogService.ShowError("Server and Database connection settings must be configured to validate columns.", "Validation Error");
                    return;
                }

                string? user = conn.IsWindowsAuth ? null : conn.User;
                string? pass = conn.Password;

                var invalidFnCols = new System.Collections.Generic.List<string>();
                var invalidTlzCols = new System.Collections.Generic.List<string>();

                // Validate Function columns against FCT_TAB schema
                try
                {
                    var fnSchema = await _dataRepository.GetQuerySchemaAsync(server, database, user, pass, "SELECT TOP 0 * FROM [FCT_TAB]", null);
                    var validFnSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (System.Data.DataColumn col in fnSchema.Columns) validFnSet.Add(col.ColumnName);

                    foreach (var raw in FunctionColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        string colName = raw.Trim('[', ']');
                        if (!validFnSet.Contains(colName)) invalidFnCols.Add(colName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error validating function columns", ex);
                    _dialogService.ShowError($"Error validating function columns: {ex.Message}", "Validation Error");
                    return;
                }

                // Validate Totalizer columns against TLZ_TAB schema
                try
                {
                    var tlzSchema = await _dataRepository.GetQuerySchemaAsync(server, database, user, pass, "SELECT TOP 0 * FROM [TLZ_TAB]", null);
                    var validTlzSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (System.Data.DataColumn col in tlzSchema.Columns) validTlzSet.Add(col.ColumnName);

                    foreach (var raw in TotalizerColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        string colName = raw.Trim('[', ']');
                        if (!validTlzSet.Contains(colName)) invalidTlzCols.Add(colName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error validating totalizer columns", ex);
                    _dialogService.ShowError($"Error validating totalizer columns: {ex.Message}", "Validation Error");
                    return;
                }

                InvalidFunctionColumns = new System.Collections.Generic.HashSet<string>(invalidFnCols, StringComparer.OrdinalIgnoreCase);
                InvalidTotalizerColumns = new System.Collections.Generic.HashSet<string>(invalidTlzCols, StringComparer.OrdinalIgnoreCase);
                HasFunctionError = invalidFnCols.Count > 0;
                HasTotalizerError = invalidTlzCols.Count > 0;

                if (invalidFnCols.Count > 0 || invalidTlzCols.Count > 0)
                {
                    var sb = new StringBuilder(150);
                    sb.AppendLine("Invalid columns detected:");
                    if (invalidFnCols.Count > 0)
                        sb.AppendLine($"- Function (FCT_TAB): {string.Join(", ", invalidFnCols)}");
                    if (invalidTlzCols.Count > 0)
                        sb.AppendLine($"- Totalizer (TLZ_TAB): {string.Join(", ", invalidTlzCols)}");
                    _dialogService.ShowError(sb.ToString().TrimEnd(), "Validation Error");
                    return;
                }

                // Both valid, save
                _repository.SetValue(AppSettings.Sections.Query, AppSettings.Keys.Function, FunctionColumns);
                _repository.SetValue(AppSettings.Sections.Query, AppSettings.Keys.Totalizer, TotalizerColumns);
                await _repository.SaveAsync();

                IsSaved = true;
                IsFunctionSaved = true;
                IsTotalizerSaved = true;

                // Flash success state
                await Task.Delay(2000);
                IsSaved = false;
                IsFunctionSaved = false;
                IsTotalizerSaved = false;
            }
            finally
            {
                IsSaving = false;
            }
        }

        public override bool Matches(string query)
        {
            if (base.Matches(query)) return true;

            if ("Columns".Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if ("Function".Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if ("Totalizer".Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if ("Behavior".Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if ("Tab".Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if ("Action".Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if ("SQL".Contains(query, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}
