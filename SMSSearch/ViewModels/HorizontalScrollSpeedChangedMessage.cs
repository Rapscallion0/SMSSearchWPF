namespace SMS_Search.ViewModels
{
    public class HorizontalScrollSpeedChangedMessage
    {
        public int Speed { get; }

        public HorizontalScrollSpeedChangedMessage(int speed)
        {
            Speed = speed;
        }
    }
}
