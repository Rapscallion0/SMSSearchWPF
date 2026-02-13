using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Windows.Media;

namespace SMS_Search.Views.Controls
{
    public class SqlCompletionData : ICompletionData
    {
        public SqlCompletionData(string text, string description, double priority)
        {
            Text = text;
            _description = description;
            Priority = priority;
        }

        public ImageSource? Image => null;

        public string Text { get; private set; }

        public object Content => Text;

        private string _description;
        public object Description => _description;

        public double Priority { get; private set; }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
