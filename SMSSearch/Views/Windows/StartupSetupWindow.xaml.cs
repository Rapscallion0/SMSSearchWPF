using System;
using System.Windows;
using System.Windows.Controls;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels.Settings;

namespace SMS_Search.Views.Windows
{
    public partial class StartupSetupWindow : Window
    {
        private readonly ConnectionSectionViewModel _connectionViewModel;
        private readonly IConfigService _configService;
        private readonly IFieldDictionaryService _fieldDictionaryService;

        public StartupSetupWindow(
            ConnectionSectionViewModel connectionViewModel,
            IConfigService configService,
            IFieldDictionaryService fieldDictionaryService)
        {
            InitializeComponent();
            _connectionViewModel = connectionViewModel;
            _configService = configService;
            _fieldDictionaryService = fieldDictionaryService;
            DataContext = _connectionViewModel;

            // Default selection based on current configuration
            var mode = _fieldDictionaryService.GetSourceMode();
            if (mode == DictionarySourceMode.Csv)
            {
                ModeCsvRadio.IsChecked = true;
            }
            else if (_fieldDictionaryService.GetStatus().IsCsvAvailable)
            {
                ModeBothRadio.IsChecked = true;
            }
            else
            {
                ModeDbRadio.IsChecked = true;
            }
        }

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (DbGroupBox == null || CsvGroupBox == null) return;

            ValidationMessage.Visibility = Visibility.Collapsed;

            if (ModeDbRadio.IsChecked == true)
            {
                _connectionViewModel.DictionarySourceMode.Value = DictionarySourceMode.Database;
                _connectionViewModel.IsDbRequired = true;
                _connectionViewModel.IsDictionaryRequired = false;

                DbGroupBox.Visibility = Visibility.Visible;
                CsvGroupBox.Visibility = Visibility.Collapsed;
                DbColumn.Width = new GridLength(1, GridUnitType.Star);
                SpacerColumn.Width = new GridLength(0);
                CsvColumn.Width = new GridLength(0);
            }
            else if (ModeCsvRadio.IsChecked == true)
            {
                _connectionViewModel.DictionarySourceMode.Value = DictionarySourceMode.Csv;
                _connectionViewModel.IsDbRequired = false;
                _connectionViewModel.IsDictionaryRequired = true;

                DbGroupBox.Visibility = Visibility.Collapsed;
                CsvGroupBox.Visibility = Visibility.Visible;
                DbColumn.Width = new GridLength(0);
                SpacerColumn.Width = new GridLength(0);
                CsvColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else if (ModeBothRadio.IsChecked == true)
            {
                _connectionViewModel.DictionarySourceMode.Value = DictionarySourceMode.Auto;
                _connectionViewModel.IsDbRequired = true;
                _connectionViewModel.IsDictionaryRequired = true;

                DbGroupBox.Visibility = Visibility.Visible;
                CsvGroupBox.Visibility = Visibility.Visible;
                DbColumn.Width = new GridLength(1, GridUnitType.Star);
                SpacerColumn.Width = new GridLength(12);
                CsvColumn.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModeBothRadio.IsChecked == true)
            {
                bool hasDb = !string.IsNullOrWhiteSpace(_connectionViewModel.Server.Value) &&
                             !string.IsNullOrWhiteSpace(_connectionViewModel.Database.Value);
                bool hasCsv = _fieldDictionaryService.IsCsvActive() && _fieldDictionaryService.GetStatus().IsCsvAvailable;

                if (hasDb && hasCsv)
                {
                    DialogResult = true;
                    Close();
                    return;
                }

                ValidationMessage.Text = "Please configure both a SQL Server connection (Server & Database) and a valid Fields.csv dictionary, or choose a single mode above.";
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            if (StartupConditionEvaluator.CanStart(_configService, _fieldDictionaryService))
            {
                DialogResult = true;
                Close();
                return;
            }

            ValidationMessage.Text = ModeCsvRadio.IsChecked == true
                ? "Please select a valid Fields.csv file (or place Fields.csv beside the executable) to continue."
                : "Please configure a SQL Server connection (Server & Database) to continue.";
            ValidationMessage.Visibility = Visibility.Visible;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _connectionViewModel.Dispose();
        }
    }
}
