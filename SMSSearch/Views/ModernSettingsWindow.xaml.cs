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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SMS_Search.ViewModels.Settings.ModernSettingsViewModel vm)
            {
                var connSection = System.Linq.Enumerable.FirstOrDefault(vm.Sections, s => s is SMS_Search.ViewModels.Settings.ConnectionSectionViewModel) as SMS_Search.ViewModels.Settings.ConnectionSectionViewModel;
                if (connSection != null)
                {
                    if (string.IsNullOrWhiteSpace(connSection.Server) || string.IsNullOrWhiteSpace(connSection.Database.Value))
                    {
                        vm.SelectedSection = connSection;

                        // Delay scroll to ensure the visual tree is fully rendered
                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                        {
                            try
                            {
                                var container = SectionItemsControl.ItemContainerGenerator.ContainerFromItem(connSection) as FrameworkElement;
                                if (container != null)
                                {
                                    var transform = container.TransformToVisual(MainScrollViewer);
                                    var position = transform.Transform(new System.Windows.Point(0, 0));
                                    MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + position.Y - 20);
                                }
                            }
                            catch (Exception)
                            {
                                // Ignore transform errors if visual tree isn't fully ready
                            }
                        }));
                    }
                }
            }
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
