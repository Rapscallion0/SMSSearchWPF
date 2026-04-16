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
            _typingTimer.Interval = TimeSpan.FromMilliseconds(100);
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

        private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_typingTimer != null)
            {
                _typingTimer.Stop();
                _typingTimer.Tick -= TypingTimer_Tick;
            }
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(FocusActiveSearchInput));
        }

        private string _lastTypedText = "";
        private string? _lastValidTable;

        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            _typingTimer.Stop();
            if (DataContext is SearchViewModel vm)
            {
                var textBox = TableComboBox.Template.FindName("PART_EditableTextBox", TableComboBox) as System.Windows.Controls.TextBox;
                if (textBox == null) return;

                string text = textBox.Text;
                int caretIndex = textBox.CaretIndex;

                string actualTypedText = text;
                if (textBox.SelectionLength > 0 && textBox.SelectionStart + textBox.SelectionLength == text.Length)
                {
                    actualTypedText = text.Substring(0, textBox.SelectionStart);
                }

                bool isTypingForward = !_isDeleting && actualTypedText.Length >= _lastTypedText.Length && actualTypedText.StartsWith(_lastTypedText, StringComparison.OrdinalIgnoreCase);
                _lastTypedText = actualTypedText;
                _isDeleting = false;

                string? startsWithMatch = null;
                if (!string.IsNullOrEmpty(actualTypedText))
                {
                    foreach (var item in vm.Tables)
                    {
                        if (item is string str && str.StartsWith(actualTypedText, StringComparison.OrdinalIgnoreCase))
                        {
                            startsWithMatch = str;
                            break;
                        }
                    }
                }

                if (startsWithMatch != null)
                {
                    vm.SelectedTable = startsWithMatch;
                    string remaining = startsWithMatch.Substring(actualTypedText.Length);

                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                    {
                        textBox.Text = actualTypedText + remaining;
                        textBox.SelectionStart = actualTypedText.Length;
                        textBox.SelectionLength = remaining.Length;
                    }));
                }
                else
                {
                    var exactMatch = System.Linq.Enumerable.FirstOrDefault(vm.Tables, t => t.Equals(actualTypedText, StringComparison.OrdinalIgnoreCase));
                    if (exactMatch != null)
                    {
                        if (vm.SelectedTable != exactMatch)
                        {
                            vm.SelectedTable = exactMatch;
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                            {
                                textBox.SelectionLength = 0;
                                textBox.CaretIndex = textBox.Text.Length;
                            }));
                        }
                    }
                    else
                    {
                        if (vm.SelectedTable != null)
                        {
                            vm.SelectedTable = null;
                        }

                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                        {
                            if (textBox.Text != actualTypedText)
                            {
                                textBox.Text = actualTypedText;
                            }
                            textBox.SelectionLength = 0;
                            textBox.CaretIndex = Math.Min(caretIndex, textBox.Text.Length);

                            if (string.IsNullOrEmpty(actualTypedText) && !TableComboBox.IsDropDownOpen)
                            {
                                TableComboBox.IsDropDownOpen = true;
                            }
                        }));
                    }
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource == sender)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(FocusActiveSearchInput));
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

        private void FocusAndSelectSql(SMS_Search.Views.Controls.SqlEditor editor, bool force = false, bool selectAll = false)
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
             if (DataContext is SearchViewModel vm)
             {
                 if (!vm.IsFieldTable) vm.IsFieldTable = true;
                 _lastValidTable = vm.SelectedTable;
             }

             var textBox = TableComboBox.Template.FindName("PART_EditableTextBox", TableComboBox) as System.Windows.Controls.TextBox;
             if (textBox != null)
             {
                 Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                 {
                     textBox.SelectAll();
                 }));
             }
        }

        private void TableComboBox_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var textBox = TableComboBox.Template.FindName("PART_EditableTextBox", TableComboBox) as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                textBox.SelectAll();
                e.Handled = true;
            }
        }

        private void TableComboBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SearchViewModel vm)
            {
                string text = TableComboBox.Text;
                if (!vm.Tables.Contains(text))
                {
                    if (vm.SelectedTable != _lastValidTable)
                    {
                        vm.SelectedTable = _lastValidTable;
                    }
                    TableComboBox.Text = _lastValidTable ?? "";
                }
                else
                {
                    _lastValidTable = text;
                }
            }
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

        private bool _isDeleting = false;

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

            _isDeleting = e.Key == System.Windows.Input.Key.Back || e.Key == System.Windows.Input.Key.Delete;

            _typingTimer.Stop();
            _typingTimer.Start();
        }

        private void TableComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Back)
            {
                if (sender is ComboBox cmb)
                {
                    var textBox = cmb.Template.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
                    if (textBox != null && textBox.SelectionLength > 0 && textBox.SelectionStart + textBox.SelectionLength == textBox.Text.Length)
                    {
                        if (textBox.SelectionStart > 0)
                        {
                            string newText = textBox.Text.Substring(0, textBox.SelectionStart - 1);
                            textBox.Text = newText;
                            textBox.CaretIndex = newText.Length;
                            _lastTypedText = newText;
                            _isDeleting = false;
                            _typingTimer.Stop();
                            _typingTimer.Start();
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
            else if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
            {
                if (sender is ComboBox cmb)
                {
                    if (cmb.SelectedItem == null && cmb.Items.Count > 0)
                    {
                        cmb.SelectedItem = cmb.Items[0];
                    }
                }
            }
            else if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down)
            {
                if (sender is ComboBox cmb && cmb.IsEditable)
                {
                    if (cmb.Items.Count > 0)
                    {
                        int currentIndex = cmb.SelectedIndex;

                        // If the text was just typed but no item selected, start at -1 so down goes to 0
                        if (currentIndex == -1 && !string.IsNullOrEmpty(cmb.Text))
                        {
                            var match = System.Linq.Enumerable.FirstOrDefault(cmb.Items.Cast<string>(), i => i.StartsWith(cmb.Text, StringComparison.OrdinalIgnoreCase) || i.IndexOf(cmb.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (match != null)
                            {
                                currentIndex = cmb.Items.IndexOf(match);
                                // if we're going down from unselected but matching text, pressing down should just select that match (index)
                                // wait, if we are at the exact match, it should be selected. If we are typed a prefix, we should just start cycling from 0.
                            }
                        }

                        // Actually, if we're typing, the text might not exactly match an item yet,
                        // but since we autocomplete StartsWith matches, cmb.SelectedIndex is usually already set if a StartsWith match exists.
                        // However, if it's a "Contains" match, SelectedIndex might be -1.
                        // Let's just cycle simply based on the current items.
                        if (e.Key == System.Windows.Input.Key.Down)
                        {
                            currentIndex++;
                            if (currentIndex >= cmb.Items.Count)
                                currentIndex = cmb.Items.Count - 1;
                        }
                        else if (e.Key == System.Windows.Input.Key.Up)
                        {
                            currentIndex--;
                            if (currentIndex < 0)
                                currentIndex = 0;
                        }

                        cmb.SelectedIndex = currentIndex;

                        var textBox = cmb.Template.FindName("PART_EditableTextBox", cmb) as System.Windows.Controls.TextBox;
                        if (textBox != null && cmb.SelectedItem is string selectedStr)
                        {
                            string typedText = _lastTypedText;

                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                            {
                                // If the item starts with the originally typed text, we do inline autocomplete style selection
                                if (!string.IsNullOrEmpty(typedText) && selectedStr.StartsWith(typedText, StringComparison.OrdinalIgnoreCase))
                                {
                                    textBox.Text = typedText + selectedStr.Substring(typedText.Length);
                                    textBox.SelectionStart = typedText.Length;
                                    textBox.SelectionLength = selectedStr.Length - typedText.Length;
                                }
                                else
                                {
                                    // Otherwise, select the whole text
                                    textBox.Text = selectedStr;
                                    textBox.SelectAll();
                                }
                            }));
                        }

                        e.Handled = true;
                    }
                }
            }
        }
    }
}
