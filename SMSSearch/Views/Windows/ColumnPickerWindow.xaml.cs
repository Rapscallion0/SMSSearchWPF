using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SMS_Search.Models;
using SMS_Search.ViewModels.Settings;

namespace SMS_Search.Views.Windows
{
    public partial class ColumnPickerWindow : Window
    {
        private readonly ColumnPickerViewModel _viewModel;
        private System.Windows.Point _dragStartPoint;
        private InsertionAdorner? _insertionAdorner;

        public ColumnPickerWindow(ColumnPickerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            _viewModel.RequestClose += OnRequestClose;
            Loaded += ColumnPickerWindow_Loaded;
        }

        private async void ColumnPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadColumnsAsync();
        }

        private void OnRequestClose(bool confirmed)
        {
            DialogResult = confirmed;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            ClearInsertionAdorner();
            _viewModel.RequestClose -= OnRequestClose;
            Loaded -= ColumnPickerWindow_Loaded;
            _viewModel.Dispose();
        }

        private void AvailableListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _viewModel.AddSelectedCommand.Execute(null);
        }

        private void SelectedListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.RemoveSelectedCommand.CanExecute(null))
            {
                _viewModel.RemoveSelectedCommand.Execute(null);
            }
        }

        private void SelectedListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void SelectedListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            System.Windows.Point currentPos = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (SelectedListBox.SelectedItem is ColumnItem item)
                {
                    System.Windows.DragDrop.DoDragDrop(SelectedListBox, item, System.Windows.DragDropEffects.Move);
                    ClearInsertionAdorner();
                }
            }
        }

        private void SelectedListBox_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ColumnItem)))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                ClearInsertionAdorner();
                e.Handled = true;
                return;
            }

            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;

            var (targetContainer, isInFirstHalf, _) = GetDropTarget(e);
            if (targetContainer != null)
            {
                UpdateInsertionAdorner(targetContainer, isInFirstHalf);
            }
            else
            {
                ClearInsertionAdorner();
            }
        }

        private void SelectedListBox_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            ClearInsertionAdorner();
        }

        private void SelectedListBox_Drop(object sender, System.Windows.DragEventArgs e)
        {
            ClearInsertionAdorner();

            if (!e.Data.GetDataPresent(typeof(ColumnItem))) return;
            if (e.Data.GetData(typeof(ColumnItem)) is not ColumnItem droppedItem) return;

            int oldIndex = _viewModel.SelectedColumns.IndexOf(droppedItem);
            if (oldIndex < 0) return;

            var (_, _, insertionIndex) = GetDropTarget(e);
            int targetIndex = insertionIndex;
            if (oldIndex < targetIndex)
            {
                targetIndex--;
            }

            targetIndex = Math.Clamp(targetIndex, 0, _viewModel.SelectedColumns.Count - 1);
            _viewModel.MoveColumn(oldIndex, targetIndex);
        }

        private (ListBoxItem? Container, bool IsInFirstHalf, int InsertionIndex) GetDropTarget(System.Windows.DragEventArgs e)
        {
            var container = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (container != null)
            {
                int index = SelectedListBox.ItemContainerGenerator.IndexFromContainer(container);
                if (index >= 0)
                {
                    System.Windows.Point pos = e.GetPosition(container);
                    bool isFirstHalf = pos.Y < (container.ActualHeight / 2.0);
                    int insertionIndex = isFirstHalf ? index : index + 1;
                    return (container, isFirstHalf, insertionIndex);
                }
            }

            if (_viewModel.SelectedColumns.Count > 0)
            {
                var lastContainer = SelectedListBox.ItemContainerGenerator.ContainerFromIndex(_viewModel.SelectedColumns.Count - 1) as ListBoxItem;
                return (lastContainer, false, _viewModel.SelectedColumns.Count);
            }

            return (null, true, 0);
        }

        private void UpdateInsertionAdorner(ListBoxItem targetContainer, bool isInFirstHalf)
        {
            if (_insertionAdorner != null &&
                ReferenceEquals(_insertionAdorner.AdornedElement, targetContainer) &&
                _insertionAdorner.IsInFirstHalf == isInFirstHalf)
            {
                return;
            }

            ClearInsertionAdorner();
            var layer = AdornerLayer.GetAdornerLayer(targetContainer);
            if (layer != null)
            {
                _insertionAdorner = new InsertionAdorner(targetContainer, isInFirstHalf, layer);
            }
        }

        private void ClearInsertionAdorner()
        {
            _insertionAdorner?.Detach();
            _insertionAdorner = null;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private sealed class InsertionAdorner : Adorner
        {
            private static readonly System.Windows.Media.Pen LinePen;
            private static readonly System.Windows.Media.Brush AccentBrush;
            private readonly AdornerLayer _layer;

            public bool IsInFirstHalf { get; }

            static InsertionAdorner()
            {
                AccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                AccentBrush.Freeze();
                LinePen = new System.Windows.Media.Pen(AccentBrush, 2);
                LinePen.Freeze();
            }

            public InsertionAdorner(UIElement adornedElement, bool isInFirstHalf, AdornerLayer layer)
                : base(adornedElement)
            {
                IsInFirstHalf = isInFirstHalf;
                _layer = layer;
                IsHitTestVisible = false;
                _layer.Add(this);
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                double y = IsInFirstHalf ? 0 : AdornedElement.RenderSize.Height;
                double width = AdornedElement.RenderSize.Width;
                var start = new System.Windows.Point(2, y);
                var end = new System.Windows.Point(Math.Max(2, width - 2), y);

                drawingContext.DrawLine(LinePen, start, end);
                drawingContext.DrawEllipse(AccentBrush, null, start, 3, 3);
                drawingContext.DrawEllipse(AccentBrush, null, end, 3, 3);
            }

            public void Detach()
            {
                _layer.Remove(this);
            }
        }
    }
}
