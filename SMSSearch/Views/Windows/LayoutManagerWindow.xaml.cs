#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SMS_Search.Services;

namespace SMS_Search.Views.Windows
{
    public partial class LayoutManagerWindow : Window
    {
        private sealed class LayoutProfileItem
        {
            public required string Name { get; init; }
            public bool IsBuiltIn { get; init; }
            public bool IsActive { get; init; }
            public bool IsDirty { get; init; }
            public bool IsStartup { get; init; }
            public bool RememberWindowPosition { get; init; }
            public bool SuppressCloseSavePrompt { get; init; }
            public SMS_Search.Data.LayoutSearchTabPreference SearchTabPreference { get; init; }
            public SMS_Search.Data.LayoutSearchModePreference SearchModePreference { get; init; }
            public required string Subtitle { get; init; }
        }

        private readonly MainWindow _mainWindow;
        private readonly IWorkspaceLayoutService _layoutService;
        private bool _isRefreshing;

        public LayoutManagerWindow(MainWindow mainWindow, IWorkspaceLayoutService layoutService)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _layoutService = layoutService;
            _isRefreshing = true;
            CmbSearchTab.ItemsSource = Enum.GetValues<SMS_Search.Data.LayoutSearchTabPreference>();
            CmbSearchMode.ItemsSource = Enum.GetValues<SMS_Search.Data.LayoutSearchModePreference>();
            _isRefreshing = false;

            _layoutService.ProfilesChanged += LayoutService_ProfilesChanged;

            Loaded += (_, _) => RefreshState();
            Closed += (_, _) => _layoutService.ProfilesChanged -= LayoutService_ProfilesChanged;
        }

