using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public class HighlightConfigurationChangedMessage : ValueChangedMessage<(bool IsHighlightEnabled, string HighlightColor)>
    {
        public HighlightConfigurationChangedMessage(bool isHighlightEnabled, string highlightColor) : base((isHighlightEnabled, highlightColor))
        {
        }
    }
}
