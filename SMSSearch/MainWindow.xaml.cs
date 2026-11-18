using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Core.Serialization.Dto;
using AvalonDock.Layout;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using SMS_Search.ViewModels;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.Data;
using SMS_Search.Views;
using SMS_Search.ViewModels.Gs1;
using SMS_Search.Views.Gs1;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MenuItem = System.Windows.Controls.MenuItem;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace SMS_Search
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IConfigService _config;
        private readonly IStateService _state;
        private readonly SMS_Search.Services.Gs1.Gs1ClipboardWatcher _clipboardWatcher;
        private readonly Func<UnarchiveWindow> _unarchiveWindowFactory;
        private readonly Func<Gs1ToolkitWindow> _gs1ToolkitWindowFactory;
        private readonly Func<Gs1ToolkitViewModel> _gs1ToolkitViewModelFactory;
        private readonly Func<ModernSettingsWindow> _settingsWindowFactory;
        private readonly Func<EncryptionUtilityWindow> _encryptionWindowFactory;
        private readonly Func<EncryptionUtilityViewModel> _encryptionViewModelFactory;
        private UnarchiveWindow? _unarchiveWindow;
        private System.Windows.Threading.DispatcherTimer _typingTimer;
        private string _lastTypedText = "";
        private bool _isDeleting = false;
        private string? _lastValidDatabase;
        private readonly IWorkspaceLayoutService? _layoutService;
        private string? _defaultLayoutXml;
        private System.Windows.Threading.DispatcherTimer? _layoutAutoSaveTimer;
        private bool _isApplyingLayout;
        private string? _activeLayoutProfileName;
        private bool _isLayoutProfileDirty;
        private Views.Windows.LayoutManagerWindow? _layoutManagerWindow;

        internal string? ActiveLayoutProfileName => _activeLayoutProfileName;
        internal bool IsLayoutProfileDirty => _isLayoutProfileDirty;

        public MainWindow(
            MainViewModel viewModel,
            IConfigService config,
            IStateService state,
            SMS_Search.Services.Gs1.Gs1ClipboardWatcher clipboardWatcher,
            Func<UnarchiveWindow> unarchiveWindowFactory,
            Func<Gs1ToolkitWindow> gs1ToolkitWindowFactory,
            Func<Gs1ToolkitViewModel> gs1ToolkitViewModelFactory,
            Func<ModernSettingsWindow> settingsWindowFactory,
            Func<EncryptionUtilityWindow> encryptionWindowFactory,
            Func<EncryptionUtilityViewModel> encryptionViewModelFactory,
            IWorkspaceLayoutService? layoutService = null)
        {
            InitializeComponent();
            _layoutService = layoutService;
            _typingTimer = new System.Windows.Threading.DispatcherTimer();
            _typingTimer.Interval = TimeSpan.FromMilliseconds(300);
            _typingTimer.Tick += TypingTimer_Tick;

            _viewModel = viewModel;
            _config = config;
            _state = state;
            _clipboardWatcher = clipboardWatcher;
            _unarchiveWindowFactory = unarchiveWindowFactory;
            _gs1ToolkitWindowFactory = gs1ToolkitWindowFactory;
            _gs1ToolkitViewModelFactory = gs1ToolkitViewModelFactory;
            _settingsWindowFactory = settingsWindowFactory;
            _encryptionWindowFactory = encryptionWindowFactory;
            _encryptionViewModelFactory = encryptionViewModelFactory;
            DataContext = viewModel;
            viewModel.RequestOpenSettings += OnRequestOpenSettings;
            viewModel.RequestOpenEncryptionUtility += OnRequestOpenEncryptionUtility;
            viewModel.RequestOpenGs1Toolkit += OnRequestOpenGs1Toolkit;
            viewModel.RequestToggleUnarchiveWindow += OnRequestToggleUnarchiveWindow;

            this.SizeChanged += MainWindow_SizeChanged;
            this.LocationChanged += MainWindow_LocationChanged;
            this.Loaded += MainWindow_Loaded;
            this.Unloaded += MainWindow_Unloaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _clipboardWatcher.Start(this);

            _layoutAutoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _layoutAutoSaveTimer.Tick += LayoutAutoSaveTimer_Tick;

            _defaultLayoutXml = SerializeLayout();

            var themeService = (System.Windows.Application.Current as App)?.Services?.GetService<IThemeService>();
            ApplyDockTheme(themeService?.IsDarkTheme ?? false);

            string? startupProfileName = _layoutService?.GetStartupLayoutName();
            if (!string.IsNullOrWhiteSpace(startupProfileName))
            {
                if (WorkspaceLayoutService.IsBuiltInDefault(startupProfileName))
                {
                    _activeLayoutProfileName = WorkspaceLayoutService.BuiltInDefaultName;
                    _isLayoutProfileDirty = false;
                }
                else
                {
                    var startupProfile = _layoutService?.GetProfile(startupProfileName);
                    if (startupProfile != null && !string.IsNullOrWhiteSpace(startupProfile.LayoutXml))
                    {
                        DeserializeLayout(startupProfile.LayoutXml);
                        if (!string.IsNullOrWhiteSpace(startupProfile.WindowState) &&
                            Enum.TryParse<WindowState>(startupProfile.WindowState, out var st) &&
                            st != WindowState.Minimized)
                        {
                            WindowState = st;
                        }
                        _activeLayoutProfileName = startupProfile.Name;
                        _isLayoutProfileDirty = false;
                    }
                }
            }
            else
            {
                string? savedXml = _layoutService?.LoadActiveLayout();
                if (!string.IsNullOrWhiteSpace(savedXml))
                {
                    DeserializeLayout(savedXml);
                }

                var activeWindowState = _layoutService?.LoadActiveWindowState();
                if (activeWindowState != null)
                {
                    if (!string.IsNullOrWhiteSpace(activeWindowState.ActiveProfileName))
                    {
                        _activeLayoutProfileName = activeWindowState.ActiveProfileName;
                    }

                    if (!string.IsNullOrWhiteSpace(activeWindowState.WindowState) &&
                        Enum.TryParse<WindowState>(activeWindowState.WindowState, out var parsedState) &&
                        parsedState != WindowState.Minimized)
                    {
                        WindowState = parsedState;
                    }
                }
            }

            ApplyPanelMinimumSizes();
            Dispatcher.InvokeAsync(ApplyVisualConstraints, System.Windows.Threading.DispatcherPriority.Loaded);
            Dispatcher.InvokeAsync(ApplyStartupSearchPreferences, System.Windows.Threading.DispatcherPriority.Loaded);
            DockManager.LayoutUpdated += (_, _) => ApplyVisualConstraints();
            DockManager.AddHandler(System.Windows.Controls.TabItem.MouseEnterEvent, new System.Windows.Input.MouseEventHandler(OnDockTabMouseEnter), true);

            WeakReferenceMessenger.Default.Register<ShowPanelMessage>(this, (r, m) =>
            {
                Dispatcher.InvokeAsync(() => ShowAndActivatePanel(m.Value));
            });

            WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, (r, m) =>
            {
                Dispatcher.InvokeAsync(() => ApplyDockTheme(m.Value));
            });
        }

        private static void OnDockTabMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source) return;
            var tabItem = FindVisualParent<System.Windows.Controls.TabItem>(source);
            if (tabItem == null) return;

            string? title = tabItem.DataContext switch
            {
                LayoutContent lc => lc.Title,
                AvalonDock.Controls.LayoutItem li => li.Title,
                _ => (tabItem.Content as LayoutContent)?.Title
            };

            if (!string.IsNullOrWhiteSpace(title) && !Equals(tabItem.ToolTip, title))
            {
                tabItem.ToolTip = title;
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void ApplyDockTheme(bool isDark)
        {
            DockManager.Theme = isDark
                ? new AvalonDock.Themes.Vs2013DarkTheme()
                : new AvalonDock.Themes.Vs2013LightTheme();
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_layoutAutoSaveTimer != null)
            {
                _layoutAutoSaveTimer.Stop();
                _layoutAutoSaveTimer.Tick -= LayoutAutoSaveTimer_Tick;
                _layoutAutoSaveTimer = null;
            }
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _clipboardWatcher.Stop();
        }

        private void LayoutAutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            _layoutAutoSaveTimer?.Stop();
            SaveActiveWorkspaceSnapshot();
        }

        private void SaveActiveWorkspaceSnapshot()
        {
            string? layoutXml = SerializeLayout();
            if (!string.IsNullOrWhiteSpace(layoutXml))
            {
                _layoutService?.SaveActiveLayout(layoutXml);
            }

            var bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, ActualWidth, ActualHeight)
                : RestoreBounds;

            _layoutService?.SaveActiveWindowState(new ActiveWorkspaceState
            {
                WindowLeft = bounds.Left,
                WindowTop = bounds.Top,
                WindowWidth = bounds.Width,
                WindowHeight = bounds.Height,
                WindowState = WindowState.ToString(),
                ActiveProfileName = _activeLayoutProfileName
            });
        }

        private void DockManager_LayoutChanged(object? sender, EventArgs e)
        {
            if (_isApplyingLayout || !IsLoaded) return;

            ApplyPanelMinimumSizes();
            Dispatcher.InvokeAsync(ApplyVisualConstraints, System.Windows.Threading.DispatcherPriority.Loaded);
            Dispatcher.InvokeAsync(ApplyStartupSearchPreferences, System.Windows.Threading.DispatcherPriority.Loaded);
            DockManager.LayoutUpdated += (_, _) => ApplyVisualConstraints();
            DockManager.AddHandler(System.Windows.Controls.TabItem.MouseEnterEvent, new System.Windows.Input.MouseEventHandler(OnDockTabMouseEnter), true);

            if (!string.IsNullOrEmpty(_activeLayoutProfileName))
            {
                _isLayoutProfileDirty = true;
            }

            _layoutAutoSaveTimer?.Stop();
            _layoutAutoSaveTimer?.Start();
            _layoutManagerWindow?.RefreshState();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ToastWindow.UpdateAllToastPositions(false, this);
            if (!_isApplyingLayout && IsLoaded && !string.IsNullOrEmpty(_activeLayoutProfileName))
            {
                _isLayoutProfileDirty = true;
                _layoutManagerWindow?.RefreshState();
            }
        }

        private void MainStatusBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DatabaseLabel.Visibility = Visibility.Visible;
            LeftStatusPanel.UpdateLayout();
            MainStatusBar.UpdateLayout();

            if (MainStatusBar.ActualWidth < LeftStatusPanel.ActualWidth + 20)
            {
                DatabaseLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            ToastWindow.UpdateAllToastPositions(false, this);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (!Enum.TryParse(_config.GetValue(AppSettings.Sections.General, AppSettings.Keys.MainStartupLocation), out StartupLocationMode configuredMode))
            {
                configuredMode = StartupLocationMode.Last;
            }

            double? stateX = null;
            if (double.TryParse(_state.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastX), out double sx)) stateX = sx;

            double? stateY = null;
            if (double.TryParse(_state.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastY), out double sy)) stateY = sy;

            string? startupProfileName = _layoutService?.GetStartupLayoutName();
            if (!string.IsNullOrWhiteSpace(startupProfileName))
            {
                if (WorkspaceLayoutService.IsBuiltInDefault(startupProfileName))
                {
                    Width = 1017;
                    Height = 757;
                    if (_layoutService?.GetDefaultRememberWindowPosition() == true && stateX.HasValue && stateY.HasValue)
                    {
                        WindowPositioner.ApplyStartupLocation(this, StartupLocationMode.Last, stateX, stateY);
                    }
                    else
                    {
                        WindowPositioner.ApplyStartupLocation(this, StartupLocationMode.Active, null, null);
                    }
                    return;
                }

                var startupProfile = _layoutService?.GetProfile(startupProfileName);
                if (startupProfile != null)
                {
                    if (startupProfile.WindowWidth is > 0 && startupProfile.WindowHeight is > 0)
                    {
                        Width = Math.Max(MinWidth, startupProfile.WindowWidth.Value);
                        Height = Math.Max(MinHeight, startupProfile.WindowHeight.Value);
                    }

                    if (startupProfile.RememberWindowPosition &&
                        startupProfile.WindowLeft.HasValue &&
                        startupProfile.WindowTop.HasValue)
                    {
                        WindowPositioner.ApplyStartupLocation(this, StartupLocationMode.Last, startupProfile.WindowLeft, startupProfile.WindowTop);
                    }
                    else
                    {
                        WindowPositioner.ApplyStartupLocation(this, configuredMode, stateX, stateY);
                    }
                    return;
                }
            }

            var activeWindowState = _layoutService?.LoadActiveWindowState();
            if (activeWindowState?.WindowWidth is > 0 && activeWindowState.WindowHeight is > 0)
            {
                Width = Math.Max(MinWidth, activeWindowState.WindowWidth.Value);
                Height = Math.Max(MinHeight, activeWindowState.WindowHeight.Value);
            }
            else
            {
                if (double.TryParse(_state.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastW), out double w)) Width = w;
                if (double.TryParse(_state.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastH), out double hVal)) Height = hVal;
            }

            double? lastX = activeWindowState?.WindowLeft ?? stateX;
            double? lastY = activeWindowState?.WindowTop ?? stateY;
            WindowPositioner.ApplyStartupLocation(this, configuredMode, lastX, lastY);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isLayoutProfileDirty &&
                !string.IsNullOrEmpty(_activeLayoutProfileName) &&
                !WorkspaceLayoutService.IsBuiltInDefault(_activeLayoutProfileName))
            {
                var activeProfile = _layoutService?.GetProfile(_activeLayoutProfileName);
                if (activeProfile != null && !activeProfile.SuppressCloseSavePrompt)
                {
                    var (choice, dontAskAgain) = ShowThemedConfirmDialogWithOption(
                        this,
                        "Unsaved Layout Profile",
                        $"Layout profile '{_activeLayoutProfileName}' has unsaved changes.\n\nSave changes to this profile before closing?",
                        "Don't ask again for this profile",
                        includeCancel: true);

                    if (choice == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }

                    if (dontAskAgain)
                    {
                        _layoutService?.SetProfileSuppressCloseSavePrompt(_activeLayoutProfileName, true);
                    }

                    if (choice == MessageBoxResult.Yes)
                    {
                        string? profileXml = SerializeLayout();
                        if (!string.IsNullOrWhiteSpace(profileXml))
                        {
                            SaveProfileWithWindowBounds(_activeLayoutProfileName, profileXml);
                        }
                    }
                }
                _isLayoutProfileDirty = false;
            }

            base.OnClosing(e);

            _layoutAutoSaveTimer?.Stop();
            string lastTabKey = _viewModel.SearchVm.IsRecordsTabActive
                ? "Records"
                : _viewModel.SearchVm.SelectedMode.ToString();
            _state.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastTab, lastTabKey);
            SaveActiveWorkspaceSnapshot();

            if (WindowState == WindowState.Normal)
            {
                _state.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastX, Left.ToString());
                _state.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastY, Top.ToString());
                _state.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastW, ActualWidth.ToString());
                _state.SetValue(AppSettings.Sections.Main, AppSettings.Keys.LastH, ActualHeight.ToString());
            }
            _state.Save();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _unarchiveWindow?.Close();

            this.SizeChanged -= MainWindow_SizeChanged;
            this.LocationChanged -= MainWindow_LocationChanged;
            this.Loaded -= MainWindow_Loaded;
            this.Unloaded -= MainWindow_Unloaded;

            _viewModel.RequestOpenSettings -= OnRequestOpenSettings;
            _viewModel.RequestOpenEncryptionUtility -= OnRequestOpenEncryptionUtility;
            _viewModel.RequestOpenGs1Toolkit -= OnRequestOpenGs1Toolkit;
            _viewModel.RequestToggleUnarchiveWindow -= OnRequestToggleUnarchiveWindow;

            if (_typingTimer != null)
            {
                _typingTimer.Stop();
                _typingTimer.Tick -= TypingTimer_Tick;
            }

            (_viewModel as IDisposable)?.Dispose();
        }

        private void OnRequestToggleUnarchiveWindow(bool isVisible)
        {
            if (isVisible)
            {
                if (_unarchiveWindow == null || !_unarchiveWindow.IsLoaded)
                {
                    _unarchiveWindow = _unarchiveWindowFactory();
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
            var gs1Window = System.Windows.Application.Current.Windows.OfType<SMS_Search.Views.Gs1.Gs1ToolkitWindow>().FirstOrDefault();

            if (gs1Window != null && gs1Window.IsLoaded)
            {
                if (gs1Window.WindowState == WindowState.Minimized)
                {
                    gs1Window.WindowState = WindowState.Normal;
                }
                gs1Window.Activate();
            }
            else
            {
                gs1Window = _gs1ToolkitWindowFactory();
                gs1Window.DataContext = _gs1ToolkitViewModelFactory();
                gs1Window.Show();
            }
        }

        private void OnRequestOpenEncryptionUtility()
        {
            var encryptWin = _encryptionWindowFactory();
            encryptWin.DataContext = _encryptionViewModelFactory();
            DialogPlacementHelper.ApplyOwnerAndPlacement(encryptWin, this);
            encryptWin.ShowDialog();
        }

        private void OnRequestOpenSettings()
        {
            MaskOverlay.Visibility = Visibility.Visible;
            try
            {
                var win = _settingsWindowFactory();
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

                bool isTypingForward = !_isDeleting && actualTypedText.Length >= _lastTypedText.Length && actualTypedText.StartsWith(_lastTypedText, StringComparison.OrdinalIgnoreCase);
                _lastTypedText = actualTypedText;
                _isDeleting = false;

                string? startsWithMatch = null;
                if (!string.IsNullOrEmpty(actualTypedText))
                {
                    foreach (var item in vm.Databases)
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
            if (e.Key == System.Windows.Input.Key.Back)
            {
                if (sender is System.Windows.Controls.ComboBox cmb)
                {
                    var textBox = cmb.Template.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
                    if (textBox != null && textBox.SelectionLength > 0 && textBox.SelectionStart + textBox.SelectionLength == textBox.Text.Length)
                    {
                        if (textBox.SelectionStart > 0)
                        {
                            string newText = textBox.Text.Substring(0, textBox.SelectionStart - 1);
                            textBox.Text = newText;
                            textBox.CaretIndex = newText.Length;
                            _lastTypedText = newText;
                            _isDeleting = false;
                            _typingTimer.Stop();
                            _typingTimer.Start();
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
            else if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
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

        private string? SerializeLayout()
        {
            try
            {
                var serializer = new WorkspaceDtoSerializer(DockManager);
                using var sw = new StringWriter();
                serializer.Serialize(sw);
                return sw.ToString();
            }
            catch (Exception ex)
            {
                (System.Windows.Application.Current as App)?.Services?.GetService<ILoggerService>()?.LogError("Failed to serialize workspace layout", ex);
                return null;
            }
        }

        private void DeserializeLayout(string layoutXml)
        {
            _isApplyingLayout = true;
            try
            {
                var serializer = new WorkspaceDtoSerializer(DockManager);
                serializer.LayoutSerializationCallback += (s, args) =>
                {
                    args.Content = args.Model.ContentId switch
                    {
                        "SearchCriteria" => new SearchView { DataContext = _viewModel.SearchVm },
                        "SqlEditor" => new Views.Panels.SqlEditorPanelView(),
                        "Results" => new Views.Panels.ResultsPanelView(),
                        "RowInspector" => new Views.Panels.RowInspectorPanelView(),
                        "SavedQueries" => new Views.Panels.SavedQueriesPanelView(),
                        "SelectionStats" => new Views.Panels.SelectionStatsPanelView(),
                        "DateConverter" => new Views.Panels.DateConverterPanelView(),
                        _ => args.Content
                    };
                };

                using var sr = new StringReader(layoutXml);
                serializer.Deserialize(sr);

                if (FindAnchorable("Results") == null && !string.IsNullOrWhiteSpace(_defaultLayoutXml) && layoutXml != _defaultLayoutXml)
                {
                    DeserializeLayout(_defaultLayoutXml);
                }

                ApplyPanelMinimumSizes();
                Dispatcher.InvokeAsync(ApplyVisualConstraints, System.Windows.Threading.DispatcherPriority.Loaded);
            Dispatcher.InvokeAsync(ApplyStartupSearchPreferences, System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                (System.Windows.Application.Current as App)?.Services?.GetService<ILoggerService>()?.LogWarning("Failed to deserialize workspace layout ({Message}); falling back to default.", ex.Message);
                if (!string.IsNullOrWhiteSpace(_defaultLayoutXml) && layoutXml != _defaultLayoutXml)
                {
                    DeserializeLayout(_defaultLayoutXml);
                }
            }
            finally
            {
                _isApplyingLayout = false;
            }
        }

        private LayoutAnchorable? FindAnchorable(string contentId)
        {
            return DockManager.Layout?
                .Descendents()
                .OfType<LayoutAnchorable>()
                .FirstOrDefault(a => string.Equals(a.ContentId, contentId, StringComparison.OrdinalIgnoreCase));
        }

        private void ShowAndActivatePanel(string contentId)
        {
            var pane = FindAnchorable(contentId);
            if (pane == null) return;

            if (pane.IsHidden)
            {
                pane.Show();
            }
            pane.IsSelected = true;
            pane.IsActive = true;
        }

        private void ToolsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, sender)) return;
            bool ctrlShift = (System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift)) ==
                             (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift);
            MenuEncryptionUtility.Visibility = (_viewModel.IsEncryptionUtilityUnlocked || ctrlShift)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void PanelsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, sender)) return;
            UpdatePanelMenuItem(MenuPanelSearchCriteria, "SearchCriteria");
            UpdatePanelMenuItem(MenuPanelSqlEditor, "SqlEditor");
            UpdatePanelMenuItem(MenuPanelResults, "Results");
            UpdatePanelMenuItem(MenuPanelRowInspector, "RowInspector");
            UpdatePanelMenuItem(MenuPanelSavedQueries, "SavedQueries");
            UpdatePanelMenuItem(MenuPanelSelectionStats, "SelectionStats");
            UpdatePanelMenuItem(MenuPanelDateConverter, "DateConverter");
        }

        private void UpdatePanelMenuItem(MenuItem item, string contentId)
        {
            var pane = FindAnchorable(contentId);
            item.IsChecked = pane != null && !pane.IsHidden;
        }

        private void TogglePanelMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item || item.Tag is not string contentId) return;

            var pane = FindAnchorable(contentId);
            if (pane == null) return;

            if (pane.IsHidden)
            {
                pane.Show();
                pane.IsSelected = true;
                pane.IsActive = true;
            }
            else
            {
                pane.Hide();
            }
        }

        private void ManageLayoutsMenu_Click(object sender, RoutedEventArgs e)
        {
            OpenLayoutManager();
        }

        public void OpenLayoutManager()
        {
            if (_layoutService == null) return;

            if (_layoutManagerWindow != null)
            {
                if (_layoutManagerWindow.WindowState == WindowState.Minimized)
                {
                    _layoutManagerWindow.WindowState = WindowState.Normal;
                }
                _layoutManagerWindow.Activate();
                _layoutManagerWindow.RefreshState();
                return;
            }

            _layoutManagerWindow = new Views.Windows.LayoutManagerWindow(this, _layoutService)
            {
                Owner = this
            };
            _layoutManagerWindow.Closed += (_, _) => _layoutManagerWindow = null;
            _layoutManagerWindow.Show();
        }

        internal void ApplyLayoutFromManager(string name)
        {
            if (WorkspaceLayoutService.IsBuiltInDefault(name))
            {
                if (!string.IsNullOrWhiteSpace(_defaultLayoutXml))
                {
                    DeserializeLayout(_defaultLayoutXml);
                    WindowState = WindowState.Normal;
                    Width = 1017;
                    Height = 757;
                    if (_layoutService?.GetDefaultRememberWindowPosition() != true)
                    {
                        WindowPositioner.ApplyStartupLocation(this, StartupLocationMode.Active, null, null);
                    }
                    _activeLayoutProfileName = WorkspaceLayoutService.BuiltInDefaultName;
                    _isLayoutProfileDirty = false;
                    SaveActiveWorkspaceSnapshot();
                    _layoutManagerWindow?.RefreshState(WorkspaceLayoutService.BuiltInDefaultName);
                    if (DataContext is MainViewModel defaultVm)
                    {
                        var prefs = _layoutService?.GetDefaultSearchPreferences()
                            ?? (SMS_Search.Data.LayoutSearchTabPreference.Function, SMS_Search.Data.LayoutSearchModePreference.Simple);
                        defaultVm.SearchVm.ApplyLayoutSearchPreferences(prefs.Tab, prefs.Mode);
                    }
                }
                return;
            }

            var selectedProfile = _layoutService?.GetProfile(name);
            if (selectedProfile != null && !string.IsNullOrWhiteSpace(selectedProfile.LayoutXml))
            {
                DeserializeLayout(selectedProfile.LayoutXml);
                ApplyWindowBoundsFromProfile(selectedProfile, selectedProfile.RememberWindowPosition);
                _activeLayoutProfileName = selectedProfile.Name;
                _isLayoutProfileDirty = false;
                SaveActiveWorkspaceSnapshot();
                _layoutManagerWindow?.RefreshState(selectedProfile.Name);
                if (DataContext is MainViewModel profileVm)
                {
                    profileVm.SearchVm.ApplyLayoutSearchPreferences(selectedProfile.SearchTabPreference, selectedProfile.SearchModePreference);
                }
            }
        }

        private void ApplyStartupSearchPreferences()
        {
            if (DataContext is not MainViewModel mvm) return;
            string? startupProfileName = _layoutService?.GetStartupLayoutName();
            if (string.IsNullOrWhiteSpace(startupProfileName)) return;

            if (WorkspaceLayoutService.IsBuiltInDefault(startupProfileName))
            {
                var prefs = _layoutService?.GetDefaultSearchPreferences()
                    ?? (SMS_Search.Data.LayoutSearchTabPreference.Function, SMS_Search.Data.LayoutSearchModePreference.Simple);
                mvm.SearchVm.ApplyLayoutSearchPreferences(prefs.Tab, prefs.Mode);
            }
            else
            {
                var profile = _layoutService?.GetProfile(startupProfileName);
                if (profile != null)
                {
                    mvm.SearchVm.ApplyLayoutSearchPreferences(profile.SearchTabPreference, profile.SearchModePreference);
                }
            }
        }

        internal void SaveCurrentToProfileFromManager(string name)
        {
            if (WorkspaceLayoutService.IsBuiltInDefault(name)) return;
            string? xml = SerializeLayout();
            if (string.IsNullOrWhiteSpace(xml)) return;

            SaveProfileWithWindowBounds(name, xml);
            _activeLayoutProfileName = name.Trim();
            _isLayoutProfileDirty = false;
            SaveActiveWorkspaceSnapshot();
            var dialogService = (System.Windows.Application.Current as App)?.Services?.GetService<IDialogService>();
            dialogService?.ShowToast($"Saved layout '{_activeLayoutProfileName}'", "Layouts", ToastType.Success);
        }

        internal void DuplicateLayoutFromManager(string sourceName, string newName)
        {
            if (_layoutService == null || WorkspaceLayoutService.IsBuiltInDefault(newName)) return;

            if (WorkspaceLayoutService.IsBuiltInDefault(sourceName))
            {
                string? xml = _defaultLayoutXml ?? SerializeLayout();
                if (string.IsNullOrWhiteSpace(xml)) return;

                var bounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, ActualWidth, ActualHeight)
                    : RestoreBounds;

                _layoutService.SaveProfile(newName, xml, bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState.ToString(), rememberWindowPosition: AreAllPanelsUndocked());
            }
            else
            {
                var source = _layoutService.GetProfile(sourceName);
                if (source == null) return;
                _layoutService.SaveProfile(
                    newName,
                    source.LayoutXml,
                    source.WindowLeft,
                    source.WindowTop,
                    source.WindowWidth,
                    source.WindowHeight,
                    source.WindowState,
                    source.RememberWindowPosition);
            }

            var dialogService = (System.Windows.Application.Current as App)?.Services?.GetService<IDialogService>();
            dialogService?.ShowToast($"Created layout '{newName.Trim()}'", "Layouts", ToastType.Success);
        }

        internal void UpdateActiveProfileName(string? name)
        {
            _activeLayoutProfileName = name;
            _isLayoutProfileDirty = false;
            SaveActiveWorkspaceSnapshot();
        }

        private bool AreAllPanelsUndocked()
        {
            if (DockManager?.Layout == null) return false;
            return !DockManager.Layout.Descendents()
                .OfType<LayoutAnchorable>()
                .Any(a => a.IsVisible && !a.IsFloating && !a.IsHidden);
        }

        private void SaveProfileWithWindowBounds(string profileName, string layoutXml)
        {
            if (_layoutService == null) return;

            var bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, ActualWidth, ActualHeight)
                : RestoreBounds;

            bool isNew = _layoutService.GetProfile(profileName) == null;
            bool? rememberPos = isNew ? AreAllPanelsUndocked() : null;

            _layoutService.SaveProfile(
                profileName,
                layoutXml,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                WindowState.ToString(),
                rememberPos);
        }

        private void ApplyWindowBoundsFromProfile(WorkspaceLayoutProfile profile, bool restorePosition)
        {
            if (profile.WindowWidth is > 0 && profile.WindowHeight is > 0)
            {
                double targetWidth = Math.Max(MinWidth, profile.WindowWidth.Value);
                double targetHeight = Math.Max(MinHeight, profile.WindowHeight.Value);

                if (restorePosition && profile.WindowLeft.HasValue && profile.WindowTop.HasValue)
                {
                    double minLeft = SystemParameters.VirtualScreenLeft;
                    double minTop = SystemParameters.VirtualScreenTop;
                    double maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Math.Min(targetWidth, 200);
                    double maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Math.Min(targetHeight, 100);

                    Left = Math.Clamp(profile.WindowLeft.Value, minLeft, Math.Max(minLeft, maxLeft));
                    Top = Math.Clamp(profile.WindowTop.Value, minTop, Math.Max(minTop, maxTop));
                }

                Width = targetWidth;
                Height = targetHeight;
            }

            if (!string.IsNullOrWhiteSpace(profile.WindowState) &&
                Enum.TryParse<WindowState>(profile.WindowState, out var parsedState) &&
                parsedState != WindowState.Minimized)
            {
                WindowState = parsedState;
            }
        }

        private static readonly Dictionary<string, (double MinWidth, double MinHeight, double? MaxHeight)> PanelConstraints = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SearchCriteria"] = (681, 170, null),
            ["SqlEditor"] = (365, 142, null),
            ["Results"] = (563, 218, null),
            ["RowInspector"] = (290, 174, null),
            ["SavedQueries"] = (290, 390, null),
            ["SelectionStats"] = (290, 397, null),
            ["DateConverter"] = (245, 101, null)
        };

        private const double FloatingChromeAllowance = 28;
        private const double DockedWidthAllowance = 16;
        private const double DockedHeightAllowance = 0;

        private bool _isApplyingVisualConstraints;

        private void ApplyPanelMinimumSizes()
        {
            if (DockManager?.Layout == null) return;

            foreach (var anchorable in DockManager.Layout.Descendents().OfType<LayoutAnchorable>())
            {
                if (anchorable.ContentId != null && PanelConstraints.TryGetValue(anchorable.ContentId, out var c))
                {
                    if (anchorable.Content is FrameworkElement fe)
                    {
                        if (c.MaxHeight.HasValue)
                        {
                            if (!NearlyEqual(fe.MaxHeight, c.MaxHeight.Value)) fe.MaxHeight = c.MaxHeight.Value;
                            if (fe.VerticalAlignment != VerticalAlignment.Top) fe.VerticalAlignment = VerticalAlignment.Top;
                        }
                        else
                        {
                            if (!NearlyEqual(fe.MaxHeight, double.PositiveInfinity)) fe.MaxHeight = double.PositiveInfinity;
                            if (fe.VerticalAlignment != VerticalAlignment.Stretch) fe.VerticalAlignment = VerticalAlignment.Stretch;
                        }
                    }
                }
            }

            foreach (var pane in DockManager.Layout.Descendents().OfType<LayoutAnchorablePane>())
            {
                var targets = pane.Children.Where(a => a.IsVisible).ToList();
                if (targets.Count == 0) targets = pane.Children.ToList();

                double maxMinW = 25;
                double maxMinH = 25;
                foreach (var child in targets)
                {
                    if (child.ContentId != null && PanelConstraints.TryGetValue(child.ContentId, out var c))
                    {
                        if (c.MinWidth > maxMinW) maxMinW = c.MinWidth;
                        if (c.MinHeight > maxMinH) maxMinH = c.MinHeight;
                    }
                }

                bool isFloating = pane.FindParent<LayoutAnchorableFloatingWindow>() != null;
                double allowW = isFloating ? FloatingChromeAllowance : DockedWidthAllowance;
                double allowH = isFloating ? FloatingChromeAllowance : DockedHeightAllowance;

                double targetMinW = Math.Max(25, maxMinW - allowW);
                double targetMinH = Math.Max(25, maxMinH - allowH);
                if (!NearlyEqual(pane.DockMinWidth, targetMinW)) pane.DockMinWidth = targetMinW;
                if (!NearlyEqual(pane.DockMinHeight, targetMinH)) pane.DockMinHeight = targetMinH;
            }

            bool hasDockedPanels = DockManager.Layout.Descendents()
                .OfType<LayoutAnchorable>()
                .Any(a => a.IsVisible && !a.IsFloating && !a.IsHidden);

            if (hasDockedPanels && DockManager.Layout.RootPanel != null)
            {
                var (dockedW, dockedH) = SyncAndComputeDockedLayoutMinSize(DockManager.Layout.RootPanel);
                double chromeW = (ActualWidth > 0 && DockManager.ActualWidth > 0)
                    ? Math.Max(20, ActualWidth - DockManager.ActualWidth) + 4
                    : 28;
                double chromeH = (ActualHeight > 0 && DockManager.ActualHeight > 0)
                    ? Math.Max(88, ActualHeight - DockManager.ActualHeight) + 4
                    : 96;

                double targetMinW = Math.Max(338, dockedW + chromeW);
                double targetMinH = Math.Max(95, dockedH + chromeH);
                if (!NearlyEqual(MinWidth, targetMinW)) MinWidth = targetMinW;
                if (!NearlyEqual(MinHeight, targetMinH)) MinHeight = targetMinH;
            }
            else
            {
                if (!NearlyEqual(MinWidth, 338)) MinWidth = 338;
                if (!NearlyEqual(MinHeight, 95)) MinHeight = 95;
            }
        }

        private static (double MinW, double MinH) SyncAndComputeDockedLayoutMinSize(ILayoutElement element)
        {
            if (element is LayoutAnchorablePane pane)
            {
                return pane.IsVisible ? (pane.DockMinWidth, pane.DockMinHeight) : (0, 0);
            }

            if (element is LayoutAnchorablePaneGroup paneGroup)
            {
                if (!paneGroup.IsVisible) return (0, 0);
                var (minW, minH) = SyncAndComputeGroupMinSize(paneGroup.Children, paneGroup.Orientation);
                if (minW > 0 && !NearlyEqual(paneGroup.DockMinWidth, minW)) paneGroup.DockMinWidth = minW;
                if (minH > 0 && !NearlyEqual(paneGroup.DockMinHeight, minH)) paneGroup.DockMinHeight = minH;
                return (minW, minH);
            }

            if (element is LayoutPanel panel)
            {
                if (!panel.IsVisible) return (0, 0);
                var (minW, minH) = SyncAndComputeGroupMinSize(panel.Children, panel.Orientation);
                if (minW > 0 && !NearlyEqual(panel.DockMinWidth, minW)) panel.DockMinWidth = minW;
                if (minH > 0 && !NearlyEqual(panel.DockMinHeight, minH)) panel.DockMinHeight = minH;
                return (minW, minH);
            }

            return (0, 0);
        }

        private static (double MinW, double MinH) SyncAndComputeGroupMinSize(IEnumerable<ILayoutElement> children, System.Windows.Controls.Orientation orientation)
        {
            double totalW = 0;
            double totalH = 0;
            int visibleCount = 0;

            foreach (var child in children)
            {
                var (w, h) = SyncAndComputeDockedLayoutMinSize(child);
                if (w <= 0 && h <= 0) continue;

                visibleCount++;
                if (orientation == System.Windows.Controls.Orientation.Horizontal)
                {
                    totalW += w;
                    if (h > totalH) totalH = h;
                }
                else
                {
                    if (w > totalW) totalW = w;
                    totalH += h;
                }
            }

            if (visibleCount > 1)
            {
                double splitterSpace = (visibleCount - 1) * 5;
                if (orientation == System.Windows.Controls.Orientation.Horizontal) totalW += splitterSpace;
                else totalH += splitterSpace;
            }

            return (totalW, totalH);
        }

        private void ApplyVisualConstraints()
        {
            if (_isApplyingVisualConstraints || _isApplyingLayout) return;
            _isApplyingVisualConstraints = true;
            try
            {
                ApplyPanelMinimumSizes();
                ApplyFloatingWindowConstraints();
                ApplyDockedGridConstraints();
            }
            finally
            {
                _isApplyingVisualConstraints = false;
            }
        }

        private void ApplyDockedGridConstraints()
        {
            if (DockManager == null) return;

            foreach (var gridControl in FindVisualChildren<Grid>(DockManager))
            {
                var group = gridControl switch
                {
                    AvalonDock.Controls.LayoutPanelControl lp => lp.Model as ILayoutOrientableGroup,
                    AvalonDock.Controls.LayoutAnchorablePaneGroupControl apg => apg.Model as ILayoutOrientableGroup,
                    AvalonDock.Controls.LayoutDocumentPaneGroupControl dpg => dpg.Model as ILayoutOrientableGroup,
                    _ => null
                };
                if (group == null) continue;

                var visibleChildren = group.Children
                    .Where(c => c is LayoutAnchorablePane p ? p.IsVisible :
                                c is LayoutAnchorablePaneGroup pg ? pg.IsVisible :
                                c is LayoutPanel lp && lp.IsVisible)
                    .ToList();

                if (group.Orientation == System.Windows.Controls.Orientation.Vertical)
                {
                    for (int i = 0; i < visibleChildren.Count; i++)
                    {
                        int rowIdx = i * 2;
                        if (rowIdx >= gridControl.RowDefinitions.Count) break;

                        double targetMinH = visibleChildren[i] switch
                        {
                            LayoutAnchorablePane p => p.DockMinHeight,
                            LayoutAnchorablePaneGroup pg => pg.DockMinHeight,
                            LayoutPanel lp => lp.DockMinHeight,
                            _ => 25
                        };

                        var rowAnchorables = (visibleChildren[i] as ILayoutElement)?
                            .Descendents()
                            .OfType<LayoutAnchorable>()
                            .Where(a => a.IsVisible)
                            .ToList();

                        double? lockedMaxH = null;
                        if (rowAnchorables != null && rowAnchorables.Count > 0 &&
                            rowAnchorables.All(a => a.ContentId != null &&
                                PanelConstraints.TryGetValue(a.ContentId, out var c) && c.MaxHeight.HasValue))
                        {
                            lockedMaxH = Math.Max(25, rowAnchorables.Max(a => PanelConstraints[a.ContentId!].MaxHeight!.Value) - DockedHeightAllowance);
                        }

                        double targetMaxH = lockedMaxH ?? double.PositiveInfinity;
                        if (!NearlyEqual(gridControl.RowDefinitions[rowIdx].MinHeight, targetMinH))
                        {
                            gridControl.RowDefinitions[rowIdx].MinHeight = targetMinH;
                        }
                        if (!NearlyEqual(gridControl.RowDefinitions[rowIdx].MaxHeight, targetMaxH))
                        {
                            gridControl.RowDefinitions[rowIdx].MaxHeight = targetMaxH;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < visibleChildren.Count; i++)
                    {
                        int colIdx = i * 2;
                        if (colIdx >= gridControl.ColumnDefinitions.Count) break;

                        double targetMinW = visibleChildren[i] switch
                        {
                            LayoutAnchorablePane p => p.DockMinWidth,
                            LayoutAnchorablePaneGroup pg => pg.DockMinWidth,
                            LayoutPanel lp => lp.DockMinWidth,
                            _ => 25
                        };

                        if (!NearlyEqual(gridControl.ColumnDefinitions[colIdx].MinWidth, targetMinW))
                        {
                            gridControl.ColumnDefinitions[colIdx].MinWidth = targetMinW;
                        }
                    }
                }
            }
        }

        private static bool NearlyEqual(double a, double b)
        {
            if (double.IsInfinity(a) && double.IsInfinity(b)) return true;
            if (double.IsInfinity(a) || double.IsInfinity(b)) return false;
            return Math.Abs(a - b) < 0.5;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var desc in FindVisualChildren<T>(child))
                    yield return desc;
            }
        }

        private void ApplyFloatingWindowConstraints()
        {
            if (DockManager == null) return;

            foreach (var fw in DockManager.FloatingWindows.OfType<AvalonDock.Controls.LayoutAnchorableFloatingWindowControl>())
            {
                if (fw.Model is not LayoutAnchorableFloatingWindow model) continue;

                var visibleAnchorables = model.Descendents()
                    .OfType<LayoutAnchorable>()
                    .Where(a => a.IsVisible)
                    .ToList();

                double maxMinW = 100;
                double maxMinH = 100;
                double? lockedMaxH = null;

                foreach (var a in visibleAnchorables)
                {
                    if (a.ContentId != null && PanelConstraints.TryGetValue(a.ContentId, out var c))
                    {
                        if (c.MinWidth > maxMinW) maxMinW = c.MinWidth;
                        if (c.MinHeight > maxMinH) maxMinH = c.MinHeight;
                    }
                }

                if (visibleAnchorables.Count > 0 &&
                    visibleAnchorables.All(a => a.ContentId != null &&
                        PanelConstraints.TryGetValue(a.ContentId, out var c) && c.MaxHeight.HasValue))
                {
                    lockedMaxH = visibleAnchorables.Max(a => PanelConstraints[a.ContentId!].MaxHeight!.Value);
                }

                double targetMaxH = lockedMaxH ?? double.PositiveInfinity;
                if (!NearlyEqual(fw.MinWidth, maxMinW)) fw.MinWidth = maxMinW;
                if (!NearlyEqual(fw.MinHeight, maxMinH)) fw.MinHeight = maxMinH;
                if (!NearlyEqual(fw.MaxHeight, targetMaxH)) fw.MaxHeight = targetMaxH;
            }
        }

        internal MessageBoxResult ShowThemedConfirmDialog(Window owner, string title, string message, bool includeCancel = false)
            => ShowThemedConfirmDialogWithOption(owner, title, message, null, includeCancel).Result;

        internal (MessageBoxResult Result, bool OptionChecked) ShowThemedConfirmDialogWithOption(
            Window owner,
            string title,
            string message,
            string? optionCheckboxText,
            bool includeCancel = false)
        {
            var confirmDlg = new Window
            {
                Title = title,
                MinWidth = 340,
                MaxWidth = 460,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                Background = (System.Windows.Media.Brush)FindResource("AppBackgroundBrush"),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
            };
            DialogPlacementHelper.ApplyOwnerAndPlacement(confirmDlg, owner);

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var msgBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(optionCheckboxText) ? 16 : 10)
            };

            System.Windows.Controls.CheckBox? optCheck = null;
            if (!string.IsNullOrEmpty(optionCheckboxText))
            {
                optCheck = new System.Windows.Controls.CheckBox
                {
                    Content = optionCheckboxText,
                    Margin = new Thickness(0, 0, 0, 14)
                };
                Grid.SetRow(optCheck, 1);
            }

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(buttons, 2);

            MessageBoxResult choice = includeCancel ? MessageBoxResult.Cancel : MessageBoxResult.No;

            var yesBtn = new Button { Content = "Yes", Width = 75, Height = 26, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            yesBtn.Click += (_, _) =>
            {
                choice = MessageBoxResult.Yes;
                confirmDlg.DialogResult = true;
                confirmDlg.Close();
            };
            buttons.Children.Add(yesBtn);

            var noBtn = new Button { Content = "No", Width = 75, Height = 26, IsCancel = !includeCancel };
            if (includeCancel) noBtn.Margin = new Thickness(0, 0, 8, 0);
            noBtn.Click += (_, _) =>
            {
                choice = MessageBoxResult.No;
                confirmDlg.DialogResult = false;
                confirmDlg.Close();
            };
            buttons.Children.Add(noBtn);

            if (includeCancel)
            {
                var cancelBtn = new Button { Content = "Cancel", Width = 75, Height = 26, IsCancel = true };
                cancelBtn.Click += (_, _) =>
                {
                    choice = MessageBoxResult.Cancel;
                    confirmDlg.DialogResult = false;
                    confirmDlg.Close();
                };
                buttons.Children.Add(cancelBtn);
            }

            root.Children.Add(msgBlock);
            if (optCheck != null) root.Children.Add(optCheck);
            root.Children.Add(buttons);
            confirmDlg.Content = root;

            confirmDlg.ShowDialog();
            return (choice, optCheck?.IsChecked == true);
        }

        internal void ShowThemedNoticeDialog(Window owner, string title, string message)
        {
            var noticeDlg = new Window
            {
                Title = title,
                MinWidth = 320,
                MaxWidth = 440,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                Background = (System.Windows.Media.Brush)FindResource("AppBackgroundBrush"),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
            };
            DialogPlacementHelper.ApplyOwnerAndPlacement(noticeDlg, owner);

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var msgBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var okBtn = new Button { Content = "OK", Width = 75, Height = 26, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(okBtn, 1);
            okBtn.Click += (_, _) => noticeDlg.Close();

            root.Children.Add(msgBlock);
            root.Children.Add(okBtn);
            noticeDlg.Content = root;
            noticeDlg.ShowDialog();
        }
    }
}
