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

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(FocusActiveSearchInput));
        }

        private string _lastTypedText = "";

        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            _typingTimer.Stop();
            if (DataContext is SearchViewModel vm)
            {
                string text = TableComboBox.Text;
                // If text comes in as POS_DEL and it has a selection, the user only actually typed POS_. We want the _lastTypedText tracking to reflect the actual typed text.
                // We shouldn't rely on just the text, but let's grab the actual typed prefix if there's an active selection from a previous autocomplete.
                int caretIndex = text.Length;

                // Save caret position if possible
                var textBox = TableComboBox.Template.FindName("PART_EditableTextBox", TableComboBox) as System.Windows.Controls.TextBox;
                string actualTypedText = text;
                if (textBox != null)
                {
                    caretIndex = textBox.CaretIndex;
                    if (textBox.SelectionLength > 0 && textBox.SelectionStart + textBox.SelectionLength == text.Length)
                    {
                        // The user has selected text at the end, meaning they only typed up to SelectionStart.
                        actualTypedText = text.Substring(0, textBox.SelectionStart);
                    }
                }

                // Filter using actual typed text so we don't accidentally filter out everything if text was autocompleted
                vm.FilterTables(actualTypedText);

                // Only attempt autocomplete if the user is typing forward, not deleting
                bool isTypingForward = !_isDeleting && actualTypedText.Length > _lastTypedText.Length && actualTypedText.StartsWith(_lastTypedText, StringComparison.OrdinalIgnoreCase);
                _lastTypedText = actualTypedText;
                _isDeleting = false;

                // Look for a StartsWith match in the currently filtered view.
                string? startsWithMatch = null;
                if (isTypingForward && !string.IsNullOrEmpty(actualTypedText))
                {
                    foreach (var item in vm.TablesView)
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
                    // Found a match to autocomplete
                    vm.SelectedTable = startsWithMatch;

                    // The text in the combobox will now be the full matched table.
                    // We need to preserve the originally typed case, append the rest of the match, and select the appended part.
                    if (textBox != null)
                    {
                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                        {
                            string remaining = startsWithMatch.Substring(actualTypedText.Length);
                            textBox.Text = actualTypedText + remaining;
                            textBox.SelectionStart = actualTypedText.Length;
                            textBox.SelectionLength = remaining.Length;
                        }));
                    }
                }
                else
                {
                    // No autocomplete match, check if it's an exact match
                    var exactMatch = System.Linq.Enumerable.FirstOrDefault(vm.Tables, t => t.Equals(actualTypedText, StringComparison.OrdinalIgnoreCase));
                    if (exactMatch != null)
                    {
                        if (vm.SelectedTable != exactMatch)
                        {
                            vm.SelectedTable = exactMatch;
                        }
                    }
                    else
                    {
                        if (vm.SelectedTable != null)
                        {
                            vm.SelectedTable = null;

                            // Setting SelectedTable to null clears the text in the ComboBox.
                            // Restore the typed text and caret position.
                            TableComboBox.Text = actualTypedText;
                            if (textBox != null)
                            {
                                textBox.CaretIndex = caretIndex <= textBox.Text.Length ? caretIndex : textBox.Text.Length;
                            }
                        }
                    }

                    if (TableComboBox.IsKeyboardFocusWithin)
                    {
                        // Prevent WPF from auto-selecting text when IsDropDownOpen becomes true
                        if (textBox != null)
                        {
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                            {
                                textBox.SelectionLength = 0;
                                // Ensure caret index is restored to its proper position, rather than strictly end
                                textBox.CaretIndex = caretIndex <= textBox.Text.Length ? caretIndex : textBox.Text.Length;
                            }));
                        }
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

             var textBox = TableComboBox.Template.FindName("PART_EditableTextBox", TableComboBox) as System.Windows.Controls.TextBox;
             if (textBox != null)
             {
                 Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                 {
                     textBox.SelectionLength = 0;
                     textBox.CaretIndex = textBox.Text.Length;
                 }));
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
