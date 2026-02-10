using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using SMS_Search.ViewModels;

namespace SMS_Search.Views
{
    public partial class CleanSqlSettingsView : UserControl
    {
        public CleanSqlSettingsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CleanSqlSettingsViewModel oldVm)
            {
                oldVm.Rules.CollectionChanged -= OnRulesCollectionChanged;
            }

            if (e.NewValue is CleanSqlSettingsViewModel newVm)
            {
                newVm.Rules.CollectionChanged += OnRulesCollectionChanged;
            }
        }

        private void OnRulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
            {
                var lastItem = e.NewItems[e.NewItems.Count - 1];
                if (lastItem != null)
                {
                    // Use Dispatcher to ensure the item is in the view before scrolling
                    Dispatcher.InvokeAsync(() =>
                    {
                        RulesDataGrid.ScrollIntoView(lastItem);
                        RulesDataGrid.SelectedItem = lastItem;
                    });
                }
            }
        }
    }
}
