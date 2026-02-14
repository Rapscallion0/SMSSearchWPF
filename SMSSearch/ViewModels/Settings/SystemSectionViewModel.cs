using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;

namespace SMS_Search.ViewModels.Settings
{
    public partial class SystemSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly ILoggerService _loggerService;
        private readonly IDialogService _dialogService;

        public override string Title => "System";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_Logging");

        [ObservableProperty]
        private string _currentLogFile = "";

        public SystemSectionViewModel(
            ISettingsRepository repository,
            ILoggerService loggerService,
            IDialogService dialogService)
        {
            _repository = repository;
            _loggerService = loggerService;
            _dialogService = dialogService;

            // IsEnabled
            var enabledStr = repository.GetValue("LOGGING", "ENABLED");
            IsEnabled = new ObservableSetting<bool>(
                repository, "LOGGING", "ENABLED",
                enabledStr != "0", // Default true
                v => v ? "1" : "0");

            IsEnabled.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<bool>.Value))
                {
                    // Update config immediately so LoggerService sees the change
                    _repository.SetValue("LOGGING", "ENABLED", IsEnabled.Value ? "1" : "0");
                    _loggerService.ApplyConfig();
                    UpdateCurrentLogFile();
                }
            };

            // LogLevel
            var levelStr = repository.GetValue("LOGGING", "LEVEL");
            if (!Enum.TryParse(levelStr, out SMS_Search.Utils.LogLevel level)) level = SMS_Search.Utils.LogLevel.Info;
            LogLevel = new ObservableSetting<SMS_Search.Utils.LogLevel>(
                repository, "LOGGING", "LEVEL",
                level,
                v => v.ToString());

            LogLevel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<SMS_Search.Utils.LogLevel>.Value))
                {
                    _repository.SetValue("LOGGING", "LEVEL", LogLevel.Value.ToString());
                    _loggerService.ApplyConfig();
                }
            };

            // RetentionDays
            var retentionStr = repository.GetValue("LOGGING", "RETENTION");
            if (!int.TryParse(retentionStr, out int retention)) retention = 14;
            RetentionDays = new ObservableSetting<int>(
                repository, "LOGGING", "RETENTION",
                retention,
                v => v.ToString());

            RetentionDays.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<int>.Value))
                {
                    _repository.SetValue("LOGGING", "RETENTION", RetentionDays.Value.ToString());
                    _loggerService.ApplyConfig();
                }
            };

            UpdateCurrentLogFile();
        }

        private void UpdateCurrentLogFile()
        {
            CurrentLogFile = _loggerService.GetCurrentLogPath();
        }

        public ObservableSetting<bool> IsEnabled { get; }
        public ObservableSetting<SMS_Search.Utils.LogLevel> LogLevel { get; }
        public ObservableSetting<int> RetentionDays { get; }

        public IEnumerable<SMS_Search.Utils.LogLevel> LogLevels => new[]
        {
            SMS_Search.Utils.LogLevel.Critical,
            SMS_Search.Utils.LogLevel.Error,
            SMS_Search.Utils.LogLevel.Warning,
            SMS_Search.Utils.LogLevel.Info,
            SMS_Search.Utils.LogLevel.Debug
        };

        [RelayCommand]
        private void OpenLogFile()
        {
            try
            {
                string path = CurrentLogFile;
                if (File.Exists(path))
                {
                    new Process
                    {
                        StartInfo = new ProcessStartInfo(path)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                }
                else
                {
                    // Maybe just open the folder if file doesn't exist
                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        new Process
                        {
                            StartInfo = new ProcessStartInfo(dir)
                            {
                                UseShellExecute = true
                            }
                        }.Start();
                    }
                    else
                    {
                        _dialogService.ShowToast("Log file or directory not found.", "Error", ToastType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Failed to open log file", ex);
                _dialogService.ShowToast("Failed to open log file.", "Error", ToastType.Error);
            }
        }

        [RelayCommand]
        private async Task ResetEula()
        {
            await _repository.SaveAsync("GENERAL", "EULA", "0");
            _dialogService.ShowToast("EULA has been reset. It will appear on next startup.", "Settings", ToastType.Info);
        }

        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             if ("Log".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Debug".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("EULA".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Agreement".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("License".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Reset".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
