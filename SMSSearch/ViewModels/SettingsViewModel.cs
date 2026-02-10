using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;

namespace SMS_Search.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigService _config;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;
        private readonly IHotkeyService _hotkeyService;

        public SettingsViewModel(
            IConfigService config,
            IDialogService dialogService,
            ILoggerService logger,
            IHotkeyService hotkeyService)
        {
            _config = config;
            _dialogService = dialogService;
            _logger = logger;
            _hotkeyService = hotkeyService;

            // Load initial connection settings
            _server = _config.GetValue("CONNECTION", "SERVER");
            _database = _config.GetValue("CONNECTION", "DATABASE");
            _user = _config.GetValue("CONNECTION", "SQLUSER");

            // Sub-ViewModels
            General = new GeneralSettingsViewModel(_config);
            Display = new DisplaySettingsViewModel(_config);
            CleanSql = new CleanSqlSettingsViewModel(_config);
            Launcher = new LauncherSettingsViewModel(_config, _hotkeyService, _logger);
        }

        [ObservableProperty]
        private string _server;

        [ObservableProperty]
        private string _database;

        [ObservableProperty]
        private string _user;

        public GeneralSettingsViewModel General { get; }
        public DisplaySettingsViewModel Display { get; }
        public CleanSqlSettingsViewModel CleanSql { get; }
        public LauncherSettingsViewModel Launcher { get; }

        public IRelayCommand<object> SaveCommand => new RelayCommand<object>(Save);

        private void Save(object parameter)
        {
            try
            {
                _config.SetValue("CONNECTION", "SERVER", Server);
                _config.SetValue("CONNECTION", "DATABASE", Database);
                _config.SetValue("CONNECTION", "SQLUSER", User);

                if (parameter is System.Windows.Controls.PasswordBox pb && !string.IsNullOrEmpty(pb.Password))
                {
                    _config.SetValue("CONNECTION", "SQLPASSWORD", GeneralUtils.Encrypt(pb.Password));
                }

                General.Save();
                Display.Save();
                CleanSql.Save();
                Launcher.Save();

                _config.Save();
                _dialogService.ShowToast("Settings saved successfully.", "Settings", SMS_Search.Views.ToastType.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error saving settings", ex);
                _dialogService.ShowError("Failed to save settings: " + ex.Message, "Error");
            }
        }
    }
}
