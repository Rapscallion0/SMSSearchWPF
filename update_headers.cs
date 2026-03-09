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
                ScrollViewer? scrollViewer = GetVisualChild<ScrollViewer>(resultsGrid);
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
