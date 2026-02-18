using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Extensions.DependencyInjection;
using SMS_Search.Services;
using SMS_Search.Utils;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Xml;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace SMS_Search.Views.Controls
{
    public partial class SqlEditor : System.Windows.Controls.UserControl
    {
        private CompletionWindow? _completionWindow;
        private IIntellisenseService? _intellisenseService;
        private ILoggerService? _logger;
        private IDialogService? _dialogService;

        private IntellisenseLevel _currentLevel = IntellisenseLevel.Schema;
        private bool _isCycling = false;

        public SqlEditor()
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;

            if (System.Windows.Application.Current is App app && app.Services != null)
            {
                _logger = app.Services.GetService<ILoggerService>();
                _intellisenseService = app.Services.GetService<IIntellisenseService>();
                _dialogService = app.Services.GetService<IDialogService>();

                // Initialize current level from service default
                if (_intellisenseService != null)
                {
                    _currentLevel = _intellisenseService.DefaultLevel;
                }
            }

            LoadHighlighting();

            Editor.TextArea.TextEntered += TextArea_TextEntered;
            Editor.TextArea.TextEntering += TextArea_TextEntering;
            Editor.TextArea.KeyDown += TextArea_KeyDown;
        }

        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }

        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (_intellisenseService == null || !_intellisenseService.IsEnabled) return;

            // If AutoTrigger is enabled, trigger on dot or letter/digit/underscore
            if (_intellisenseService.AutoTriggerEnabled)
            {
                if (e.Text == "." ||
                   (e.Text.Length > 0 && (char.IsLetterOrDigit(e.Text[0]) || e.Text[0] == '_' || e.Text[0] == '@')))
                {
                    // Auto-trigger always tries to show completion.
                    // If window is already open, ShowCompletion returns early.
                    // If window is not open, it uses the current level (which is reset to default on close).
                    ShowCompletion();
                }
            }
        }

        private void TextArea_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;

                // If window is open, we are cycling to the next level
                if (_completionWindow != null)
                {
                    _isCycling = true;
                    _completionWindow.Close();
                    _isCycling = false;

                    // Cycle: Schema -> Standard -> Functional -> Full -> Default
                    if (_currentLevel >= IntellisenseLevel.Full)
                    {
                         // Reset to user's preferred default level
                         if (_intellisenseService != null)
                             _currentLevel = _intellisenseService.DefaultLevel;
                         else
                             _currentLevel = IntellisenseLevel.Schema;
                    }
                    else
                    {
                        _currentLevel++;
                    }

                    _logger?.LogDebug($"Intellisense: Cycling to level {_currentLevel}");
                }
                else
                {
                    // Manual trigger when closed starts at default
                    // Note: If previously closed, it was reset to default.
                    if (_intellisenseService != null)
                         _currentLevel = _intellisenseService.DefaultLevel;
                    else
                         _currentLevel = IntellisenseLevel.Schema;
                }

                ShowCompletion();
            }
        }

        private void ShowCompletion()
        {
            if (_completionWindow != null) return;
            if (_intellisenseService == null) return;
            if (!_intellisenseService.IsEnabled) return;

            if (!_intellisenseService.IsReady)
            {
                _logger?.LogWarning("IntelliSense requested but service is not ready (Schema not loaded).");
                return;
            }

            try
            {
                var text = Editor.Text;
                var offset = Editor.CaretOffset;

                var completions = _intellisenseService.GetCompletions(text, offset, _currentLevel);
                if (completions == null || !completions.Any())
                {
                     _logger?.LogDebug($"Intellisense: No completions found at offset {offset} for level {_currentLevel}.");
                     return;
                }

                _logger?.LogDebug($"Intellisense: Found {completions.Count()} completions (Level: {_currentLevel}).");

                _completionWindow = new CompletionWindow(Editor.TextArea);

                // Calculate the start offset of the word being typed
                int startOffset = offset;
                while (startOffset > 0)
                {
                    char c = text[startOffset - 1];
                    if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '$')
                        startOffset--;
                    else
                        break;
                }
                _completionWindow.StartOffset = startOffset;

                var data = _completionWindow.CompletionList.CompletionData;

                foreach (var item in completions)
                {
                    data.Add(new SqlCompletionData(item.Text, item.Description, item.Priority));
                }

                if (data.Count == 0) return;

                _completionWindow.Show();
                _completionWindow.Closed += (s, args) =>
                {
                    _completionWindow = null;
                    if (!_isCycling)
                    {
                        // Reset to default level on close
                        if (_intellisenseService != null)
                            _currentLevel = _intellisenseService.DefaultLevel;
                        else
                            _currentLevel = IntellisenseLevel.Schema;
                    }
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error showing completion window", ex);
            }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SqlEditor),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty EditorFontFamilyProperty =
            DependencyProperty.Register("EditorFontFamily", typeof(System.Windows.Media.FontFamily), typeof(SqlEditor),
                new FrameworkPropertyMetadata(new System.Windows.Media.FontFamily("Consolas")));

        public System.Windows.Media.FontFamily EditorFontFamily
        {
            get { return (System.Windows.Media.FontFamily)GetValue(EditorFontFamilyProperty); }
            set { SetValue(EditorFontFamilyProperty, value); }
        }

        public static readonly DependencyProperty EditorFontSizeProperty =
            DependencyProperty.Register("EditorFontSize", typeof(double), typeof(SqlEditor),
                new FrameworkPropertyMetadata(14.0));

        public double EditorFontSize
        {
            get { return (double)GetValue(EditorFontSizeProperty); }
            set { SetValue(EditorFontSizeProperty, value); }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SqlEditor control)
            {
                var newText = (string)e.NewValue;
                if (control.Editor.Text != newText)
                {
                    control.Editor.Text = newText ?? "";
                }
            }
        }

        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            if (Text != Editor.Text)
            {
                Text = Editor.Text;
            }
        }

        public new bool Focus()
        {
            return Editor.Focus();
        }

        public void SelectAll()
        {
            Editor.SelectAll();
        }

        private void LoadHighlighting()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();
                var resourceName = resourceNames.FirstOrDefault(s => s.EndsWith("SQL.xshd", StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (var reader = new XmlTextReader(stream))
                            {
                                Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                                _logger?.LogInfo($"Successfully loaded SQL syntax highlighting from '{resourceName}'.");
                            }
                        }
                        else
                        {
                            var msg = $"Failed to get manifest resource stream for '{resourceName}'.";
                            _logger?.LogError(msg);
                            _dialogService?.ShowError(msg, "SQL Editor Error");
                        }
                    }
                }
                else
                {
                    var msg = $"Could not find SQL syntax highlighting resource (SQL.xshd).\nAvailable resources:\n{string.Join("\n", resourceNames)}";
                    _logger?.LogError(msg);
                    _dialogService?.ShowError("Could not find SQL syntax highlighting resource (SQL.xshd). Check logs for details.", "SQL Editor Error");
                }
            }
            catch (Exception ex)
            {
                var msg = $"Failed to load syntax highlighting: {ex.Message}";
                _logger?.LogError(msg, ex);
                System.Diagnostics.Debug.WriteLine(msg);
                _dialogService?.ShowError(msg, "SQL Editor Error");
            }
        }
    }
}
