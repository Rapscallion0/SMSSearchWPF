using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public class SqlFontSettingsChangedMessage : ValueChangedMessage<(string Family, int Size)>
    {
        public SqlFontSettingsChangedMessage((string Family, int Size) fontSettings) : base(fontSettings)
        {
        }
    }
}
