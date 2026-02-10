using System.Windows;
using System.Runtime.InteropServices;

namespace SMS_Search.Services
{
    public class ClipboardService : IClipboardService
    {
        public void SetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
            }
            catch (ExternalException)
            {
                // Clipboard can be locked by another process
            }
        }

        public string GetText()
        {
            if (Clipboard.ContainsText())
                return Clipboard.GetText();
            return string.Empty;
        }
    }
}
