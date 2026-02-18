using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public class FocusTableMessage : ValueChangedMessage<bool>
    {
        public FocusTableMessage(bool value) : base(value)
        {
        }
    }
}
