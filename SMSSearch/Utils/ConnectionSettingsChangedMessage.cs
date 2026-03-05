using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public class ConnectionSettingsChangedMessage : ValueChangedMessage<bool>
    {
        public ConnectionSettingsChangedMessage(bool value) : base(value)
        {
        }
    }
}
