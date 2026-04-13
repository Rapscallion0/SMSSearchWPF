using System.Windows;

namespace SMS_Search.Views.Gs1
{
    public partial class Gs1ToolkitWindow : Window
    {
        public Gs1ToolkitWindow()
        {
            InitializeComponent();
            DataContextChanged += Gs1ToolkitWindow_DataContextChanged;
        }

        private void Window_LocationChanged(object? sender, System.EventArgs e)
        {
            ToastWindow.UpdateAllToastPositions(false, this);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ToastWindow.UpdateAllToastPositions(false, this);
        }

        private void Gs1ToolkitWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.Gs1.Gs1ToolkitViewModel oldVm)
            {
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }
            if (e.NewValue is ViewModels.Gs1.Gs1ToolkitViewModel newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
                UpdatePlaceholderWidth(newVm);
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.Gs1.Gs1ToolkitViewModel.IsHistoryPanelPinned) ||
                e.PropertyName == nameof(ViewModels.Gs1.Gs1ToolkitViewModel.IsHistoryPanelOpen) ||
                e.PropertyName == nameof(ViewModels.Gs1.Gs1ToolkitViewModel.HistoryPanelWidth))
            {
                if (DataContext is ViewModels.Gs1.Gs1ToolkitViewModel vm)
                {
                    UpdatePlaceholderWidth(vm);
                }
            }
        }

        private void UpdatePlaceholderWidth(ViewModels.Gs1.Gs1ToolkitViewModel vm)
        {
            double targetWidth = 0;
            if (vm.IsHistoryPanelPinned && vm.IsHistoryPanelOpen)
            {
                targetWidth = vm.HistoryPanelWidth;
            }

            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = targetWidth,
                Duration = new Duration(System.TimeSpan.FromMilliseconds(250)),
                DecelerationRatio = 0.8,
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
            };

            Column2Placeholder.BeginAnimation(WidthProperty, animation);
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is ViewModels.Gs1.Gs1ParsedAiViewModel vm)
            {
                if (vm.IsModified)
                {
                    vm.CommitCommand.Execute(null);
                }
            }
        }

        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is ViewModels.Gs1.Gs1ParsedAiViewModel vm)
            {
                if (vm.Model?.Definition != null)
                {
                    string dataType = vm.Model.Definition.DataType ?? "";
                    // Reject whitespace
                    if (string.IsNullOrWhiteSpace(e.Text))
                    {
                        e.Handled = true;
                        return;
                    }

                    // Numeric only
                    if (dataType.Contains("N") && !dataType.Contains("X"))
                    {
                        e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
                    }
                    // Alphanumeric ("X") usually allows anything except maybe whitespace, which we already blocked above.
                }
            }
        }

        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is ViewModels.Gs1.Gs1ParsedAiViewModel vm)
            {
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string text = (string)e.DataObject.GetData(typeof(string));

                    if (string.IsNullOrWhiteSpace(text) || text.Contains(" ") || text.Contains("\t") || text.Contains("\n") || text.Contains("\r"))
                    {
                        e.CancelCommand();
                        return;
                    }

                    if (vm.Model?.Definition != null)
                    {
                        string dataType = vm.Model.Definition.DataType ?? "";
                        if (dataType.Contains("N") && !dataType.Contains("X"))
                        {
                            if (!System.Text.RegularExpressions.Regex.IsMatch(text, "^[0-9]+$"))
                            {
                                e.CancelCommand();
                            }
                        }
                    }
                }
                else
                {
                    e.CancelCommand();
                }
            }
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                System.Windows.DataObject.AddPastingHandler(textBox, TextBox_Pasting);
            }
        }

        private void TextBox_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                System.Windows.DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
            }
        }

        private void HistoryItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBoxItem item && item.DataContext is Models.Gs1.Gs1HistoryItem historyItem)
            {
                if (DataContext is ViewModels.Gs1.Gs1ToolkitViewModel vm)
                {
                    if (vm.LoadHistoryItemCommand.CanExecute(historyItem))
                    {
                        vm.LoadHistoryItemCommand.Execute(historyItem);
                    }
                }
            }
        }

        private void Segment_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ViewModels.Gs1.Gs1BarcodeSegmentViewModel vm)
            {
                vm.StartHover();
                if (vm.AssociatedAi != null)
                {
                    AisDataGrid.ScrollIntoView(vm.AssociatedAi);
                }
            }
        }

        private void AisDataGrid_SelectedCellsChanged(object sender, System.Windows.Controls.SelectedCellsChangedEventArgs e)
        {
            if (e.AddedCells.Count > 0)
            {
                var item = e.AddedCells[0].Item;
                if (item is ViewModels.Gs1.Gs1ParsedAiViewModel aiVm)
                {
                    if (DataContext is ViewModels.Gs1.Gs1ToolkitViewModel vm)
                    {
                        vm.SelectedAi = aiVm;
                    }
                }
            }
        }

        private void Segment_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ViewModels.Gs1.Gs1BarcodeSegmentViewModel vm)
            {
                vm.EndHover();
            }
        }

        private void Segment_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ViewModels.Gs1.Gs1BarcodeSegmentViewModel vm)
            {
                if (vm.AssociatedAi != null && DataContext is ViewModels.Gs1.Gs1ToolkitViewModel mainVm)
                {
                    mainVm.SelectedAi = vm.AssociatedAi;
                    AisDataGrid.ScrollIntoView(vm.AssociatedAi);

                    // Delay slightly to allow virtualization to generate the row if needed
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var row = (System.Windows.Controls.DataGridRow)AisDataGrid.ItemContainerGenerator.ContainerFromItem(vm.AssociatedAi);
                        if (row != null)
                        {
                            // Column 3 is "Value" column
                            System.Windows.Controls.DataGridCell? cell = GetCell(AisDataGrid, row, 3);
                            if (cell != null)
                            {
                                cell.Focus();
                                AisDataGrid.SelectedCells.Clear();
                                AisDataGrid.SelectedCells.Add(new System.Windows.Controls.DataGridCellInfo(cell));
                            }
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private System.Windows.Controls.DataGridCell? GetCell(System.Windows.Controls.DataGrid grid, System.Windows.Controls.DataGridRow row, int column)
        {
            if (row != null)
            {
                var presenter = GetVisualChild<System.Windows.Controls.Primitives.DataGridCellsPresenter>(row);
                if (presenter == null) return null;

                var cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as System.Windows.Controls.DataGridCell;
                if (cell == null)
                {
                    grid.ScrollIntoView(row, grid.Columns[column]);
                    cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as System.Windows.Controls.DataGridCell;
                }
                return cell;
            }
            return null;
        }

        private T? GetVisualChild<T>(System.Windows.Media.Visual parent) where T : System.Windows.Media.Visual
        {
            T? child = default;
            int numVisuals = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                System.Windows.Media.Visual v = (System.Windows.Media.Visual)System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
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

        private void HistoryPanelThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (DataContext is ViewModels.Gs1.Gs1ToolkitViewModel vm)
            {
                // Dragging from the left edge of a right-docked panel: negative HorizontalChange means width increases.
                double newWidth = vm.HistoryPanelWidth - e.HorizontalChange;
                if (newWidth >= 200)
                {
                    vm.HistoryPanelWidth = newWidth;
                }
            }
        }

        private void CollapsedHistoryBar_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.Gs1.Gs1ToolkitViewModel vm)
            {
                vm.IsHistoryPanelOpen = true;
            }
        }

        private void HistoryOverlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.Gs1.Gs1ToolkitViewModel vm && vm.IsHistoryPanelOpen && !vm.IsHistoryPanelPinned)
            {
                vm.IsHistoryPanelOpen = false;
            }
        }
    }
}
