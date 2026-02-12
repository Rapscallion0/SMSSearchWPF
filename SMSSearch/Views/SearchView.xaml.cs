using System;
using System.Windows.Controls;
using SMS_Search.ViewModels;

namespace SMS_Search.Views
{
    public partial class SearchView : System.Windows.Controls.UserControl
    {
        public SearchView()
        {
            InitializeComponent();
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
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
                    }
                }));
            }
        }

        private void FocusAndSelect(SMS_Search.Views.Controls.ClearableTextBox box)
        {
            if (box != null && box.Visibility == System.Windows.Visibility.Visible)
            {
                // If the user clicked the box, it already has focus.
                // We don't want to re-select all text in that case.
                // We only Focus/SelectAll if the user clicked the Radio Button.
                if (!box.IsKeyboardFocusWithin)
                {
                    box.Focus();
                    box.SelectAll();
                }
            }
        }

        private void FocusAndSelectSql(SMS_Search.Views.Controls.SqlEditor editor)
        {
            if (editor != null && editor.Visibility == System.Windows.Visibility.Visible)
            {
                if (!editor.IsKeyboardFocusWithin)
                {
                    editor.Focus();
                    editor.SelectAll();
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
    }
}
