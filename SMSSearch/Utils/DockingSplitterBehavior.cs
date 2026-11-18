#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace SMS_Search.Utils
{
    /// <summary>
    /// Isolates AvalonDock GridSplitter drags so resizing between two adjacent panels
    /// does not resize other sibling panels in the same row or column.
    /// </summary>
    public static class DockingSplitterBehavior
    {
        public static readonly DependencyProperty IsolateAdjacentResizeProperty =
            DependencyProperty.RegisterAttached(
                "IsolateAdjacentResize",
                typeof(bool),
                typeof(DockingSplitterBehavior),
                new PropertyMetadata(false, OnIsolateAdjacentResizeChanged));

        public static bool GetIsolateAdjacentResize(DependencyObject obj) =>
            (bool)obj.GetValue(IsolateAdjacentResizeProperty);

        public static void SetIsolateAdjacentResize(DependencyObject obj, bool value) =>
            obj.SetValue(IsolateAdjacentResizeProperty, value);

        private sealed class ActiveDragState
        {
            public required Grid GridControl { get; init; }
            public required System.Windows.Controls.Orientation Orientation { get; init; }
            public required List<ILayoutElement> Children { get; init; }
            public required double[] PreDragSizes { get; init; }
            public required int PrevChildIndex { get; init; }
            public required int NextChildIndex { get; init; }
        }

        private static readonly Dictionary<DockingManager, ActiveDragState?> _activeDrags = new();

        private static void OnIsolateAdjacentResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DockingManager manager) return;

            if ((bool)e.NewValue)
            {
                manager.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnSplitterDragStarted), true);
                manager.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnSplitterDragCompleted), true);
                manager.Unloaded += Manager_Unloaded;
            }
            else
            {
                manager.RemoveHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnSplitterDragStarted));
                manager.RemoveHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnSplitterDragCompleted));
                manager.Unloaded -= Manager_Unloaded;
                _activeDrags.Remove(manager);
            }
        }

        private static void Manager_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DockingManager manager)
            {
                _activeDrags.Remove(manager);
            }
        }

        private static void OnSplitterDragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is not DockingManager manager) return;
            if (e.OriginalSource is not LayoutGridResizerControl resizer) return;

            if (VisualTreeHelper.GetParent(resizer) is not Grid gridControl) return;
            var group = gridControl switch
            {
                LayoutPanelControl lp => lp.Model as ILayoutOrientableGroup,
                LayoutAnchorablePaneGroupControl apg => apg.Model as ILayoutOrientableGroup,
                LayoutDocumentPaneGroupControl dpg => dpg.Model as ILayoutOrientableGroup,
                _ => null
            };
            if (group == null) return;

            var orientation = group.Orientation;
            int splitterGridIndex = orientation == System.Windows.Controls.Orientation.Horizontal
                ? Grid.GetColumn(resizer)
                : Grid.GetRow(resizer);

            int prevChildIndex = (splitterGridIndex - 1) / 2;
            int nextChildIndex = (splitterGridIndex + 1) / 2;

            var children = group.Children
                .Where(IsPositionableVisible)
                .ToList();

            int defCount = orientation == System.Windows.Controls.Orientation.Horizontal
                ? gridControl.ColumnDefinitions.Count
                : gridControl.RowDefinitions.Count;

            int expectedDefCount = children.Count * 2 - 1;
            if (children.Count < 3 || defCount != expectedDefCount)
            {
                _activeDrags[manager] = null;
                return;
            }

            if (prevChildIndex < 0 || nextChildIndex >= children.Count)
            {
                _activeDrags[manager] = null;
                return;
            }

            var preDragSizes = new double[children.Count];
            for (int i = 0; i < children.Count; i++)
            {
                int gridIdx = i * 2;
                double size = orientation == System.Windows.Controls.Orientation.Horizontal
                    ? gridControl.ColumnDefinitions[gridIdx].ActualWidth
                    : gridControl.RowDefinitions[gridIdx].ActualHeight;

                preDragSizes[i] = Math.Max(1.0, size);

                // Normalize all definitions to proportional Star weights matching current actual sizes
                // so that GridSplitter adjusting prev/next leaves every other sibling's pixel size unchanged.
                var star = new GridLength(preDragSizes[i], GridUnitType.Star);
                if (orientation == System.Windows.Controls.Orientation.Horizontal)
                    gridControl.ColumnDefinitions[gridIdx].Width = star;
                else
                    gridControl.RowDefinitions[gridIdx].Height = star;
            }

            _activeDrags[manager] = new ActiveDragState
            {
                GridControl = gridControl,
                Orientation = orientation,
                Children = children,
                PreDragSizes = preDragSizes,
                PrevChildIndex = prevChildIndex,
                NextChildIndex = nextChildIndex
            };
        }

        private static void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (sender is not DockingManager manager) return;
            if (!_activeDrags.TryGetValue(manager, out var state) || state == null) return;
            _activeDrags[manager] = null;

            // Run after AvalonDock's own DragCompleted handler finishes updating DockWidth/DockHeight.
            manager.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                for (int i = 0; i < state.Children.Count; i++)
                {
                    int gridIdx = i * 2;
                    double actualSize = state.Orientation == System.Windows.Controls.Orientation.Horizontal
                        ? (gridIdx < state.GridControl.ColumnDefinitions.Count ? state.GridControl.ColumnDefinitions[gridIdx].ActualWidth : state.PreDragSizes[i])
                        : (gridIdx < state.GridControl.RowDefinitions.Count ? state.GridControl.RowDefinitions[gridIdx].ActualHeight : state.PreDragSizes[i]);

                    // Keep non-adjacent siblings at their exact pre-drag pixel proportion.
                    double targetSize = (i == state.PrevChildIndex || i == state.NextChildIndex)
                        ? Math.Max(1.0, actualSize)
                        : state.PreDragSizes[i];

                    var star = new GridLength(targetSize, GridUnitType.Star);
                    SetDockSize(state.Children[i], state.Orientation, star);
                }
            }));
        }

        private static void SetDockSize(ILayoutElement element, System.Windows.Controls.Orientation orientation, GridLength size)
        {
            switch (element)
            {
                case LayoutAnchorablePane pane:
                    if (orientation == System.Windows.Controls.Orientation.Horizontal) pane.DockWidth = size;
                    else pane.DockHeight = size;
                    break;
                case LayoutAnchorablePaneGroup paneGroup:
                    if (orientation == System.Windows.Controls.Orientation.Horizontal) paneGroup.DockWidth = size;
                    else paneGroup.DockHeight = size;
                    break;
                case LayoutPanel panel:
                    if (orientation == System.Windows.Controls.Orientation.Horizontal) panel.DockWidth = size;
                    else panel.DockHeight = size;
                    break;
                case LayoutDocumentPane docPane:
                    if (orientation == System.Windows.Controls.Orientation.Horizontal) docPane.DockWidth = size;
                    else docPane.DockHeight = size;
                    break;
                case LayoutDocumentPaneGroup docGroup:
                    if (orientation == System.Windows.Controls.Orientation.Horizontal) docGroup.DockWidth = size;
                    else docGroup.DockHeight = size;
                    break;
            }
        }

        private static bool IsPositionableVisible(ILayoutElement element) => element switch
        {
            LayoutAnchorablePane pane => pane.IsVisible,
            LayoutAnchorablePaneGroup paneGroup => paneGroup.IsVisible,
            LayoutPanel panel => panel.IsVisible,
            LayoutDocumentPane docPane => docPane.IsVisible,
            LayoutDocumentPaneGroup docGroup => docGroup.IsVisible,
            _ => false
        };
    }
}
