using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    public sealed class SelectionStatsUpdatedMessage : ValueChangedMessage<DetailedSelectionStats>
    {
        public SelectionStatsUpdatedMessage(DetailedSelectionStats stats) : base(stats)
        {
        }
    }
}

