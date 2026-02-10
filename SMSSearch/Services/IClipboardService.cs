namespace SMS_Search.Services
{
    public interface IClipboardService
    {
        void SetText(string text);
        string GetText();
    }
}
