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
using SMS_Search.ViewModels;
using SMS_Search.Data;

namespace SMS_Search.Views
{
    public partial class ResultsView : UserControl
    {
        private DispatcherTimer _debounceTimer;

        public ResultsView()
        {
            InitializeComponent();
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(500);
            _debounceTimer.Tick += DebounceTimer_Tick;

            this.DataContextChanged += ResultsView_DataContextChanged;
            resultsGrid.AutoGeneratingColumn += resultsGrid_AutoGeneratingColumn;
        }

        private void ResultsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ResultsViewModel newVm)
            {
                newVm.ScrollToRowRequested += Vm_ScrollToRowRequested;
                newVm.HeadersUpdated += Vm_HeadersUpdated;
            }
            if (e.OldValue is ResultsViewModel oldVm)
            {
                oldVm.ScrollToRowRequested -= Vm_ScrollToRowRequested;
                oldVm.HeadersUpdated -= Vm_HeadersUpdated;
            }
        }

        private void Vm_HeadersUpdated(object sender, EventArgs e)
        {
            if (DataContext is ResultsViewModel vm)
            {
                foreach (var col in resultsGrid.Columns)
                {
                    string key = col.SortMemberPath;
                    if (!string.IsNullOrEmpty(key) && vm.ColumnHeaders.TryGetValue(key, out string header))
                    {
                        col.Header = header;
                    }
                }
            }
        }

        private void resultsGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
             if (DataContext is ResultsViewModel vm)
             {
                 string key = e.PropertyName;
                 if (vm.ColumnHeaders.TryGetValue(key, out string header))
                 {
                     e.Column.Header = header;
                 }
             }
        }

        private void Vm_ScrollToRowRequested(object sender, int rowIndex)
        {
            if (resultsGrid.Items.Count > rowIndex && rowIndex >= 0)
            {
                var item = resultsGrid.Items[rowIndex];
                resultsGrid.ScrollIntoView(item);
                resultsGrid.SelectedItems.Clear();
                resultsGrid.SelectedItems.Add(item);

                // Try to focus the grid so keyboard navigation works
                resultsGrid.Focus();
            }
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
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

        private void FilterBySelection_Click(object sender, RoutedEventArgs e)
        {
            if (resultsGrid.SelectedCells.Count > 0)
            {
                var cellInfo = resultsGrid.SelectedCells[0];
                var val = GetCellValue(cellInfo.Item, cellInfo.Column);
                if (val != null)
                {
                    txtFilter.Text = val.ToString();
                }
            }
        }

        private void CopyWithHeaders_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedCells(true, true);
        }

        private void AdvancedCopy_Click(object sender, RoutedEventArgs e)
        {
             if (resultsGrid.SelectedCells.Count == 0) return;

             var dlg = new ClipboardOptionsWindow();
             dlg.Owner = Window.GetWindow(this);
             if (dlg.ShowDialog() == true)
             {
                 CopySelectedCells(false, dlg.PreserveLayout);
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
                Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to copy: " + ex.Message);
            }
        }

        private object GetCellValue(object row, DataGridColumn col)
        {
            if (row == null) return null;
            string propName = col.SortMemberPath;
            if (string.IsNullOrEmpty(propName)) return null;

            var props = TypeDescriptor.GetProperties(row);
            var prop = props[propName];
            return prop?.GetValue(row);
        }

        private string FormatSqlValue(object value)
        {
            if (value == null || value == DBNull.Value) return "NULL";
            if (value is bool b) return b ? "1" : "0";
            if (IsNumeric(value)) return value.ToString();
            if (value is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";
            return $"'{value.ToString().Replace("'", "''")}'";
        }

        private bool IsNumeric(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                txtFilter.Focus();
                e.Handled = true;
            }
        }

        // New Context Menu Command Handlers
        private void CopyColumnName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is DataGridColumn col)
            {
                try { Clipboard.SetText(col.Header.ToString()); } catch { }
            }
        }

        private void HideColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is DataGridColumn col)
            {
                col.Visibility = Visibility.Collapsed;
            }
        }

        private void BestFit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is DataGridColumn col)
            {
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = resultsGrid.SelectedItems;
            if (selectedItems.Count == 0) return;

            var sb = new StringBuilder();
            if (selectedItems.Count > 0)
            {
                var props = TypeDescriptor.GetProperties(selectedItems[0]);
                foreach (var item in selectedItems)
                {
                    var values = new List<string>();
                    foreach (PropertyDescriptor prop in props)
                    {
                        values.Add(prop.GetValue(item)?.ToString() ?? "");
                    }
                    sb.AppendLine(string.Join("\t", values));
                }
            }
            try { Clipboard.SetText(sb.ToString()); } catch { }
        }

        private void CopyAsInsert_Click(object sender, RoutedEventArgs e)
        {
             if (DataContext is ResultsViewModel vm)
             {
                 vm.CopyInsertCommand.Execute(resultsGrid.SelectedItems);
             }
        }
    }
}
