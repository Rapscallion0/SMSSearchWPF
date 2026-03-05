using System;
using System.Windows;
using System.Windows.Controls;
using SMS_Search.ViewModels.Settings;

namespace SMS_Search.Views.Settings
{
    public partial class ConnectionSectionView : System.Windows.Controls.UserControl
    {
        private System.Windows.Threading.DispatcherTimer _typingTimer;
        private string _lastTypedText = "";
        private bool _isDeleting = false;

        public ConnectionSectionView()
        {
            InitializeComponent();
            _typingTimer = new System.Windows.Threading.DispatcherTimer();
            _typingTimer.Interval = TimeSpan.FromMilliseconds(300);
            _typingTimer.Tick += TypingTimer_Tick;
        }

        private System.Windows.Controls.ComboBox? GetDatabaseComboBox()
        {
            return FindVisualChild<System.Windows.Controls.ComboBox>(this);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T t)
                    return t;
                else
                {
                    T? childOfChild = FindVisualChild<T>(child!);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        private void TypingTimer_Tick(object? sender, EventArgs e)
        {
            _typingTimer.Stop();
            var databaseComboBox = GetDatabaseComboBox();
            if (databaseComboBox == null) return;

            if (DataContext is ConnectionSectionViewModel vm)
            {
                string text = databaseComboBox.Text;
                int caretIndex = text.Length;

                var textBox = databaseComboBox.Template.FindName("PART_EditableTextBox", databaseComboBox) as System.Windows.Controls.TextBox;
                string actualTypedText = text;
                if (textBox != null)
                {
                    caretIndex = textBox.CaretIndex;
                    if (textBox.SelectionLength > 0 && textBox.SelectionStart + textBox.SelectionLength == text.Length)
                    {
                        actualTypedText = text.Substring(0, textBox.SelectionStart);
                    }
                }

                vm.FilterDatabases(actualTypedText);

                bool isTypingForward = !_isDeleting && actualTypedText.Length > _lastTypedText.Length && actualTypedText.StartsWith(_lastTypedText, StringComparison.OrdinalIgnoreCase);
                _lastTypedText = actualTypedText;
                _isDeleting = false;

                string? startsWithMatch = null;
                if (isTypingForward && !string.IsNullOrEmpty(actualTypedText))
                {
                    foreach (var item in vm.DatabasesView)
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
                    vm.Database.Value = startsWithMatch;

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
                    var exactMatch = System.Linq.Enumerable.FirstOrDefault(vm.Databases, t => t.Equals(actualTypedText, StringComparison.OrdinalIgnoreCase));
                    if (exactMatch != null)
                    {
                        if (vm.Database.Value != exactMatch)
                        {
                            vm.Database.Value = exactMatch;
                        }
                    }
                    else
                    {
                        if (vm.Database.Value != null)
                        {
                            vm.Database.Value = actualTypedText;

                            databaseComboBox.Text = actualTypedText;
                            if (textBox != null)
                            {
                                textBox.CaretIndex = caretIndex <= textBox.Text.Length ? caretIndex : textBox.Text.Length;
                            }
                        }
                    }

                    if (databaseComboBox.IsKeyboardFocusWithin)
                    {
                        if (textBox != null)
                        {
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                            {
                                textBox.SelectionLength = 0;
                                textBox.CaretIndex = caretIndex <= textBox.Text.Length ? caretIndex : textBox.Text.Length;
                            }));
                        }
                    }
                }
            }
        }

        private void DatabaseComboBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
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

        private void DatabaseComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
            {
                if (sender is System.Windows.Controls.ComboBox cmb)
                {
                    if (cmb.SelectedItem == null && cmb.Items.Count > 0)
                    {
                        cmb.SelectedItem = cmb.Items[0];
                    }
                }
            }
            else if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down)
            {
                if (sender is System.Windows.Controls.ComboBox cmb && cmb.IsEditable)
                {
                    if (cmb.Items.Count > 0)
                    {
                        int currentIndex = cmb.SelectedIndex;

                        if (currentIndex == -1 && !string.IsNullOrEmpty(cmb.Text))
                        {
                            var match = System.Linq.Enumerable.FirstOrDefault(cmb.Items.Cast<string>(), i => i.StartsWith(cmb.Text, StringComparison.OrdinalIgnoreCase) || i.IndexOf(cmb.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (match != null)
                            {
                                currentIndex = cmb.Items.IndexOf(match);
                            }
                        }

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
                                if (!string.IsNullOrEmpty(typedText) && selectedStr.StartsWith(typedText, StringComparison.OrdinalIgnoreCase))
                                {
                                    textBox.Text = typedText + selectedStr.Substring(typedText.Length);
                                    textBox.SelectionStart = typedText.Length;
                                    textBox.SelectionLength = selectedStr.Length - typedText.Length;
                                }
                                else
                                {
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

        private void DatabaseComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var databaseComboBox = GetDatabaseComboBox();
            if (databaseComboBox == null) return;

            var textBox = databaseComboBox.Template.FindName("PART_EditableTextBox", databaseComboBox) as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    textBox.SelectionLength = 0;
                    textBox.CaretIndex = textBox.Text.Length;
                }));
            }
        }
    }
}
