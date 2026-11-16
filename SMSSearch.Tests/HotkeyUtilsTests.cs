using System.Windows.Input;
using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class HotkeyUtilsTests
    {
        [Fact]
        public void Parse_EmptyInput_ReturnsNone()
        {
            var (key, modifiers) = HotkeyUtils.Parse("");

            Assert.Equal(Key.None, key);
            Assert.Equal(ModifierKeys.None, modifiers);
        }

        [Fact]
        public void Parse_CtrlShiftF_ReturnsExpectedKeyAndModifiers()
        {
            var (key, modifiers) = HotkeyUtils.Parse("Ctrl+Shift+F");

            Assert.Equal(Key.F, key);
            Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, modifiers);
        }

        [Fact]
        public void GetFriendlyKeyName_Oem3_ReturnsBacktick()
        {
            Assert.Equal("`", HotkeyUtils.GetFriendlyKeyName(Key.Oem3));
        }
    }
}

