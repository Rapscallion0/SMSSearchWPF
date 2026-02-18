using System;
using System.Windows.Controls;
using SMS_Search.ViewModels;
using ComboBox = System.Windows.Controls.ComboBox;
using CommunityToolkit.Mvvm.Messaging;

namespace SMS_Search.Views
{
    public partial class SearchView : System.Windows.Controls.UserControl
    {
        private System.Windows.Threading.DispatcherTimer _typingTimer;

        public SearchView()
        {
            InitializeComponent();
            _typingTimer = new System.Windows.Threading.DispatcherTimer();
            _typingTimer.Interval = TimeSpan.FromMilliseconds(300);
            _typingTimer.Tick += TypingTimer_Tick;

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<SMS_Search.Utils.SearchExecutedMessage>(this, (r, m) =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(FocusActiveSearchInput));
            });

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<SMS_Search.Utils.FocusTableMessage>(this, (r, m) =>
            {
                if (m.Value)
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(FocusTableCombo));
                }
            });
        }

        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            _typingTimer.Stop();
            if (DataContext is SearchViewModel vm)
            {
                vm.FilterTables(TableComboBox.Text);
                if (TableComboBox.IsKeyboardFocusWithin)
                {
                    TableComboBox.IsDropDownOpen = true;
                }
            }
        }

        private void FocusActiveSearchInput()
        {
            if (DataContext is SearchViewModel vm)
            {
                if (vm.SelectedMode == SMS_Search.Data.SearchMode.Function)
                {
                    if (vm.IsFunctionNumber) FocusAndSelect(FunctionNumberBox, force: true);
                    else if (vm.IsFunctionDescription) FocusAndSelect(FunctionDescriptionBox, force: true);
                    else if (vm.IsFunctionCustomSql) FocusAndSelectSql(FunctionSqlEditor, force: true, selectAll: false);
                }
                else if (vm.SelectedMode == SMS_Search.Data.SearchMode.Totalizer)
                {
                    if (vm.IsTotalizerNumber) FocusAndSelect(TotalizerNumberBox, force: true);
                    else if (vm.IsTotalizerDescription) FocusAndSelect(TotalizerDescriptionBox, force: true);
                    else if (vm.IsTotalizerCustomSql) FocusAndSelectSql(TotalizerSqlEditor, force: true, selectAll: false);
                }
                else if (vm.SelectedMode == SMS_Search.Data.SearchMode.Field)
                {
                    if (vm.IsFieldNumber) FocusAndSelect(FieldNumberBox, force: true);
                    else if (vm.IsFieldDescription) FocusAndSelect(FieldDescriptionBox, force: true);
                    else if (vm.IsFieldCustomSql) FocusAndSelectSql(FieldSqlEditor, force: true, selectAll: false);
                    else if (vm.IsFieldTable) FocusTableCombo();
                }
            }
        }

        private void FocusTableCombo()
        {
            if (TableComboBox != null && TableComboBox.Visibility == System.Windows.Visibility.Visible)
            {
                TableComboBox.Focus();
            }
        }

        private void ComboBox_DropDownOpened(object? sender, EventArgs e)
        {
            if (DataContext is SearchViewModel vm)
            {
                vm.LoadTablesCommand.Execute(null);
            }
        }

        private void SearchType_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb)
            {
                // We use Dispatcher to allow the property change to propagate if needed,
                // but mainly to decouple the event slightly.
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    string tag = rb.Tag?.ToString() ?? "";
                    string group = rb.GroupName;

                    if (group == "FctType")
                    {
                        if (tag == "Number") FocusAndSelect(FunctionNumberBox);
                        else if (tag == "Description") FocusAndSelect(FunctionDescriptionBox);
                        else if (tag == "CustomSql") FocusAndSelectSql(FunctionSqlEditor);
                    }
                    else if (group == "TlzType")
                    {
                        if (tag == "Number") FocusAndSelect(TotalizerNumberBox);
                        else if (tag == "Description") FocusAndSelect(TotalizerDescriptionBox);
                        else if (tag == "CustomSql") FocusAndSelectSql(TotalizerSqlEditor);
                    }
                    else if (group == "FldType")
                    {
                        if (tag == "Number") FocusAndSelect(FieldNumberBox);
                        else if (tag == "Description") FocusAndSelect(FieldDescriptionBox);
                        else if (tag == "CustomSql") FocusAndSelectSql(FieldSqlEditor);
                        else if (tag == "Table") FocusTableCombo();
                    }
                }));
            }
        }

        private void FocusAndSelect(SMS_Search.Views.Controls.ClearableTextBox box, bool force = false)
        {
            if (box != null && box.Visibility == System.Windows.Visibility.Visible)
            {
                // If the user clicked the box, it already has focus.
                // We don't want to re-select all text in that case.
                // We only Focus/SelectAll if the user clicked the Radio Button or if forced (Search Executed).
                if (force || !box.IsKeyboardFocusWithin)
                {
                    box.Focus();
                    box.SelectAll();
                }
            }
        }

        private void FocusAndSelectSql(SMS_Search.Views.Controls.SqlEditor editor, bool force = false, bool selectAll = true)
        {
            if (editor != null && editor.Visibility == System.Windows.Visibility.Visible)
            {
                if (force || !editor.IsKeyboardFocusWithin)
                {
                    editor.Focus();
                    if (selectAll)
                    {
                        editor.SelectAll();
                    }
                }
            }
        }

        // Function Tab
        private void FunctionNumberBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsFunctionNumber) vm.IsFunctionNumber = true;
        }

        private void FunctionDescriptionBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsFunctionDescription) vm.IsFunctionDescription = true;
        }

        // Totalizer Tab
        private void TotalizerNumberBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsTotalizerNumber) vm.IsTotalizerNumber = true;
        }

        private void TotalizerDescriptionBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsTotalizerDescription) vm.IsTotalizerDescription = true;
        }

        // Fields Tab
        private void FieldNumberBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsFieldNumber) vm.IsFieldNumber = true;
        }

        private void FieldDescriptionBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsFieldDescription) vm.IsFieldDescription = true;
        }

        private void ComboBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
             if (DataContext is SearchViewModel vm && !vm.IsFieldTable) vm.IsFieldTable = true;
        }

        private void FunctionSqlEditor_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsFunctionCustomSql) vm.IsFunctionCustomSql = true;
        }

        private void TotalizerSqlEditor_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsTotalizerCustomSql) vm.IsTotalizerCustomSql = true;
        }

        private void FieldSqlEditor_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm && !vm.IsFieldCustomSql) vm.IsFieldCustomSql = true;
        }

        private void TableComboBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down ||
                e.Key == System.Windows.Input.Key.Left || e.Key == System.Windows.Input.Key.Right ||
                e.Key == System.Windows.Input.Key.Home || e.Key == System.Windows.Input.Key.End ||
                e.Key == System.Windows.Input.Key.PageUp || e.Key == System.Windows.Input.Key.PageDown ||
                e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab ||
                e.Key == System.Windows.Input.Key.Escape)
            {
                return;
            }


            _typingTimer.Stop();
            _typingTimer.Start();
        }

        private void TableComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
            {
                if (sender is ComboBox cmb)
                {
                    if (cmb.SelectedItem == null && cmb.Items.Count > 0)
                    {
                        cmb.SelectedItem = cmb.Items[0];
                    }
                }
            }
        }
    }
}
