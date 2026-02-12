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
            if (sender is RadioButton rb)
            {
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
                box.Focus();
                box.SelectAll();
            }
        }

        private void FocusAndSelectSql(SMS_Search.Views.Controls.SqlEditor editor)
        {
            if (editor != null && editor.Visibility == System.Windows.Visibility.Visible)
            {
                editor.Focus();
                editor.SelectAll();
            }
        }
    }
}
