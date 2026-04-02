using System.Windows;

namespace SMS_Search.Views.Gs1
{
    public partial class Gs1ToolkitWindow : Window
    {
        public Gs1ToolkitWindow()
        {
            InitializeComponent();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is ViewModels.Gs1.Gs1ParsedAiViewModel vm)
            {
                if (vm.IsModified)
                {
                    vm.CommitCommand.Execute(null);
                }
            }
        }
    }
}
