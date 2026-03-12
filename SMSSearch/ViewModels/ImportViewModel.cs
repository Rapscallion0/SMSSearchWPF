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
        private readonly MainViewModel _mainViewModel;

        public ImportViewModel(IDataRepository repository, ILoggerService logger, IDialogService dialogService, IConfigService config, MainViewModel mainViewModel)
        {
            _repository = repository;
            _logger = logger;
            _dialogService = dialogService;
            _config = config;
            _mainViewModel = mainViewModel;

            TargetDatabaseSuffix = "";
            SwitchToDatabaseAfterImport = true;
            SelectedFiles = new ObservableCollection<string>();
            Databases = new ObservableCollection<string>();
            TargetDatabases = new ObservableCollection<string>();
            CanChangeTemplateDatabase = true;

            // When the import overlay is shown, load databases
            _mainViewModel.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(_mainViewModel.IsImportTargetVisible) && _mainViewModel.IsImportTargetVisible)
                {
                    await LoadDatabasesAsync();
                }
            };
        }

        [ObservableProperty]
        private string _targetDatabaseSuffix;

        partial void OnTargetDatabaseSuffixChanged(string value)
        {
            UpdateTemplateDatabaseState();
        }

        [ObservableProperty]
        private string? _templateDatabaseName;

        [ObservableProperty]
        private bool _canChangeTemplateDatabase;

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

        private void UpdateTemplateDatabaseState()
        {
            string fullTargetDbName = "DBUSER_" + (TargetDatabaseSuffix ?? "");

            // If the target database already exists, enforce it as the template and disable changes
            if (Databases.Contains(fullTargetDbName, StringComparer.OrdinalIgnoreCase))
            {
                // Find the actual cased name from the list
                string actualDbName = Databases.First(d => d.Equals(fullTargetDbName, StringComparison.OrdinalIgnoreCase));
                TemplateDatabaseName = actualDbName;
                CanChangeTemplateDatabase = false;
            }
            else
            {
                CanChangeTemplateDatabase = true;
            }
        }

        private async Task LoadDatabasesAsync()
        {
            try
            {
                var server = _config.GetValue("CONNECTION", "SERVER") ?? "";
                string user = "";
                string? decryptedPass = null;

                bool isWindowsAuth = true;
                if (bool.TryParse(_config.GetValue("CONNECTION", "WINDOWSAUTH"), out bool b))
                    isWindowsAuth = b;

                if (!isWindowsAuth)
                {
                    user = _config.GetValue("CONNECTION", "SQLUSER") ?? "";
                    var pass = _config.GetValue("CONNECTION", "SQLPASSWORD");
                    decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;
                }

                if (string.IsNullOrEmpty(server)) return;

                var dbs = await _repository.GetDatabasesAsync(server, user, decryptedPass);
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

                // Auto-select the currently active database as the template
                if (!string.IsNullOrEmpty(_mainViewModel.SelectedDatabase) && Databases.Contains(_mainViewModel.SelectedDatabase))
                {
                    TemplateDatabaseName = _mainViewModel.SelectedDatabase;
                }
                else if (Databases.Count > 0)
                {
                    TemplateDatabaseName = Databases[0];
                }

                UpdateTemplateDatabaseState();
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
            _mainViewModel.IsImportTargetVisible = false;
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

            if (string.IsNullOrWhiteSpace(TemplateDatabaseName))
            {
                _dialogService.ShowMessage("Please select a template database.", "Import");
                return;
            }

            IsBusy = true;
            IsProgressIndeterminate = true;
            ProgressStatusText = "Starting import process...";

            try
            {
                var server = _config.GetValue("CONNECTION", "SERVER") ?? "";
                string user = "";
                string? decryptedPass = null;

                bool isWindowsAuth = true;
                if (bool.TryParse(_config.GetValue("CONNECTION", "WINDOWSAUTH"), out bool b))
                    isWindowsAuth = b;

                if (!isWindowsAuth)
                {
                    user = _config.GetValue("CONNECTION", "SQLUSER") ?? "";
                    var pass = _config.GetValue("CONNECTION", "SQLPASSWORD");
                    decryptedPass = !string.IsNullOrEmpty(pass) ? GeneralUtils.Decrypt(pass) : null;
                }

                // Call the actual logic (which we will implement next in DataRepository)
                // For now, let's just create a shell method in DataRepository to handle this.
                await _repository.PerformImportProcessAsync(server, user, decryptedPass, fullTargetDbName, TemplateDatabaseName, SelectedFiles.ToList(),
                    progress => {
                        App.Current.Dispatcher.Invoke(() => {
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
                        });
                    },
                    tableName => {
                        return Task.FromResult(_dialogService.ShowTableExistsPrompt(tableName));
                    });

                _dialogService.ShowToast("Import completed successfully.", "Import", Views.ToastType.Success);

                SelectedFiles.Clear();
                _mainViewModel.IsImportTargetVisible = false;

                // Reload main view model databases to see the new one
                await _mainViewModel.LoadDatabasesAsync();

                if (SwitchToDatabaseAfterImport)
                {
                    _mainViewModel.SelectedDatabase = fullTargetDbName;
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