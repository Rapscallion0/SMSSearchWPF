using System;
using System.ComponentModel;
using System.Windows;
using SMS_Search.ViewModels;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.Data;
using SMS_Search.Views;
using Microsoft.Extensions.DependencyInjection;

namespace SMS_Search
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IConfigService _config;
        private readonly IStateService _state;
        private UnarchiveWindow? _unarchiveWindow;
        private System.Windows.Threading.DispatcherTimer _typingTimer;
        private string _lastTypedText = "";
        private bool _isDeleting = false;
        private string? _lastValidDatabase;

        public MainWindow(MainViewModel viewModel, IConfigService config, IStateService state)
        {
            InitializeComponent();
            _typingTimer = new System.Windows.Threading.DispatcherTimer();
            _typingTimer.Interval = TimeSpan.FromMilliseconds(300);
            _typingTimer.Tick += TypingTimer_Tick;

            _viewModel = viewModel;
            _config = config;
            _state = state;
            DataContext = viewModel;
            viewModel.RequestOpenSettings += OnRequestOpenSettings;
            viewModel.RequestOpenGs1Toolkit += OnRequestOpenGs1Toolkit;
            viewModel.RequestToggleUnarchiveWindow += OnRequestToggleUnarchiveWindow;

            this.SizeChanged += MainWindow_SizeChanged;
            this.LocationChanged += MainWindow_LocationChanged;
            this.Loaded += MainWindow_Loaded;
            this.Unloaded += MainWindow_Unloaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var watcher = App.Current.Services.GetService<SMS_Search.Services.Gs1.Gs1ClipboardWatcher>();
            watcher?.Start(this);
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            var watcher = App.Current.Services.GetService<SMS_Search.Services.Gs1.Gs1ClipboardWatcher>();
            watcher?.Stop();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ToastWindow.UpdateAllToastPositions(false);
        }

        private void MainStatusBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // First pass: show all to calculate needed space
            DatabaseLabel.Visibility = Visibility.Visible;
            JulianLabel.Visibility = Visibility.Visible;
            GregorianLabel.Visibility = Visibility.Visible;

            // Required space approximation based on layout elements
            double rightPanelWidth = RightStatusItem.ActualWidth;

            // Re-evaluate widths after making everything visible:
            LeftStatusPanel.UpdateLayout();
            MainStatusBar.UpdateLayout();

            double totalNeededWidth = LeftStatusPanel.ActualWidth + rightPanelWidth + 20; // 20 for margin/padding buffer

            if (MainStatusBar.ActualWidth < totalNeededWidth)
            {
                // Hide Database Label first
                DatabaseLabel.Visibility = Visibility.Collapsed;
                LeftStatusPanel.UpdateLayout();
                totalNeededWidth = LeftStatusPanel.ActualWidth + rightPanelWidth + 20;

                if (MainStatusBar.ActualWidth < totalNeededWidth)
                {
                    // If still not enough, hide Date labels
                    JulianLabel.Visibility = Visibility.Collapsed;
                    GregorianLabel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            ToastWindow.UpdateAllToastPositions(false);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            bool rememberSize = _config.GetValue("GENERAL", "MAIN_REMEMBER_SIZE") == "1";

            if (rememberSize)
            {
                // Restore Split Position
                if (double.TryParse(_state.GetValue("MAIN", "SEARCH_HEIGHT"), out double h))
                {
                    SearchRow.Height = new GridLength(h);
                }

                // Restore Size
                if (double.TryParse(_state.GetValue("MAIN", "LAST_W"), out double w)) Width = w;
                if (double.TryParse(_state.GetValue("MAIN", "LAST_H"), out double hVal)) Height = hVal;
            }

            if (Enum.TryParse(_config.GetValue("GENERAL", "MAIN_STARTUP_LOCATION"), out StartupLocationMode mode))
            {
                // good
            }
            else
            {
                mode = StartupLocationMode.Last;
            }

            double? lastX = null;
            if (double.TryParse(_state.GetValue("MAIN", "LAST_X"), out double x)) lastX = x;

            double? lastY = null;
            if (double.TryParse(_state.GetValue("MAIN", "LAST_Y"), out double y)) lastY = y;

            WindowPositioner.ApplyStartupLocation(this, mode, lastX, lastY);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _state.SetValue("MAIN", "LAST_TAB", _viewModel.SearchVm.SelectedMode.ToString());

            bool rememberSize = _config.GetValue("GENERAL", "MAIN_REMEMBER_SIZE") == "1";

            if (rememberSize)
            {
                _state.SetValue("MAIN", "SEARCH_HEIGHT", SearchRow.Height.Value.ToString());
            }
            else
            {
                _state.RemoveValue("MAIN", "SEARCH_HEIGHT");
                _state.RemoveValue("MAIN", "LAST_W");
                _state.RemoveValue("MAIN", "LAST_H");
            }

            if (WindowState == WindowState.Normal)
            {
                _state.SetValue("MAIN", "LAST_X", Left.ToString());
                _state.SetValue("MAIN", "LAST_Y", Top.ToString());

                if (rememberSize)
                {
                    _state.SetValue("MAIN", "LAST_W", ActualWidth.ToString());
                    _state.SetValue("MAIN", "LAST_H", ActualHeight.ToString());
                }
            }
            _state.Save();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _unarchiveWindow?.Close();

            if (_typingTimer != null)
            {
                _typingTimer.Stop();
                _typingTimer.Tick -= TypingTimer_Tick;
            }
        }

        private void OnRequestToggleUnarchiveWindow(bool isVisible)
        {
            if (isVisible)
            {
                if (_unarchiveWindow == null || !_unarchiveWindow.IsLoaded)
                {
                    _unarchiveWindow = App.Current.Services.GetRequiredService<UnarchiveWindow>();
                    _unarchiveWindow.Closed += (s, e) =>
                    {
                        _viewModel.IsUnarchiveTargetVisible = false;
                        _unarchiveWindow = null;
                    };
                    _unarchiveWindow.Show();
                }
            }
            else
            {
                _unarchiveWindow?.Close();
                _unarchiveWindow = null;
            }
        }

        private void OnRequestOpenGs1Toolkit()
        {
            var gs1Window = App.Current.Services.GetRequiredService<SMS_Search.Views.Gs1.Gs1ToolkitWindow>();
            gs1Window.DataContext = App.Current.Services.GetRequiredService<SMS_Search.ViewModels.Gs1.Gs1ToolkitViewModel>();
            gs1Window.Owner = this;
            gs1Window.Show();
        }

        private void OnRequestOpenSettings()
        {
            MaskOverlay.Visibility = Visibility.Visible;
            try
            {
                var win = App.Current.Services.GetRequiredService<ModernSettingsWindow>();
                win.Owner = this;
                win.ShowDialog();
            }
            finally
            {
                MaskOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            _typingTimer.Stop();
            if (DataContext is MainViewModel vm)
            {
                var textBox = DatabaseComboBox.Template.FindName("PART_EditableTextBox", DatabaseComboBox) as System.Windows.Controls.TextBox;
                if (textBox == null) return;

                string text = textBox.Text;
                int caretIndex = textBox.CaretIndex;

                string actualTypedText = text;
                if (textBox.SelectionLength > 0 && textBox.SelectionStart + textBox.SelectionLength == text.Length)
                {
                    actualTypedText = text.Substring(0, textBox.SelectionStart);
                }

                vm.FilterDatabases(actualTypedText);

                bool isTypingForward = !_isDeleting && actualTypedText.Length > _lastTypedText.Length && actualTypedText.StartsWith(_lastTypedText, StringComparison.OrdinalIgnoreCase);
                _lastTypedText = actualTypedText;
                _isDeleting = false;

                string? startsWithMatch = null;
                if (isTypingForward && !string.IsNullOrEmpty(actualTypedText))
                {
                    foreach (var item in vm.DatabasesView)
                    {
                        if (item is string str && str.StartsWith(actualTypedText, StringComparison.OrdinalIgnoreCase))
                        {
                            startsWithMatch = str;
                            break;
                        }
                    }
                }

                if (startsWithMatch != null)
                {
                    vm.SelectedDatabase = startsWithMatch;
                    string remaining = startsWithMatch.Substring(actualTypedText.Length);

                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                    {
                        textBox.Text = actualTypedText + remaining;
                        textBox.SelectionStart = actualTypedText.Length;
                        textBox.SelectionLength = remaining.Length;
                    }));
                }
                else
                {
                    var exactMatch = System.Linq.Enumerable.FirstOrDefault(vm.Databases, t => t.Equals(actualTypedText, StringComparison.OrdinalIgnoreCase));
                    if (exactMatch != null)
                    {
                        if (vm.SelectedDatabase != exactMatch)
                        {
                            vm.SelectedDatabase = exactMatch;
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                            {
                                textBox.SelectionLength = 0;
                                textBox.CaretIndex = textBox.Text.Length;
                            }));
                        }
                    }
                    else
                    {
                        if (vm.SelectedDatabase != null)
                        {
                            vm.SelectedDatabase = null;
                        }

                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                        {
                            if (textBox.Text != actualTypedText)
                            {
                                textBox.Text = actualTypedText;
                            }
                            textBox.SelectionLength = 0;
                            textBox.CaretIndex = Math.Min(caretIndex, textBox.Text.Length);

                            if (string.IsNullOrEmpty(actualTypedText) && !DatabaseComboBox.IsDropDownOpen)
                            {
                                DatabaseComboBox.IsDropDownOpen = true;
                            }
                        }));
                    }
                }
            }
        }

        private void DatabaseComboBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down ||
                e.Key == System.Windows.Input.Key.Left || e.Key == System.Windows.Input.Key.Right ||
                e.Key == System.Windows.Input.Key.Home || e.Key == System.Windows.Input.Key.End ||
                e.Key == System.Windows.Input.Key.PageUp || e.Key == System.Windows.Input.Key.PageDown ||
                e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab ||
                e.Key == System.Windows.Input.Key.Escape)
            {
                return;
            }

            _isDeleting = e.Key == System.Windows.Input.Key.Back || e.Key == System.Windows.Input.Key.Delete;

            _typingTimer.Stop();
            _typingTimer.Start();
        }

        private void DatabaseComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
            {
                if (sender is System.Windows.Controls.ComboBox cmb)
                {
                    if (cmb.SelectedItem == null && cmb.Items.Count > 0)
                    {
                        cmb.SelectedItem = cmb.Items[0];
                    }
                }
            }
            else if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down)
            {
                if (sender is System.Windows.Controls.ComboBox cmb && cmb.IsEditable)
                {
                    if (cmb.Items.Count > 0)
                    {
                        int currentIndex = cmb.SelectedIndex;

                        if (currentIndex == -1 && !string.IsNullOrEmpty(cmb.Text))
                        {
                            var match = System.Linq.Enumerable.FirstOrDefault(cmb.Items.Cast<string>(), i => i.StartsWith(cmb.Text, StringComparison.OrdinalIgnoreCase) || i.IndexOf(cmb.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (match != null)
                            {
                                currentIndex = cmb.Items.IndexOf(match);
                            }
                        }

                        if (e.Key == System.Windows.Input.Key.Down)
                        {
                            currentIndex++;
                            if (currentIndex >= cmb.Items.Count)
                                currentIndex = cmb.Items.Count - 1;
                        }
                        else if (e.Key == System.Windows.Input.Key.Up)
                        {
                            currentIndex--;
                            if (currentIndex < 0)
                                currentIndex = 0;
                        }

                        cmb.SelectedIndex = currentIndex;

                        var textBox = cmb.Template.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
                        if (textBox != null && cmb.SelectedItem is string selectedStr)
                        {
                            string typedText = _lastTypedText;

                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                            {
                                if (!string.IsNullOrEmpty(typedText) && selectedStr.StartsWith(typedText, StringComparison.OrdinalIgnoreCase))
                                {
                                    textBox.Text = typedText + selectedStr.Substring(typedText.Length);
                                    textBox.SelectionStart = typedText.Length;
                                    textBox.SelectionLength = selectedStr.Length - typedText.Length;
                                }
                                else
                                {
                                    textBox.Text = selectedStr;
                                    textBox.SelectAll();
                                }
                            }));
                        }

                        e.Handled = true;
                    }
                }
            }
        }

        private void DatabaseComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                _lastValidDatabase = vm.SelectedDatabase;
            }

            var textBox = DatabaseComboBox.Template.FindName("PART_EditableTextBox", DatabaseComboBox) as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    textBox.SelectAll();
                }));
            }
        }

        private void DatabaseComboBox_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var textBox = DatabaseComboBox.Template.FindName("PART_EditableTextBox", DatabaseComboBox) as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                textBox.SelectAll();
                e.Handled = true;
            }
        }

        private void DatabaseComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                string text = DatabaseComboBox.Text;
                if (!vm.Databases.Contains(text))
                {
                    // If text is empty or not in the list, revert to the last valid selection.
                    if (vm.SelectedDatabase != _lastValidDatabase)
                    {
                        vm.SelectedDatabase = _lastValidDatabase;
                    }
                    DatabaseComboBox.Text = _lastValidDatabase ?? "";
                }
                else
                {
                    _lastValidDatabase = text;
                }
            }
        }
    }
}
