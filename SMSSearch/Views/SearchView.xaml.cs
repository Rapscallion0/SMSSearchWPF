using System;
using System.Windows.Controls;
using SMS_Search.ViewModels;

namespace SMS_Search.Views
{
    public partial class SearchView : UserControl
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
    }
}
