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

        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is ViewModels.Gs1.Gs1ParsedAiViewModel vm)
            {
                if (vm.Model?.Definition != null)
                {
                    string dataType = vm.Model.Definition.DataType ?? "";
                    // Reject whitespace
                    if (string.IsNullOrWhiteSpace(e.Text))
                    {
                        e.Handled = true;
                        return;
                    }

                    // Numeric only
                    if (dataType.Contains("N") && !dataType.Contains("X"))
                    {
                        e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
                    }
                    // Alphanumeric ("X") usually allows anything except maybe whitespace, which we already blocked above.
                }
            }
        }

        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.DataContext is ViewModels.Gs1.Gs1ParsedAiViewModel vm)
            {
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string text = (string)e.DataObject.GetData(typeof(string));

                    if (string.IsNullOrWhiteSpace(text) || text.Contains(" ") || text.Contains("\t") || text.Contains("\n") || text.Contains("\r"))
                    {
                        e.CancelCommand();
                        return;
                    }

                    if (vm.Model?.Definition != null)
                    {
                        string dataType = vm.Model.Definition.DataType ?? "";
                        if (dataType.Contains("N") && !dataType.Contains("X"))
                        {
                            if (!System.Text.RegularExpressions.Regex.IsMatch(text, "^[0-9]+$"))
                            {
                                e.CancelCommand();
                            }
                        }
                    }
                }
                else
                {
                    e.CancelCommand();
                }
            }
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                System.Windows.DataObject.AddPastingHandler(textBox, TextBox_Pasting);
            }
        }

        private void TextBox_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                System.Windows.DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
            }
        }

        private void HistoryItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBoxItem item && item.DataContext is Models.Gs1.Gs1HistoryItem historyItem)
            {
                if (DataContext is ViewModels.Gs1.Gs1ToolkitViewModel vm)
                {
                    if (vm.LoadHistoryItemCommand.CanExecute(historyItem))
                    {
                        vm.LoadHistoryItemCommand.Execute(historyItem);
                    }
                }
            }
        }

        private void Segment_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ViewModels.Gs1.Gs1BarcodeSegmentViewModel vm)
            {
                vm.StartHover();
            }
        }

        private void Segment_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ViewModels.Gs1.Gs1BarcodeSegmentViewModel vm)
            {
                vm.EndHover();
            }
        }
    }
}
