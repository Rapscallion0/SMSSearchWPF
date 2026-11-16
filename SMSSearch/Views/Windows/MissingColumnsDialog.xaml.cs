using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SMS_Search.Models;

namespace SMS_Search.Views.Windows
{
    public partial class MissingColumnsDialog : Window
    {
        public MissingColumnDialogResult Result { get; private set; }

        private List<MissingColumnInfo> _columns;

        public MissingColumnsDialog(string tableName, List<MissingColumnInfo> missingColumns, bool isTypeConfirmation = false)
        {
            InitializeComponent();
            _columns = missingColumns;

            if (isTypeConfirmation)
            {
                Title = "Confirm Inferred Column Types";
                HeaderText.Text = "Confirm Column Data Types";
                HeaderText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x19, 0x76, 0xD2));
                MessageText.Text = $"Review and adjust the inferred SQL data types for table '{tableName}' before creating it.";
                ColumnsGrid.Columns[1].Visibility = Visibility.Collapsed;
                ColumnsGrid.Columns[2].Visibility = Visibility.Collapsed;
            }
            else
            {
                int sourceMissing = _columns.Count(c => c.IsInTarget && !c.IsInSource);
                int targetMissing = _columns.Count(c => c.IsInSource && !c.IsInTarget);

                MessageText.Text = $"There are column mismatches for table '{tableName}'.\n" +
                                   $"Missing from source file: {sourceMissing}\n" +
                                   $"Missing from template database: {targetMissing}\n\n" +
                                   $"Select which missing columns you'd like to import and choose their data types.";

                // For columns that are in Target but NOT in Source, it makes no sense to check "Import" because we have no data
                // Let's force ShouldImport to false for these
                foreach (var col in _columns.Where(c => !c.IsInSource))
                {
                    col.ShouldImport = false;
                }
            }

            ColumnsGrid.ItemsSource = _columns;
            Result = new MissingColumnDialogResult { Action = MissingColumnAction.Cancel, Columns = _columns };
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            Result.Action = MissingColumnAction.Continue;
            Result.Columns = _columns;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result.Action = MissingColumnAction.Cancel;
            DialogResult = false;
        }
    }
}
