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
using System.Xml;

namespace SMS_Search.Views.Controls
{
    public partial class SqlEditor : System.Windows.Controls.UserControl
    {
        public SqlEditor()
        {
            InitializeComponent();
            LoadHighlighting();
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
            ILoggerService? logger = null;
            try
            {
                if (Application.Current is App app && app.Services != null)
                {
                    logger = app.Services.GetService<ILoggerService>();
                }
            }
            catch
            {
                // Ignore if we can't get logger (e.g. design time)
            }

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
                                logger?.LogInfo($"Successfully loaded SQL syntax highlighting from '{resourceName}'.");
                            }
                        }
                        else
                        {
                            var msg = $"Failed to get manifest resource stream for '{resourceName}'.";
                            logger?.LogError(msg);
                            MessageBox.Show(msg, "SQL Editor Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    var msg = $"Could not find SQL syntax highlighting resource (SQL.xshd).\nAvailable resources:\n{string.Join("\n", resourceNames)}";
                    logger?.LogError(msg);
                    MessageBox.Show("Could not find SQL syntax highlighting resource (SQL.xshd). Check logs for details.", "SQL Editor Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                var msg = $"Failed to load syntax highlighting: {ex.Message}";
                logger?.LogError(msg, ex);
                System.Diagnostics.Debug.WriteLine(msg);
                MessageBox.Show(msg, "SQL Editor Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
