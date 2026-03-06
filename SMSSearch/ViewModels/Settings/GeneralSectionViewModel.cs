using SMS_Search.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;

namespace SMS_Search.ViewModels.Settings
{
    public partial class GeneralSectionViewModel : SettingsSectionViewModel
    {
        private readonly ISettingsRepository _repository;
        private readonly IDialogService _dialogService;
        private readonly UpdateChecker _updateChecker;

        [ObservableProperty]
        private string _updateStatusMessage = "";

        [ObservableProperty]
        private string? _updateStatusColor = null;

        public override string Title => "General";
        public override ControlTemplate Icon => (ControlTemplate)System.Windows.Application.Current.FindResource("Icon_Nav_General");

        public GeneralSectionViewModel(ISettingsRepository repository, IDialogService dialogService, UpdateChecker updateChecker)
        {
            _repository = repository;
            _dialogService = dialogService;
            _updateChecker = updateChecker;

            // Always On Top
            var alwaysOnTopStr = repository.GetValue("GENERAL", "ALWAYSONTOP");
            AlwaysOnTop = new ObservableSetting<bool>(
                repository, "GENERAL", "ALWAYSONTOP",
                alwaysOnTopStr == "1",
                v => v ? "1" : "0");

            // Show In Tray
            var showInTrayStr = repository.GetValue("GENERAL", "SHOWINTRAY");
            ShowInTray = new ObservableSetting<bool>(
                repository, "GENERAL", "SHOWINTRAY",
                showInTrayStr == "1",
                v => v ? "1" : "0");

            // Check Update
            var checkUpdateStr = repository.GetValue("GENERAL", "CHECKUPDATE");
            CheckUpdate = new ObservableSetting<bool>(
                repository, "GENERAL", "CHECKUPDATE",
                checkUpdateStr == "1",
                v => v ? "1" : "0");

            // Main Startup Location
            var mainStartStr = repository.GetValue("GENERAL", "MAIN_STARTUP_LOCATION");
            StartupLocationMode mainStart;
            if (!Enum.TryParse(mainStartStr, out mainStart)) mainStart = StartupLocationMode.Last;
            MainStartupLocation = new ObservableSetting<StartupLocationMode>(
                repository, "GENERAL", "MAIN_STARTUP_LOCATION",
                mainStart,
                v => v.ToString());

            // Unarchive Startup Location
            var unarchiveStartStr = repository.GetValue("GENERAL", "UNARCHIVE_STARTUP_LOCATION");
            StartupLocationMode unarchiveStart;
            if (!Enum.TryParse(unarchiveStartStr, out unarchiveStart)) unarchiveStart = StartupLocationMode.Last;
            UnarchiveStartupLocation = new ObservableSetting<StartupLocationMode>(
                repository, "GENERAL", "UNARCHIVE_STARTUP_LOCATION",
                unarchiveStart,
                v => v.ToString());

            // Default Search Tab
            var defaultTabStr = repository.GetValue("GENERAL", "DEFAULT_TAB");
            DefaultSearchTabMode defaultTab;
            if (!Enum.TryParse(defaultTabStr, out defaultTab)) defaultTab = DefaultSearchTabMode.Function;
            DefaultSearchTab = new ObservableSetting<DefaultSearchTabMode>(
                repository, "GENERAL", "DEFAULT_TAB",
                defaultTab,
                v => v.ToString());

            // Default Table Action
            var defaultActionStr = repository.GetValue("GENERAL", "DEFAULT_TABLE_ACTION");
            SMS_Search.Data.DefaultTableAction defaultAction;
            if (!Enum.TryParse(defaultActionStr, out defaultAction)) defaultAction = SMS_Search.Data.DefaultTableAction.QueryFields;
            DefaultTableAction = new ObservableSetting<SMS_Search.Data.DefaultTableAction>(
                repository, "GENERAL", "DEFAULT_TABLE_ACTION",
                defaultAction,
                v => v.ToString());

            // Remember Size
            var rememberSizeStr = repository.GetValue("GENERAL", "MAIN_REMEMBER_SIZE");
            RememberSize = new ObservableSetting<bool>(
                repository, "GENERAL", "MAIN_REMEMBER_SIZE",
                rememberSizeStr == "1",
                v => v ? "1" : "0");

            // Copy Delimiter
            var copyDelimStr = repository.GetValue("GENERAL", "COPY_DELIMITER");
            CopyDelimiter = new ObservableSetting<string>(
                repository, "GENERAL", "COPY_DELIMITER",
                !string.IsNullOrEmpty(copyDelimStr) ? copyDelimStr! : "TAB");

            CopyDelimiter.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableSetting<string>.Value))
                {
                    UpdateVisibility();
                }
            };

            // Custom Delimiter
            var customDelimStr = repository.GetValue("GENERAL", "COPY_DELIMITER_CUSTOM");
            CustomDelimiter = new ObservableSetting<string>(
                repository, "GENERAL", "COPY_DELIMITER_CUSTOM",
                customDelimStr ?? "");

            // Toast Timeout
            var toastTimeoutStr = repository.GetValue("GENERAL", "TOAST_TIMEOUT");
            int toastTimeout;
            if (!int.TryParse(toastTimeoutStr, out toastTimeout)) toastTimeout = 5;
            ToastTimeout = new ObservableSetting<int>(
                repository, "GENERAL", "TOAST_TIMEOUT",
                toastTimeout,
                v => v.ToString());

            UpdateVisibility();

            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
            UpdateStatusMessage = $"Your version: V{version}";

            if (CheckUpdate.Value)
            {
                _ = CheckUpdateSilentAsync();
            }
        }

        private async Task CheckUpdateSilentAsync()
        {
            try
            {
                var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
                var info = await _updateChecker.CheckForUpdatesAsync();

                if (info.IsNewer)
                {
                    UpdateStatusMessage = $"An updated version is available!\n\nYour version: V{version}\nAvailable: V{info.Version}";
                    UpdateStatusColor = "Red";
                }
                else
                {
                    UpdateStatusMessage = $"Application is up to date: V{version}";
                    UpdateStatusColor = "Green";
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        [RelayCommand]
        private async Task CheckUpdateAsync()
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
            try
            {
                var info = await _updateChecker.CheckForUpdatesAsync();

                if (info.IsNewer)
                {
                    UpdateStatusMessage = $"An updated version is available!\n\nYour version: V{version}\nAvailable: V{info.Version}";
                    UpdateStatusColor = "Red";
                    _dialogService.ShowToast("An update is available!", "Update", ToastType.Info);

                    var msg = $"There is an update available for download.\n\nCurrent Version: {version}\nNew Version: {info.Version}\n\nWould you like to update now?";
                    if (_dialogService.ShowConfirmation(msg, "SMS Search Update"))
                    {
                        await _updateChecker.PerformUpdate(info);
                    }
                }
                else
                {
                    UpdateStatusMessage = $"Application is up to date: V{version}";
                    UpdateStatusColor = "Green";
                    _dialogService.ShowToast("You are on the latest version.", "Update", ToastType.Success);
                }
            }
            catch (Exception ex)
            {
                UpdateStatusMessage = $"Failed to check for updates.";
                UpdateStatusColor = "Red";
                _dialogService.ShowToast("Failed to check for updates: " + ex.Message, "Update Error", ToastType.Error);
            }
        }

        public ObservableSetting<bool> AlwaysOnTop { get; }
        public ObservableSetting<bool> ShowInTray { get; }
        public ObservableSetting<bool> CheckUpdate { get; }
        public ObservableSetting<StartupLocationMode> MainStartupLocation { get; }
        public ObservableSetting<StartupLocationMode> UnarchiveStartupLocation { get; }
        public ObservableSetting<bool> RememberSize { get; }
        public ObservableSetting<DefaultSearchTabMode> DefaultSearchTab { get; }
        public ObservableSetting<SMS_Search.Data.DefaultTableAction> DefaultTableAction { get; }
        public ObservableSetting<string> CopyDelimiter { get; }
        public ObservableSetting<string> CustomDelimiter { get; }
        public ObservableSetting<int> ToastTimeout { get; }

        public IEnumerable<StartupLocationMode> StartupLocationModes => Enum.GetValues<StartupLocationMode>();
        public IEnumerable<DefaultSearchTabMode> SearchModes => Enum.GetValues<DefaultSearchTabMode>();
        public IEnumerable<SMS_Search.Data.DefaultTableAction> TableActions => Enum.GetValues<SMS_Search.Data.DefaultTableAction>();

        [ObservableProperty]
        private bool _isCustomDelimiterVisible;

        public ObservableCollection<string> Delimiters { get; } = new ObservableCollection<string>
        {
            "TAB",
            "Comma (,)",
            "Pipe (|)",
            "Semicolon (;)",
            "Custom..."
        };

        private void UpdateVisibility()
        {
            IsCustomDelimiterVisible = CopyDelimiter.Value == "Custom...";
        }

        public override bool Matches(string query)
        {
             if (base.Matches(query)) return true;

             if ("Startup".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Location".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Window".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Tray".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Update".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Export".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Delimiter".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Tab".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Toast".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Notification".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Query".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Fields".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;
             if ("Records".Contains(query, System.StringComparison.OrdinalIgnoreCase)) return true;

             return false;
        }
    }
}
