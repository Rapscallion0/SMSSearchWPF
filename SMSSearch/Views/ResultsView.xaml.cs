using System.Windows.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;
using SMS_Search.ViewModels;
using SMS_Search.Data;
using SMS_Search.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace SMS_Search.Views
{
    public partial class ResultsView : System.Windows.Controls.UserControl
    {
        private DispatcherTimer _debounceTimer;

        public IRelayCommand<DataGridColumn> CopyColumnNameCommand { get; }
        public IRelayCommand<DataGridColumn> HideColumnCommand { get; }
        public IRelayCommand<HiddenColumnItem> ShowColumnCommand { get; }
        public IRelayCommand ShowAllColumnsCommand { get; }
        public IRelayCommand<DataGridColumn> BestFitCommand { get; }

        public static readonly DependencyProperty HasHiddenColumnsProperty = DependencyProperty.Register(
            "HasHiddenColumns", typeof(bool), typeof(ResultsView), new PropertyMetadata(false));

        public bool HasHiddenColumns
        {
            get { return (bool)GetValue(HasHiddenColumnsProperty); }
            set { SetValue(HasHiddenColumnsProperty, value); }
        }

        public static readonly DependencyProperty HiddenColumnsProperty = DependencyProperty.Register(
            "HiddenColumns", typeof(System.Collections.ObjectModel.ObservableCollection<HiddenColumnItem>), typeof(ResultsView), new PropertyMetadata(null));

        public System.Collections.ObjectModel.ObservableCollection<HiddenColumnItem> HiddenColumns
        {
            get { return (System.Collections.ObjectModel.ObservableCollection<HiddenColumnItem>)GetValue(HiddenColumnsProperty); }
            set { SetValue(HiddenColumnsProperty, value); }
        }
        public IRelayCommand FilterBySelectionCommand { get; }
        public IRelayCommand CopyWithHeadersCommand { get; }
        public IRelayCommand AdvancedCopyCommand { get; }
        public IRelayCommand CopyRowCommand { get; }
        public IRelayCommand CopyInsertCommand { get; }

        private const int WM_MOUSEHWHEEL = 0x020E;
        private HwndSource? _hwndSource;
        private ScrollViewer? _cachedScrollViewer;

        public ResultsView()
        {
            InitializeComponent();
            HiddenColumns = new System.Collections.ObjectModel.ObservableCollection<HiddenColumnItem>();
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(500);
            _debounceTimer.Tick += DebounceTimer_Tick;

            CopyColumnNameCommand = new RelayCommand<DataGridColumn>(CopyColumnName);
            HideColumnCommand = new RelayCommand<DataGridColumn>(HideColumn);
            ShowColumnCommand = new RelayCommand<HiddenColumnItem>(ShowColumn);
            ShowAllColumnsCommand = new RelayCommand(ShowAllColumns);
            BestFitCommand = new RelayCommand<DataGridColumn>(BestFit);
            FilterBySelectionCommand = new RelayCommand(FilterBySelection);
            CopyWithHeadersCommand = new RelayCommand(CopyWithHeaders);
            AdvancedCopyCommand = new RelayCommand(AdvancedCopy);
            CopyRowCommand = new RelayCommand(CopyRow);
            CopyInsertCommand = new RelayCommand(CopyInsert);

            this.DataContextChanged += ResultsView_DataContextChanged;
            resultsGrid.AutoGeneratingColumn += resultsGrid_AutoGeneratingColumn;
            this.Loaded += ResultsView_Loaded;
            this.Unloaded += ResultsView_Unloaded;
        }

        private void ResultsView_Loaded(object sender, RoutedEventArgs e)
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
        }

        private void ResultsView_Unloaded(object sender, RoutedEventArgs e)
        {
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
        }

        private ScrollViewer? GetCachedScrollViewer()
        {
            if (_cachedScrollViewer == null)
            {
                _cachedScrollViewer = GetVisualChild<ScrollViewer>(resultsGrid);
            }
            return _cachedScrollViewer;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                // Is the mouse over this control?
                var pt = Mouse.PrimaryDevice.GetPosition(resultsGrid);
                if (pt.X >= 0 && pt.X <= resultsGrid.ActualWidth &&
                    pt.Y >= 0 && pt.Y <= resultsGrid.ActualHeight)
                {
                    // HIWORD of wParam contains the wheel delta
                    short wheelDelta = unchecked((short)((long)wParam >> 16));

                    var scrollViewer = GetCachedScrollViewer();
                    if (scrollViewer != null && DataContext is ResultsViewModel vm)
                    {
                        int multiplier = vm.HorizontalScrollSpeed;

                        // WM_MOUSEHWHEEL positive value indicates the wheel was rotated to the right
                        // Standard wheelDelta is 120. Convert to lines and multiply by pixel speed.
                        double lines = wheelDelta / 120.0;
                        double pixelChange = lines * 3 * multiplier;

                        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + pixelChange);
                        handled = true;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private void ResultsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ResultsViewModel newVm)
            {
                newVm.ScrollToCellRequested += Vm_ScrollToCellRequested;
                newVm.HeadersUpdated += Vm_HeadersUpdated;
            }
            if (e.OldValue is ResultsViewModel oldVm)
            {
                oldVm.ScrollToCellRequested -= Vm_ScrollToCellRequested;
                oldVm.HeadersUpdated -= Vm_HeadersUpdated;
            }
        }

        private static T? GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T? child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }

        private void Vm_HeadersUpdated(object? sender, string? toggledColumnName)
        {
            if (DataContext is ResultsViewModel vm)
            {
                ScrollViewer? scrollViewer = GetCachedScrollViewer();
                double targetAbsoluteX = 0;
                DataGridColumn? targetCol = null;

                if (!string.IsNullOrEmpty(toggledColumnName) && scrollViewer != null)
                {
                    targetCol = resultsGrid.Columns.FirstOrDefault(c => c.SortMemberPath == toggledColumnName);
                    if (targetCol != null && targetCol.Visibility == Visibility.Visible)
                    {
                        var precedingCols = resultsGrid.Columns
                            .Where(c => c.Visibility == Visibility.Visible && c.DisplayIndex < targetCol.DisplayIndex);
                        targetAbsoluteX = precedingCols.Sum(c => c.ActualWidth);
                    }
                }

                foreach (var col in resultsGrid.Columns)
                {
                    string? key = col.SortMemberPath;
                    if (!string.IsNullOrEmpty(key) && vm.ColumnHeaders.TryGetValue(key, out string? header))
                    {
                        col.Header = header;
                    }
                }

                if (targetCol != null && scrollViewer != null)
                {
                    resultsGrid.UpdateLayout();

                    var precedingCols = resultsGrid.Columns
                        .Where(c => c.Visibility == Visibility.Visible && c.DisplayIndex < targetCol.DisplayIndex);
                    double newAbsoluteX = precedingCols.Sum(c => c.ActualWidth);

                    double viewPortRelativeX = targetAbsoluteX - scrollViewer.HorizontalOffset;
                    double newOffset = newAbsoluteX - viewPortRelativeX;

                    scrollViewer.ScrollToHorizontalOffset(newOffset);
                }
            }
        }

        private void resultsGrid_CurrentCellChanged(object? sender, EventArgs e)
        {
            if (DataContext is ResultsViewModel vm)
            {
                var cellInfo = resultsGrid.CurrentCell;
                if (cellInfo.IsValid && cellInfo.Item is SMS_Search.Data.VirtualRow vRow)
                {
                    string? colName = cellInfo.Column?.SortMemberPath;
                    vm.SetCurrentCell(vRow.RowIndex, colName);
                }
            }
        }

        private void resultsGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
             if (DataContext is ResultsViewModel vm)
             {
                 string key = e.PropertyName;
                 if (vm.ColumnHeaders.TryGetValue(key, out string? header))
                 {
                     e.Column.Header = header;
                 }
             }
        }

        private void Vm_ScrollToCellRequested(object? sender, (int RowIndex, string ColumnName) args)
        {
            int rowIndex = args.RowIndex;
            string colName = args.ColumnName;

            if (resultsGrid.Items.Count > rowIndex && rowIndex >= 0)
            {
                var item = resultsGrid.Items[rowIndex];
                resultsGrid.ScrollIntoView(item);

                // Find column
                var col = resultsGrid.Columns.FirstOrDefault(c => c.SortMemberPath == colName);

                // Dispatch to allow the container to be generated if virtualized
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    // Re-fetch the item to get the exact instance the grid is currently using
                    if (resultsGrid.Items.Count > rowIndex)
                    {
                        var currentItem = resultsGrid.Items[rowIndex];

                        if (col != null)
                        {
                            resultsGrid.SelectedCells.Clear();
                            resultsGrid.SelectedItems.Clear();

                            var cellInfo = new DataGridCellInfo(currentItem, col);
                            resultsGrid.CurrentCell = cellInfo;
                            resultsGrid.SelectedCells.Add(cellInfo);
                            resultsGrid.ScrollIntoView(currentItem, col);
                        }
                        else
                        {
                            // Fallback to row selection if column not found
                            resultsGrid.SelectedItems.Clear();
                            resultsGrid.SelectedItems.Add(currentItem);
                        }
                    }

                    // Try to focus the grid so keyboard navigation works
                    resultsGrid.Focus();
                }));
            }
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (DataContext is ResultsViewModel vm)
            {
                if (vm.ApplyFilterCommand.CanExecute(txtFilter.Text))
                {
                    vm.ApplyFilterCommand.Execute(txtFilter.Text);
                }
            }
        }

        private void resultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ResultsViewModel vm && resultsGrid.SelectedItem != null)
            {
                if (resultsGrid.SelectedItem is SMS_Search.Data.VirtualRow vRow)
                {
                    vm.SetCurrentRowIndex(vRow.RowIndex);
                }
            }
        }

        private void resultsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            if (DataContext is ResultsViewModel vm)
            {
                string? sortPath = e.Column.SortMemberPath;
                if (!string.IsNullOrEmpty(sortPath))
                {
                    var currentDir = e.Column.SortDirection;
                    foreach (var col in resultsGrid.Columns)
                    {
                        col.SortDirection = null;
                    }
                    e.Column.SortDirection = currentDir == System.ComponentModel.ListSortDirection.Ascending ? System.ComponentModel.ListSortDirection.Descending : System.ComponentModel.ListSortDirection.Ascending;
                    if (vm.SortCommand.CanExecute(sortPath))
                    {
                        vm.SortCommand.Execute(sortPath);
                    }
                }
            }
        }

        private void FilterBySelection()
        {
            if (resultsGrid.SelectedCells.Count > 0)
            {
                var cellInfo = resultsGrid.SelectedCells[0];
                var val = GetCellValue(cellInfo.Item, cellInfo.Column);
                if (val != null)
                {
                    txtFilter.Text = val.ToString() ?? "";
                }
            }
        }

        private void CopyWithHeaders()
        {
            CopySelectedCells(true, true);
        }

        private void AdvancedCopy()
        {
             if (resultsGrid.SelectedCells.Count == 0) return;

             var dlg = new ClipboardOptionsWindow();
             dlg.Owner = Window.GetWindow(this);
             if (dlg.ShowDialog() == true)
             {
                 CopySelectedCells(false, dlg.PreserveLayout);
             }
        }

        private void CopyRow()
        {
             if (DataContext is ResultsViewModel vm)
             {
                 vm.CopyRowCommand.Execute(resultsGrid.SelectedItems);
             }
        }

        private void CopyInsert()
        {
             if (DataContext is ResultsViewModel vm)
             {
                 vm.CopyInsertCommand.Execute(resultsGrid.SelectedItems);
             }
        }

        private void CopySelectedCells(bool includeHeaders, bool preserveLayout)
        {
            if (resultsGrid.SelectedCells.Count == 0) return;

            var cells = resultsGrid.SelectedCells;
            var rows = cells.Select(c => c.Item).Distinct().ToList();

            // We need to sort rows by their index to maintain visual order
            rows = rows.OrderBy(r => (r as VirtualRow)?.RowIndex ?? 0).ToList();

            // Sort columns by DisplayIndex
            var cols = cells.Select(c => c.Column).Distinct().OrderBy(c => c.DisplayIndex).ToList();

            var sb = new StringBuilder();

            if (includeHeaders)
            {
                sb.AppendLine(string.Join("\t", cols.Select(c => c.Header)));
            }

            foreach (var row in rows)
            {
                var values = new List<string>();
                foreach (var col in cols)
                {
                    // Check if this specific cell (row, col) is in the selection
                    bool isSelected = cells.Any(c => c.Item == row && c.Column == col);

                    if (isSelected)
                    {
                        var val = GetCellValue(row, col);
                        values.Add(val?.ToString() ?? "");
                    }
                    else if (preserveLayout)
                    {
                        // Not selected but we preserve layout (gap)
                        values.Add("");
                    }
                    // Else: not selected and not preserving layout -> skip value (content only)
                }

                // Only append row if we have values (skip empty rows if copy content only filtered everything out)
                if (values.Count > 0)
                {
                    sb.AppendLine(string.Join("\t", values));
                }
            }

            try
            {
                System.Windows.Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                var dialogService = (System.Windows.Application.Current as App)?.Services.GetService<IDialogService>();
                if (dialogService != null)
                {
                    dialogService.ShowError("Failed to copy: " + ex.Message, "Error");
                }
                else
                {
                    System.Windows.MessageBox.Show("Failed to copy: " + ex.Message);
                }
            }
        }

        private object? GetCellValue(object row, DataGridColumn col)
        {
            if (row == null) return null;
            string? propName = col.SortMemberPath;
            if (string.IsNullOrEmpty(propName)) return null;

            var props = TypeDescriptor.GetProperties(row);
            var prop = props[propName];
            return prop?.GetValue(row);
        }

        private string FormatSqlValue(object? value)
        {
            if (value == null || value == DBNull.Value) return "NULL";
            if (value is bool b) return b ? "1" : "0";
            if (IsNumeric(value)) return value.ToString() ?? "NULL";
            if (value is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";
            return $"'{value.ToString()?.Replace("'", "''") ?? ""}'";
        }

        private bool IsNumeric(object? value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }

        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                txtFilter.Focus();
                e.Handled = true;
            }
        }

        // Command Implementations
        private void CopyColumnName(DataGridColumn? col)
        {
            if (col != null)
            {
                try { System.Windows.Clipboard.SetText(col.Header.ToString() ?? ""); } catch { }
            }
        }

        private void HideColumn(DataGridColumn? col)
        {
            if (col != null)
            {
                col.Visibility = Visibility.Collapsed;
                UpdateHiddenColumnsList();
            }
        }

        private void ShowColumn(HiddenColumnItem? item)
        {
            if (item != null && item.Column != null)
            {
                item.Column.Visibility = Visibility.Visible;
                UpdateHiddenColumnsList();
            }
        }

        private void ShowAllColumns()
        {
            foreach (var col in resultsGrid.Columns)
            {
                col.Visibility = Visibility.Visible;
            }
            UpdateHiddenColumnsList();
        }

        private void UpdateHiddenColumnsList()
        {
            HiddenColumns.Clear();

            var hiddenCols = resultsGrid.Columns.Where(c => c.Visibility == Visibility.Collapsed).ToList();
            if (hiddenCols.Any())
            {
                HiddenColumns.Add(new HiddenColumnItem { Header = "Show All Hidden Columns", IsShowAll = true });
                foreach (var col in hiddenCols)
                {
                    HiddenColumns.Add(new HiddenColumnItem { Header = col.Header?.ToString() ?? "Unknown", Column = col });
                }
                HasHiddenColumns = true;
            }
            else
            {
                HasHiddenColumns = false;
            }

            if (DataContext is ResultsViewModel vm)
            {
                vm.HiddenColumns = new HashSet<string>(hiddenCols.Select(c => c.SortMemberPath).Where(s => !string.IsNullOrEmpty(s))!);
            }
        }

        private void BestFit(DataGridColumn? col)
        {
            if (col != null)
            {
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }

        private void resultsGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void resultsGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                var scrollViewer = GetCachedScrollViewer();
                if (scrollViewer != null && DataContext is ResultsViewModel vm)
                {
                    int multiplier = vm.HorizontalScrollSpeed;

                    // Standard delta is usually 120. Convert delta into lines, then multiply by pixel speed.
                    double lines = e.Delta / 120.0;
                    double pixelChange = lines * 3 * multiplier;

                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - pixelChange);
                    e.Handled = true;
                }
            }
        }
    }

    public class HiddenColumnItem
    {
        public string Header { get; set; } = "";
        public DataGridColumn? Column { get; set; }
        public bool IsShowAll { get; set; }
    }
}
