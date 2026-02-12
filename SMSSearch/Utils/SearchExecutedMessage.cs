using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public class SearchExecutedMessage : ValueChangedMessage<bool>
    {
        public SearchExecutedMessage(bool value) : base(value)
        {
        }
    }
}
