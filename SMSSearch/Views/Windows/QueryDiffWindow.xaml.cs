using System.Windows;

namespace SMS_Search.Views.Windows
{
    public enum QueryDiffResolution
    {
        KeepExisting,
        UseNewFormatting,
        SaveAsNewCopy
    }

    public partial class QueryDiffWindow : Window
    {
        public QueryDiffResolution Resolution { get; private set; } = QueryDiffResolution.KeepExisting;

        public QueryDiffWindow(
            string headerMessage,
            string existingSql,
            string newSql,
            bool allowSaveAsNewCopy)
        {
            InitializeComponent();
            HeaderTextBlock.Text = headerMessage;
            ExistingSqlBox.Text = existingSql;
            NewSqlBox.Text = newSql;
            SaveNewCopyButton.Visibility = allowSaveAsNewCopy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UseNewButton_Click(object sender, RoutedEventArgs e)
        {
            Resolution = QueryDiffResolution.UseNewFormatting;
            DialogResult = true;
            Close();
        }

        private void SaveNewCopyButton_Click(object sender, RoutedEventArgs e)
        {
            Resolution = QueryDiffResolution.SaveAsNewCopy;
            DialogResult = true;
            Close();
        }

        private void KeepExistingButton_Click(object sender, RoutedEventArgs e)
        {
            Resolution = QueryDiffResolution.KeepExisting;
            DialogResult = false;
            Close();
        }
    }
}

