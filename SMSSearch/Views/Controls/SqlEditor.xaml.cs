using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.IO;
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
            this.Height = 200;
        }

        private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (double.IsNaN(this.Height)) this.Height = this.ActualHeight;
            double newHeight = this.Height + e.VerticalChange;
            if (newHeight < 100) newHeight = 100;
            this.Height = newHeight;
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
                var uri = new Uri("pack://application:,,,/SMSSearch;component/Resources/SQL.xshd", UriKind.Absolute);
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    using (var reader = new XmlTextReader(streamInfo.Stream))
                    {
                        Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to plain text if failed
            }
        }
    }
}
