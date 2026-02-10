using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SMS_Search.Views
{
    public partial class ModernSettingsWindow : Window
    {
        private bool _isSyncingSelection;
        private bool _isSyncingScroll;

        public ModernSettingsWindow(SMS_Search.ViewModels.Settings.ModernSettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingScroll) return;

            _isSyncingSelection = true;
            try
            {
                var section = Sidebar.SelectedItem;
                if (section != null)
                {
                    var container = SectionItemsControl.ItemContainerGenerator.ContainerFromItem(section) as FrameworkElement;
                    if (container != null)
                    {
                        var transform = container.TransformToVisual(MainScrollViewer);
                        var position = transform.Transform(new System.Windows.Point(0, 0));

                        // Scroll to position with padding
                        MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + position.Y - 20);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore transformation errors
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingSelection) return;

            _isSyncingScroll = true;
            try
            {
                // Find the first visible item at the top
                foreach (var item in SectionItemsControl.Items)
                {
                    var container = SectionItemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        try
                        {
                            var transform = container.TransformToVisual(MainScrollViewer);
                            var position = transform.Transform(new System.Windows.Point(0, 0));

                            // If the bottom of the item is below the top margin (e.g. 50px), it's the "active" one
                            if (position.Y + container.ActualHeight > 50)
                            {
                                Sidebar.SelectedItem = item;
                                break;
                            }
                        }
                        catch { continue; }
                    }
                }
            }
            finally
            {
                _isSyncingScroll = false;
            }
        }
    }
}
