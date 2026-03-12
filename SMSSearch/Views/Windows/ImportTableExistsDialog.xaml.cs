using System.Windows;

namespace SMS_Search.Views.Windows
{
    public partial class ImportTableExistsDialog : Window
    {
        public Services.ExistingTableAction Result { get; private set; } = Services.ExistingTableAction.Skip;

        public ImportTableExistsDialog(string tableName)
        {
            InitializeComponent();
            MessageText.Text = $"The table '{tableName}' already exists in the target database.";
        }

        private void Recreate_Click(object sender, RoutedEventArgs e)
        {
            Result = Services.ExistingTableAction.Recreate;
            DialogResult = true;
        }

        private void RecreateAll_Click(object sender, RoutedEventArgs e)
        {
            Result = Services.ExistingTableAction.RecreateAll;
            DialogResult = true;
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            Result = Services.ExistingTableAction.Skip;
            DialogResult = true;
        }

        private void SkipAll_Click(object sender, RoutedEventArgs e)
        {
            Result = Services.ExistingTableAction.SkipAll;
            DialogResult = true;
        }
    }
}
