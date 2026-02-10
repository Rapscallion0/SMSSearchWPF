using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public class RowNumberVisibilityChangedMessage : ValueChangedMessage<bool>
    {
        public RowNumberVisibilityChangedMessage(bool isVisible) : base(isVisible)
        {
        }
    }
}