        private void LayoutService_ProfilesChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() => RefreshState());
        }

        public void RefreshState(string? selectName = null)
        {
            _isRefreshing = true;
            try
            {
                string? activeName = _mainWindow.ActiveLayoutProfileName;
                bool isDirty = _mainWindow.IsLayoutProfileDirty;
                string? startupName = _layoutService.GetStartupLayoutName();

                if (string.IsNullOrWhiteSpace(startupName))
                {
                    RadioLastSession.IsChecked = true;
                    TxtStartupProfileName.Text = "(none — using last session)";
                }
                else
                {
                    RadioStartupProfile.IsChecked = true;
                    TxtStartupProfileName.Text = startupName;
                }

                string? previousSelection = selectName ?? (LayoutsList.SelectedItem as LayoutProfileItem)?.Name ?? activeName ?? WorkspaceLayoutService.BuiltInDefaultName;

                var defaultSearchPrefs = _layoutService.GetDefaultSearchPreferences();
                var items = new List<LayoutProfileItem>
                {
                    new LayoutProfileItem
                    {
                        Name = WorkspaceLayoutService.BuiltInDefaultName,
                        IsBuiltIn = true,
                        IsActive = WorkspaceLayoutService.IsBuiltInDefault(activeName),
                        IsDirty = WorkspaceLayoutService.IsBuiltInDefault(activeName) && isDirty,
                        IsStartup = WorkspaceLayoutService.IsBuiltInDefault(startupName),
                        RememberWindowPosition = _layoutService.GetDefaultRememberWindowPosition(),
                        SearchTabPreference = defaultSearchPrefs.Tab,
                        SearchModePreference = defaultSearchPrefs.Mode,
                        Subtitle = "Built-in read-only layout (duplicate to customize)"
                    }
                };

                foreach (var p in _layoutService.GetProfiles())
                {
                    bool isActive = string.Equals(p.Name, activeName, StringComparison.OrdinalIgnoreCase);
                    bool isStartup = string.Equals(p.Name, startupName, StringComparison.OrdinalIgnoreCase);
                    string sizeText = (p.WindowWidth is > 0 && p.WindowHeight is > 0)
                        ? $"{Math.Round(p.WindowWidth.Value)} × {Math.Round(p.WindowHeight.Value)}"
                        : "Panels only";
                    string savedLocal = p.SavedAtUtc.ToLocalTime().ToString("g");

                    items.Add(new LayoutProfileItem
                    {
                        Name = p.Name,
                        IsBuiltIn = false,
                        IsActive = isActive,
                        IsDirty = isActive && isDirty,
                        IsStartup = isStartup,
                        RememberWindowPosition = p.RememberWindowPosition,
                        SuppressCloseSavePrompt = p.SuppressCloseSavePrompt,
                        SearchTabPreference = p.SearchTabPreference,
                        SearchModePreference = p.SearchModePreference,
                        Subtitle = $"Window: {sizeText}  •  Saved: {savedLocal}"
                    });
                }

                LayoutsList.ItemsSource = items;
                LayoutsList.SelectedItem = items.FirstOrDefault(i => string.Equals(i.Name, previousSelection, StringComparison.OrdinalIgnoreCase))
                    ?? items[0];

                UpdateButtonStates();

                if (string.IsNullOrWhiteSpace(activeName))
                {
                    TxtStatus.Text = "Current workspace: Custom (unsaved)";
                }
                else
                {
                    TxtStatus.Text = isDirty
                        ? $"Active layout: {activeName} (Modified)"
                        : $"Active layout: {activeName}";
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void UpdateButtonStates()
        {
            var selected = LayoutsList.SelectedItem as LayoutProfileItem;
            bool hasSelection = selected != null;
            bool isCustom = selected != null && !selected.IsBuiltIn;

            BtnApply.IsEnabled = hasSelection;
            BtnDuplicate.IsEnabled = hasSelection;
            BtnSetStartup.IsEnabled = hasSelection;
            BtnSave.IsEnabled = isCustom;
            BtnRename.IsEnabled = isCustom;
            BtnDelete.IsEnabled = isCustom;

            bool prevRefreshing = _isRefreshing;
            _isRefreshing = true;
            ChkRememberPosition.IsEnabled = hasSelection;
            ChkRememberPosition.IsChecked = hasSelection && selected!.RememberWindowPosition;
            ChkPromptOnClose.Visibility = isCustom ? Visibility.Visible : Visibility.Hidden;
            ChkPromptOnClose.IsChecked = !hasSelection || !selected!.SuppressCloseSavePrompt;
            CmbSearchTab.IsEnabled = hasSelection;
            CmbSearchMode.IsEnabled = hasSelection;
            CmbSearchTab.SelectedItem = hasSelection ? selected!.SearchTabPreference : SMS_Search.Data.LayoutSearchTabPreference.Function;
            CmbSearchMode.SelectedItem = hasSelection ? selected!.SearchModePreference : SMS_Search.Data.LayoutSearchModePreference.Simple;
            _isRefreshing = prevRefreshing;
        }

        private void SearchPref_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing) return;
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected) return;
            var tab = CmbSearchTab.SelectedItem is SMS_Search.Data.LayoutSearchTabPreference t ? t : SMS_Search.Data.LayoutSearchTabPreference.Function;
            var mode = CmbSearchMode.SelectedItem is SMS_Search.Data.LayoutSearchModePreference m ? m : SMS_Search.Data.LayoutSearchModePreference.Simple;
            _layoutService.SetProfileSearchPreferences(selected.Name, tab, mode);
        }

        private void ChkRememberPosition_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected) return;
            _layoutService.SetProfileRememberWindowPosition(selected.Name, ChkRememberPosition.IsChecked == true);
        }

        private void ChkPromptOnClose_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected || selected.IsBuiltIn) return;
            _layoutService.SetProfileSuppressCloseSavePrompt(selected.Name, ChkPromptOnClose.IsChecked != true);
        }

        private void LayoutsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing) return;
            UpdateButtonStates();
        }

        private void LayoutsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ApplySelected();
        }

        private void StartupMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;

            if (RadioLastSession.IsChecked == true)
            {
                _layoutService.SetStartupLayoutName(null);
            }
            else if (RadioStartupProfile.IsChecked == true)
            {
                string? target = _layoutService.GetStartupLayoutName();
                if (string.IsNullOrWhiteSpace(target))
                {
                    target = (LayoutsList.SelectedItem as LayoutProfileItem)?.Name ?? WorkspaceLayoutService.BuiltInDefaultName;
                }
                _layoutService.SetStartupLayoutName(target);
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e) => ApplySelected();

        private void ApplySelected()
        {
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected) return;
            _mainWindow.ApplyLayoutFromManager(selected.Name);
            RefreshState(selected.Name);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected || selected.IsBuiltIn) return;

            if (_mainWindow.ShowThemedConfirmDialog(
                    this,
                    "Confirm Save",
                    $"Overwrite layout '{selected.Name}' with the current panel and window arrangement?") != MessageBoxResult.Yes)
            {
                return;
            }

            _mainWindow.SaveCurrentToProfileFromManager(selected.Name);
            RefreshState(selected.Name);
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            string? activeName = _mainWindow.ActiveLayoutProfileName;
            string initialName = (!string.IsNullOrWhiteSpace(activeName) && !WorkspaceLayoutService.IsBuiltInDefault(activeName))
                ? activeName
                : string.Empty;

            string? newName = PromptForProfileName("Save Current Layout As", "Layout name:", initialName);
            if (string.IsNullOrWhiteSpace(newName)) return;

            if (WorkspaceLayoutService.IsBuiltInDefault(newName))
            {
                _mainWindow.ShowThemedNoticeDialog(this, "Reserved Name", $"'{WorkspaceLayoutService.BuiltInDefaultName}' is a built-in read-only layout.");
                return;
            }

            if (_layoutService.GetProfile(newName) != null)
            {
                if (_mainWindow.ShowThemedConfirmDialog(
                        this,
                        "Confirm Overwrite",
                        $"Layout '{newName}' already exists.\n\nDo you want to overwrite it?") != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _mainWindow.SaveCurrentToProfileFromManager(newName);
            RefreshState(newName);
        }

        private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected) return;

            string defaultCopyName = $"{selected.Name} - Copy";
            string? newName = PromptForProfileName("Duplicate Layout", "New layout name:", defaultCopyName);
            if (string.IsNullOrWhiteSpace(newName)) return;

            if (WorkspaceLayoutService.IsBuiltInDefault(newName))
            {
                _mainWindow.ShowThemedNoticeDialog(this, "Reserved Name", $"'{WorkspaceLayoutService.BuiltInDefaultName}' is a built-in read-only layout.");
                return;
            }

            if (_layoutService.GetProfile(newName) != null)
            {
                if (_mainWindow.ShowThemedConfirmDialog(
                        this,
                        "Confirm Overwrite",
                        $"Layout '{newName}' already exists.\n\nDo you want to overwrite it?") != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _mainWindow.DuplicateLayoutFromManager(selected.Name, newName);
            RefreshState(newName);
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected || selected.IsBuiltIn) return;

            string? newName = PromptForProfileName("Rename Layout", "New layout name:", selected.Name);
            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, selected.Name, StringComparison.Ordinal)) return;

            if (WorkspaceLayoutService.IsBuiltInDefault(newName))
            {
                _mainWindow.ShowThemedNoticeDialog(this, "Reserved Name", $"'{WorkspaceLayoutService.BuiltInDefaultName}' is a built-in read-only layout.");
                return;
            }

            if (!string.Equals(newName, selected.Name, StringComparison.OrdinalIgnoreCase) &&
                _layoutService.GetProfile(newName) != null)
            {
                _mainWindow.ShowThemedNoticeDialog(this, "Duplicate Name", $"A layout named '{newName}' already exists.");
                return;
            }

            _layoutService.RenameProfile(selected.Name, newName);
            if (string.Equals(_mainWindow.ActiveLayoutProfileName, selected.Name, StringComparison.OrdinalIgnoreCase))
            {
                _mainWindow.UpdateActiveProfileName(newName);
            }

            RefreshState(newName);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected || selected.IsBuiltIn) return;

            if (_mainWindow.ShowThemedConfirmDialog(
                    this,
                    "Confirm Delete",
                    $"Delete saved layout '{selected.Name}'?") != MessageBoxResult.Yes)
            {
                return;
            }

            _layoutService.DeleteProfile(selected.Name);
            if (string.Equals(_mainWindow.ActiveLayoutProfileName, selected.Name, StringComparison.OrdinalIgnoreCase))
            {
                _mainWindow.UpdateActiveProfileName(null);
            }

            RefreshState();
        }

        private void BtnSetStartup_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutsList.SelectedItem is not LayoutProfileItem selected) return;
            _layoutService.SetStartupLayoutName(selected.Name);
            RefreshState(selected.Name);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private string? PromptForProfileName(string title, string prompt, string initialText)
        {
            var dlg = new Window
            {
                Title = title,
                Width = 360,
                MinWidth = 360,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                Background = (System.Windows.Media.Brush)FindResource("AppBackgroundBrush"),
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush")
            };

            var root = new Grid { Margin = new Thickness(14) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 6) };
            var box = new System.Windows.Controls.TextBox
            {
                Text = initialText,
                Height = 26,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            Grid.SetRow(box, 1);

            var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            Grid.SetRow(buttons, 2);

            string? result = null;
            var okBtn = new System.Windows.Controls.Button { Content = "OK", Width = 75, Height = 26, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            okBtn.Click += (_, _) =>
            {
                string trimmed = box.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(trimmed)) return;
                result = trimmed;
                dlg.DialogResult = true;
                dlg.Close();
            };
            var cancelBtn = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, Height = 26, IsCancel = true };
            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);

            root.Children.Add(label);
            root.Children.Add(box);
            root.Children.Add(buttons);
            dlg.Content = root;
            box.Focus();
            box.SelectAll();

            return dlg.ShowDialog() == true ? result : null;
        }
    }
}
