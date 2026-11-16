using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMS_Search.Utils
{
    /// <summary>
    /// Broadcasts when the active application theme changes. Value is true for dark theme, false for light theme.
    /// </summary>
    public class ThemeChangedMessage : ValueChangedMessage<bool>
    {
        public ThemeChangedMessage(bool isDark) : base(isDark)
        {
        }
    }
}

