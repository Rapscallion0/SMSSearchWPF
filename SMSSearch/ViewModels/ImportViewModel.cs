using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IDataRepository _repository;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IConfigService _config;
        private readonly IConnectionSettingsService _connectionSettingsService;

        public event Action? RequestClose;
        public event Func<string, bool, Task>? ImportCompleted;
        public event Func<Task>? DatabasesChanged;

        public ImportViewModel(IDataRepository repository, ILoggerService logger, IDialogService dialogService, IConfigService config, IConnectionSettingsService connectionSettingsService)
        {
            _repository = repository;
            _logger = logger;
            _dialogService = dialogService;
            _config = config;
            _connectionSettingsService = connectionSettingsService;

            SelectedFiles = new ObservableCollection<string>();
            Databases = new ObservableCollection<string>();
            TargetDatabases = new ObservableCollection<string>();

            TargetDatabaseSuffix = "";
            SwitchToDatabaseAfterImport = true;
            CanChangeTemplateDatabase = true;
        }

        [ObservableProperty]
        private string _targetDatabaseSuffix;

        partial void OnTargetDatabaseSuffixChanged(string value)
        {
            _ = UpdateTemplateDatabaseStateAsync();
        }

        [ObservableProperty]
        private string? _templateDatabaseName;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSelectTemplateDatabase))]
        private bool _canChangeTemplateDatabase;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSelectTemplateDatabase))]
        private bool _useTemplateDatabase;

        [ObservableProperty]
        private bool _confirmInferredTypes;

        public bool CanSelectTemplateDatabase => UseTemplateDatabase && CanChangeTemplateDatabase;

        [ObservableProperty]
        private bool _switchToDatabaseAfterImport;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isProgressIndeterminate;

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private string _progressStatusText = "";

        public ObservableCollection<string> SelectedFiles { get; }
        public ObservableCollection<string> Databases { get; }
        public ObservableCollection<string> TargetDatabases { get; }

        private async Task UpdateTemplateDatabaseStateAsync()
        {
            string fullTargetDbName = "DBUSER_" + (TargetDatabaseSuffix ?? "");

            // If the target database already exists, find its original template database and lock it
            if (Databases.Contains(fullTargetDbName, StringComparer.OrdinalIgnoreCase))
            {
                string actualDbName = Databases.First(d => d.Equals(fullTargetDbName, StringComparison.OrdinalIgnoreCase));

                var conn = _connectionSettingsService.GetConnectionSettings();

                if (!string.IsNullOrEmpty(conn.Server))
                {
                    string? originalTemplate = await _repository.GetDatabaseTemplateAsync(conn.Server, actualDbName, conn.User, conn.Password);
                    if (!string.IsNullOrEmpty(originalTemplate) && Databases.Contains(originalTemplate, StringComparer.OrdinalIgnoreCase))
                    {
                        string actualTemplateName = Databases.First(d => d.Equals(originalTemplate, StringComparison.OrdinalIgnoreCase));
                        TemplateDatabaseName = actualTemplateName;
                        UseTemplateDatabase = true;
                    }
                    else
                    {
                        TemplateDatabaseName = null;
                        UseTemplateDatabase = false;
                    }
                }

                CanChangeTemplateDatabase = false;
            }
            else
            {
                if (!CanChangeTemplateDatabase)
                {
                    UseTemplateDatabase = !string.IsNullOrEmpty(TemplateDatabaseName);
                }
                CanChangeTemplateDatabase = true;
            }
        }

        public async Task LoadDatabasesAsync(string? activeDatabase = null)
        {
            try
            {
                var conn = _connectionSettingsService.GetConnectionSettings();

                if (string.IsNullOrEmpty(conn.Server)) return;

                var dbs = await _repository.GetDatabasesAsync(conn.Server, conn.User, conn.Password);
                Databases.Clear();
                TargetDatabases.Clear();
                foreach (var db in dbs)
                {
                    Databases.Add(db);
                    if (db.StartsWith("dbUser_", StringComparison.OrdinalIgnoreCase) && db.Length > 7)
                    {
                        TargetDatabases.Add(db.Substring(7));
                    }
                }

                // Auto-select the currently active database as the template, or first available
                if (!string.IsNullOrEmpty(activeDatabase) && Databases.Contains(activeDatabase))
                {
                    TemplateDatabaseName = activeDatabase;
                }
                else if (Databases.Count > 0)
                {
                    TemplateDatabaseName = Databases[0];
                }
                else
                {
                    TemplateDatabaseName = null;
                }

                UseTemplateDatabase = !string.IsNullOrEmpty(TemplateDatabaseName);

                await UpdateTemplateDatabaseStateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load databases for import overlay", ex);
            }
        }

        [RelayCommand]
        private void BrowseFiles()
        {
            string? file = _dialogService.OpenFileDialog("SQL Files (*.sql)|*.sql|All Files (*.*)|*.*");
            if (!string.IsNullOrEmpty(file))
            {
                if (!SelectedFiles.Contains(file))
                    SelectedFiles.Add(file);
            }
        }

        [RelayCommand]
        private void HandleDroppedFiles(string[] files)
        {
            foreach (var file in files)
            {
                if (Path.GetExtension(file).Equals(".sql", StringComparison.OrdinalIgnoreCase) && !SelectedFiles.Contains(file))
                {
                    SelectedFiles.Add(file);
                }
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            SelectedFiles.Clear();
            RequestClose?.Invoke();
        }

        [RelayCommand]
        private async Task DeleteDatabaseAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetDatabaseSuffix)) return;
            string fullTargetDbName = "DBUSER_" + TargetDatabaseSuffix;

            if (!Databases.Contains(fullTargetDbName, StringComparer.OrdinalIgnoreCase))
            {
                _dialogService.ShowError($"Database '{fullTargetDbName}' does not exist.", "Delete Failed");
                return;
            }

            if (!_dialogService.ShowConfirmation($"Are you sure you want to permanently delete the database '{fullTargetDbName}'?", "Confirm Delete"))
            {
                return;
            }

            IsBusy = true;
            IsProgressIndeterminate = true;
            ProgressStatusText = $"Deleting database {fullTargetDbName}...";

            try
            {
                var conn = _connectionSettingsService.GetConnectionSettings();

                await _repository.DropDatabaseAsync(conn.Server, fullTargetDbName, conn.User, conn.Password);

                _dialogService.ShowToast($"Database '{fullTargetDbName}' deleted.", "Delete Success", Views.ToastType.Success);

                TargetDatabaseSuffix = "";
                await LoadDatabasesAsync();
                if (DatabasesChanged != null)
                {
                    await DatabasesChanged.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Delete database failed", ex);
                _dialogService.ShowError($"Delete failed: {ex.Message}", "Delete Error");
            }
            finally
            {
                IsBusy = false;
                ProgressStatusText = "";
            }
        }

        [RelayCommand]
        private async Task ImportAsync()
        {
            if (SelectedFiles.Count == 0)
            {
                _dialogService.ShowMessage("Please select at least one .sql file to import.", "Import");
                return;
            }

            if (string.IsNullOrWhiteSpace(TargetDatabaseSuffix))
            {
                _dialogService.ShowMessage("Please enter a target database suffix.", "Import");
                return;
            }

            string fullTargetDbName = "DBUSER_" + TargetDatabaseSuffix;

            if (UseTemplateDatabase && string.IsNullOrWhiteSpace(TemplateDatabaseName))
            {
                _dialogService.ShowMessage("Please select a template database or uncheck 'Use template database'.", "Import");
                return;
            }

            string? effectiveTemplateDb = UseTemplateDatabase ? TemplateDatabaseName : null;

            IsBusy = true;
            IsProgressIndeterminate = true;
            ProgressStatusText = "Starting import process...";

            try
            {
                var conn = _connectionSettingsService.GetConnectionSettings();

                _logger.LogInfo("Starting import process for target DB: {TargetDb} from template DB: {TemplateDb}", fullTargetDbName, effectiveTemplateDb ?? "(None)");
                await _repository.PerformImportProcessAsync(conn.Server, conn.User, conn.Password, fullTargetDbName, effectiveTemplateDb, SelectedFiles.ToList(),
                    progress => {
                        try
                        {
                            App.Current.Dispatcher.BeginInvoke((Action)(() => {
                                if (progress.IsIndeterminate)
                                {
                                    IsProgressIndeterminate = true;
                                }
                                else
                                {
                                    IsProgressIndeterminate = false;
                                    ProgressValue = progress.Percentage;
                                }
                                ProgressStatusText = progress.Message;
                            }));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Error updating UI progress during import", ex);
                        }
                    },
                    async tableName => {
                        try
                        {
                            _logger.LogInfo("Prompting user for existing table action: {TableName}", tableName);
                            var result = await _dialogService.ShowTableExistsPromptAsync(tableName);
                            _logger.LogInfo("User selected action for existing table {TableName}: {Action}", tableName, result);
                            return result;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error displaying TableExistsPrompt for table {TableName}", tableName);
                            throw;
                        }
                    },
                    async (tableName, missingColumns, isTypeConfirmation) => {
                        try
                        {
                            _logger.LogInfo("Prompting user for columns on table: {TableName}. Count: {MissingCount}, IsTypeConfirmation: {IsTypeConfirmation}", tableName, missingColumns?.Count ?? 0, isTypeConfirmation);
                            var result = await _dialogService.ShowMissingColumnsPromptAsync(tableName, missingColumns ?? new List<Models.MissingColumnInfo>(), isTypeConfirmation);
                            _logger.LogInfo("User completed columns prompt for table {TableName}. Action selected: {Action}", tableName, result.Action);
                            return result;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error displaying MissingColumnsPrompt for table {TableName}", tableName);
                            throw;
                        }
                    },
                    confirmInferredTypes: !UseTemplateDatabase && ConfirmInferredTypes);

                _logger.LogInfo("Import process completed successfully from view model perspective.");

                _dialogService.ShowToast("Import completed successfully.", "Import", Views.ToastType.Success);

                SelectedFiles.Clear();
                RequestClose?.Invoke();

                if (ImportCompleted != null)
                {
                    await ImportCompleted.Invoke(fullTargetDbName, SwitchToDatabaseAfterImport);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Import process failed", ex);
                _dialogService.ShowError($"Import failed: {ex.Message}", "Import Error");
            }
            finally
            {
                IsBusy = false;
                ProgressStatusText = "";
            }
        }
    }

    public class ImportProgressInfo
    {
        public bool IsIndeterminate { get; set; }
        public double Percentage { get; set; }
        public string Message { get; set; } = "";
    }
}