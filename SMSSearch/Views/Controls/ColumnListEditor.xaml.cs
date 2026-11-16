using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace SMS_Search.Views.Controls
{
    public partial class ColumnListEditor : System.Windows.Controls.UserControl
    {
        private bool _isUpdatingFromTextProperty;
        private readonly InvalidColumnColorizer _colorizer;

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ColumnListEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextPropertyChanged));

        public static readonly DependencyProperty InvalidColumnsProperty = DependencyProperty.Register(
            nameof(InvalidColumns),
            typeof(IReadOnlySet<string>),
            typeof(ColumnListEditor),
            new PropertyMetadata(null, OnInvalidColumnsChanged));

        public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
            nameof(HasError),
            typeof(bool),
            typeof(ColumnListEditor),
            new PropertyMetadata(false));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public IReadOnlySet<string>? InvalidColumns
        {
            get => (IReadOnlySet<string>?)GetValue(InvalidColumnsProperty);
            set => SetValue(InvalidColumnsProperty, value);
        }

        public bool HasError
        {
            get => (bool)GetValue(HasErrorProperty);
            set => SetValue(HasErrorProperty, value);
        }

        public ColumnListEditor()
        {
            InitializeComponent();
            _colorizer = new InvalidColumnColorizer();
            Editor.TextArea.TextView.LineTransformers.Add(_colorizer);
            Editor.TextChanged += Editor_TextChanged;
            Unloaded += ColumnListEditor_Unloaded;
        }

        private void ColumnListEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            Editor.TextChanged -= Editor_TextChanged;
            Unloaded -= ColumnListEditor_Unloaded;
        }

        private void Editor_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                e.Handled = true;
            }
        }

        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingFromTextProperty) return;

            string current = Editor.Text.Replace("\r", "").Replace("\n", " ");
            if (current != Editor.Text)
            {
                int caret = Editor.CaretOffset;
                _isUpdatingFromTextProperty = true;
                Editor.Text = current;
                Editor.CaretOffset = Math.Min(caret, current.Length);
                _isUpdatingFromTextProperty = false;
            }

            _isUpdatingFromTextProperty = true;
            Text = current;
            _isUpdatingFromTextProperty = false;
        }

        private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColumnListEditor control && !control._isUpdatingFromTextProperty)
            {
                string newText = (e.NewValue as string) ?? string.Empty;
                if (control.Editor.Text != newText)
                {
                    control._isUpdatingFromTextProperty = true;
                    control.Editor.Text = newText;
                    control._isUpdatingFromTextProperty = false;
                }
            }
        }

        private static void OnInvalidColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColumnListEditor control)
            {
                control._colorizer.InvalidColumns = e.NewValue as IReadOnlySet<string>;
                control.Editor.TextArea.TextView.Redraw();
            }
        }

        private sealed class InvalidColumnColorizer : DocumentColorizingTransformer
        {
            private static readonly System.Windows.Media.Brush ErrorBrush;
            private static readonly Regex IdentifierRegex = new(@"^\[?[A-Za-z_][A-Za-z0-9_]*\]?$", RegexOptions.Compiled);

            public IReadOnlySet<string>? InvalidColumns { get; set; }

            static InvalidColumnColorizer()
            {
                ErrorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47));
                ErrorBrush.Freeze();
            }

            protected override void ColorizeLine(DocumentLine line)
            {
                if (line.Length == 0) return;

                string text = CurrentContext.Document.GetText(line);
                int lineStart = line.Offset;
                int index = 0;

                while (index <= text.Length)
                {
                    int comma = text.IndexOf(',', index);
                    int segEnd = comma == -1 ? text.Length : comma;

                    int tokenStart = index;
                    while (tokenStart < segEnd && char.IsWhiteSpace(text[tokenStart])) tokenStart++;

                    int tokenEnd = segEnd;
                    while (tokenEnd > tokenStart && char.IsWhiteSpace(text[tokenEnd - 1])) tokenEnd--;

                    if (tokenEnd > tokenStart)
                    {
                        string token = text.Substring(tokenStart, tokenEnd - tokenStart);
                        string normalized = token.Trim('[', ']');
                        bool isInvalidSyntax = !IdentifierRegex.IsMatch(token);
                        bool isUnknown = InvalidColumns != null &&
                                         (InvalidColumns.Contains(token) || InvalidColumns.Contains(normalized));

                        if (isInvalidSyntax || isUnknown)
                        {
                            ChangeLinePart(
                                lineStart + tokenStart,
                                lineStart + tokenEnd,
                                element =>
                                {
                                    element.TextRunProperties.SetForegroundBrush(ErrorBrush);
                                    element.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                                });
                        }
                    }

                    if (comma == -1) break;
                    index = comma + 1;
                }
            }
        }
    }
}

