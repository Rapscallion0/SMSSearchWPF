using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace SMS_Search.ViewModels.Settings
{
    public partial class SearchSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly IDataRepository _dataRepository;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;

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
        private bool _isSaved;

        [ObservableProperty]
        private bool _isSaving;

        public ObservableSetting<bool> SelectCustomSqlOnBuild { get; }

        public SearchSectionViewModel(
            ISettingsRepository repository,
            IDataRepository dataRepository,
            ILoggerService logger,
            IDialogService dialogService)
        {
            _repository = repository;
            _dataRepository = dataRepository;
            _logger = logger;
            _dialogService = dialogService;

            FunctionColumns = _repository.GetValue("QUERY", "FUNCTION") ?? "F1063, F1064, F1051, F1050, F1081";
            TotalizerColumns = _repository.GetValue("QUERY", "TOTALIZER") ?? "F1034, F1039, F1128, F1129, F1179, F1253, F1710, F1131, F1048, F1709";

            var selectCustomSqlStr = repository.GetValue("GENERAL", "SELECT_CUSTOM_SQL_ON_BUILD");
            SelectCustomSqlOnBuild = new ObservableSetting<bool>(
                repository, "GENERAL", "SELECT_CUSTOM_SQL_ON_BUILD",
                selectCustomSqlStr != "0", // Default true
                v => v ? "1" : "0");
        }

        partial void OnFunctionColumnsChanged(string value)
        {
            HasFunctionError = false;
            IsSaved = false;
        }

        partial void OnTotalizerColumnsChanged(string value)
        {
            HasTotalizerError = false;
            IsSaved = false;
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
                var server = _repository.GetValue("CONNECTION", "SERVER") ?? "";
                var database = _repository.GetValue("CONNECTION", "DATABASE") ?? "";

                if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database))
                {
                    _dialogService.ShowError("Server and Database connection settings must be configured to validate columns.", "Validation Error");
                    return;
                }

                string? user = null;
                string? pass = null;

                if (bool.TryParse(_repository.GetValue("CONNECTION", "WINDOWSAUTH"), out bool windowsAuth) && !windowsAuth)
                {
                    user = _repository.GetValue("CONNECTION", "SQLUSER") ?? "";
                    var encryptedPass = _repository.GetValue("CONNECTION", "SQLPASSWORD");
                    pass = !string.IsNullOrEmpty(encryptedPass) ? GeneralUtils.Decrypt(encryptedPass) : null;
                }

                bool functionValid = true;
                bool totalizerValid = true;

                // Validate Function columns
                if (!string.IsNullOrWhiteSpace(FunctionColumns))
                {
                    try
                    {
                        var sql = $"SELECT TOP 0 {FunctionColumns} FROM FCT_TAB";
                        await _dataRepository.GetQuerySchemaAsync(server, database, user, pass, sql, null);
                    }
                    catch (SqlException)
                    {
                        functionValid = false;
                        HasFunctionError = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error validating function columns", ex);
                        _dialogService.ShowError($"Error validating function columns: {ex.Message}", "Validation Error");
                        return; // Unknown error, abort save
                    }
                }

                // Validate Totalizer columns
                if (!string.IsNullOrWhiteSpace(TotalizerColumns))
                {
                    try
                    {
                        var sql = $"SELECT TOP 0 {TotalizerColumns} FROM TLZ_TAB";
                        await _dataRepository.GetQuerySchemaAsync(server, database, user, pass, sql, null);
                    }
                    catch (SqlException)
                    {
                        totalizerValid = false;
                        HasTotalizerError = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error validating totalizer columns", ex);
                        _dialogService.ShowError($"Error validating totalizer columns: {ex.Message}", "Validation Error");
                        return; // Unknown error, abort save
                    }
                }

                if (!functionValid || !totalizerValid)
                {
                    string errorMessage = "Invalid columns detected:\n";
                    if (!functionValid) errorMessage += "- Function columns contain invalid fields for FCT_TAB.\n";
                    if (!totalizerValid) errorMessage += "- Totalizer columns contain invalid fields for TLZ_TAB.\n";
                    _dialogService.ShowError(errorMessage, "Validation Error");
                    return;
                }

                // Both valid, save
                _repository.SetValue("QUERY", "FUNCTION", FunctionColumns);
                _repository.SetValue("QUERY", "TOTALIZER", TotalizerColumns);
                await _repository.SaveAsync();

                IsSaved = true;

                // Flash success state
                await Task.Delay(2000);
                IsSaved = false;
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
            if ("SQL".Contains(query, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}
