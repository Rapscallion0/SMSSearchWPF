using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public sealed class ShowPanelMessage : ValueChangedMessage<string>
    {
        public ShowPanelMessage(string panelContentId) : base(panelContentId)
        {
        }
    }
}

