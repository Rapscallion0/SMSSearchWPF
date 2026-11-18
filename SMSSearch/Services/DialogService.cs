using System;
using System.Windows;
using Microsoft.Win32;
using SMS_Search.Views;
using System.Drawing;
using System.Windows.Forms;
using SMS_Search.Utils;

namespace SMS_Search.Services
{
    public class DialogService : IDialogService
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILoggerService _loggerService;

        public DialogService(ISettingsRepository settingsRepository, ILoggerService loggerService)
        {
            _settingsRepository = settingsRepository;
            _loggerService = loggerService;
        }

        public void ShowMessage(string message, string title)
        {
            ShowToast(message, title, ToastType.Info);
        }

        public void ShowError(string message, string title)
        {
            string logPath = _loggerService.GetCurrentLogPath();
            ShowToast(message, title, ToastType.Error, null, logPath);
        }

        public void ShowWarning(string message, string title)
        {
            string logPath = _loggerService.GetCurrentLogPath();
            ShowToast(message, title, ToastType.Warning, null, logPath);
        }

        public bool ShowConfirmation(string message, string title)
        {
            return System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public string? OpenFileDialog(string filter)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() == true) return dlg.FileName;
            return null;
        }

        public string? SaveFileDialog(string filter, string defaultName = "")
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = defaultName };
            if (dlg.ShowDialog() == true) return dlg.FileName;
            return null;
        }

        public string? PickColor(string? defaultColor = null)
        {
             using (var dlg = new ColorDialog())
             {
                 if (!string.IsNullOrEmpty(defaultColor))
                 {
                     try
                     {
                         dlg.Color = ColorTranslator.FromHtml(defaultColor);
                     }
                     catch { }
                 }

                 if (dlg.ShowDialog() == DialogResult.OK)
                 {
                     return ColorTranslator.ToHtml(dlg.Color);
                 }
             }
             return null;
        }

        public void ShowToast(string message, string title, ToastType type = ToastType.Info, string? details = null, string? filePath = null, System.Windows.Window? owner = null)
        {
            // Ensure UI thread access for creating window
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            void Show()
            {
                // Get Timeout
                int timeout = 5;
                string? val = _settingsRepository.GetValue(AppSettings.Sections.General, AppSettings.Keys.ToastTimeout);
                if (!string.IsNullOrEmpty(val) && int.TryParse(val, out int t))
                {
                    timeout = t;
                }

                var toast = new ToastWindow(message, title, type, timeout, details, filePath);

                var targetWindow = owner ?? System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(x => x.IsActive) ?? System.Windows.Application.Current.MainWindow;
                if (targetWindow != null && targetWindow.IsVisible)
                {
                    toast.Owner = targetWindow;
                }

                toast.Show();
            }

            if (dispatcher.CheckAccess())
            {
                Show();
            }
            else
            {
                dispatcher.BeginInvoke((Action)Show);
            }
        }

        public async Task<ExistingTableAction> ShowTableExistsPromptAsync(string tableName)
        {
            var app = System.Windows.Application.Current;
            var dispatcher = app?.Dispatcher;
            if (dispatcher != null)
            {
                return await dispatcher.InvokeAsync(() =>
                {
                    var dlg = new SMS_Search.Views.Windows.ImportTableExistsDialog(tableName);
                    var mainWindow = app?.MainWindow;
                    if (mainWindow != null && mainWindow.IsVisible)
                    {
                        dlg.Owner = mainWindow;
                    }

                    dlg.ShowDialog();
                    return dlg.Result;
                });
            }

            // Fallback if no UI thread context exists
            return ExistingTableAction.Skip;
        }

        public async Task<Models.MissingColumnDialogResult> ShowMissingColumnsPromptAsync(string tableName, System.Collections.Generic.List<Models.MissingColumnInfo> missingColumns, bool isTypeConfirmation = false)
        {
            var app = System.Windows.Application.Current;
            var dispatcher = app?.Dispatcher;
            if (dispatcher != null)
            {
                return await dispatcher.InvokeAsync(() =>
                {
                    var dlg = new Views.Windows.MissingColumnsDialog(tableName, missingColumns, isTypeConfirmation);
                    var mainWindow = app?.MainWindow;
                    if (mainWindow != null && mainWindow.IsVisible)
                    {
                        dlg.Owner = mainWindow;
                    }

                    dlg.ShowDialog();
                    return dlg.Result;
                });
            }

            // Fallback if no UI thread context exists
            return new Models.MissingColumnDialogResult { Action = Models.MissingColumnAction.Cancel, Columns = missingColumns };
        }

        public bool? ShowColumnPicker(SMS_Search.ViewModels.Settings.ColumnPickerViewModel viewModel)
        {
            var dlg = new Views.Windows.ColumnPickerWindow(viewModel);
            var activeWindow = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(x => x.IsActive && !(x is ToastWindow))
                ?? System.Windows.Application.Current?.MainWindow;
            if (activeWindow != null && activeWindow.IsVisible)
            {
                dlg.Owner = activeWindow;
            }
            return dlg.ShowDialog();
        }
        public bool TryPromptSaveQuery(string suggestedName, System.Collections.Generic.IEnumerable<string> categories, out string name, out string category, string title = "Save Query")
        {
            string resultName = string.Empty;
            string resultCategory = "General";
            bool confirmed = false;

            var dlg = new Window
            {
                Title = title,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };
            DialogPlacementHelper.ApplyOwnerAndPlacement(dlg);
            dlg.SetResourceReference(Window.BackgroundProperty, "AppBackgroundBrush");
            dlg.SetResourceReference(Window.ForegroundProperty, "PrimaryTextBrush");

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(14) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(75) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var nameLbl = new System.Windows.Controls.TextBlock { Text = "Name:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 8) };
            var nameBox = new System.Windows.Controls.TextBox { Text = suggestedName, Height = 26, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            System.Windows.Controls.Grid.SetRow(nameLbl, 0);
            System.Windows.Controls.Grid.SetColumn(nameLbl, 0);
            System.Windows.Controls.Grid.SetRow(nameBox, 0);
            System.Windows.Controls.Grid.SetColumn(nameBox, 1);

            var catLbl = new System.Windows.Controls.TextBlock { Text = "Category:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 12) };
            var catCombo = new System.Windows.Controls.ComboBox { IsEditable = true, Height = 26, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
            foreach (var c in categories) catCombo.Items.Add(c);
            catCombo.Text = "General";
            System.Windows.Controls.Grid.SetRow(catLbl, 1);
            System.Windows.Controls.Grid.SetColumn(catLbl, 0);
            System.Windows.Controls.Grid.SetRow(catCombo, 1);
            System.Windows.Controls.Grid.SetColumn(catCombo, 1);

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            var saveBtn = new System.Windows.Controls.Button { Content = "Save", Width = 78, Height = 26, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancelBtn = new System.Windows.Controls.Button { Content = "Cancel", Width = 78, Height = 26, IsCancel = true };
            saveBtn.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    nameBox.Focus();
                    return;
                }
                resultName = nameBox.Text.Trim();
                resultCategory = string.IsNullOrWhiteSpace(catCombo.Text) ? "General" : catCombo.Text.Trim();
                confirmed = true;
                dlg.DialogResult = true;
            };
            btnPanel.Children.Add(saveBtn);
            btnPanel.Children.Add(cancelBtn);
            System.Windows.Controls.Grid.SetRow(btnPanel, 2);
            System.Windows.Controls.Grid.SetColumnSpan(btnPanel, 2);

            grid.Children.Add(nameLbl);
            grid.Children.Add(nameBox);
            grid.Children.Add(catLbl);
            grid.Children.Add(catCombo);
            grid.Children.Add(btnPanel);
            dlg.Content = grid;

            dlg.Loaded += (_, _) =>
            {
                nameBox.Focus();
                nameBox.SelectAll();
            };

            dlg.ShowDialog();
            name = resultName;
            category = resultCategory;
            return confirmed;
        }
    }
}
