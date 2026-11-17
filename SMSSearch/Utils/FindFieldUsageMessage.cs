using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public sealed class FindFieldUsageMessage : ValueChangedMessage<string>
    {
        public FindFieldUsageMessage(string fieldIdentifier) : base(fieldIdentifier)
        {
        }
    }
}

