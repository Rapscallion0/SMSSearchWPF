using System.Windows.Forms;
using System.Windows.Input;
using SMS_Search.Services;
using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class TrayIconServiceTests
    {
        [Fact]
        public void BuildTooltipText_BackgroundWithHotkey_IncludesHotkey()
        {
            string tooltip = TrayIconService.BuildTooltipText(TrayIconState.Background, "Ctrl + Shift + `");
            Assert.Equal("SMS Search — Background (Ctrl + Shift + `)", tooltip);
        }

        [Fact]
        public void BuildTooltipText_BackgroundWithoutHotkey_ShowsRunningInTray()
        {
            string tooltip = TrayIconService.BuildTooltipText(TrayIconState.Background, null);
            Assert.Equal("SMS Search — Running in tray", tooltip);
        }

        [Fact]
        public void BuildTooltipText_MinimizedWithAndWithoutHotkey()
        {
            Assert.Equal("SMS Search — Minimized (Ctrl + Shift + `)", TrayIconService.BuildTooltipText(TrayIconState.Minimized, "Ctrl + Shift + `"));
            Assert.Equal("SMS Search (Minimized)", TrayIconService.BuildTooltipText(TrayIconState.Minimized, null));
        }

        [Fact]
        public void BuildTooltipText_OpenWithAndWithoutHotkey()
        {
            Assert.Equal("SMS Search (Ctrl + Shift + `)", TrayIconService.BuildTooltipText(TrayIconState.Open, "Ctrl + Shift + `"));
            Assert.Equal("SMS Search", TrayIconService.BuildTooltipText(TrayIconState.Open, ""));
        }

        [Fact]
        public void BuildTooltipText_WhenExceedsMaxLength_TruncatesSafely()
        {
            string longHotkey = new string('X', 100);
            string tooltip = TrayIconService.BuildTooltipText(TrayIconState.Background, longHotkey);
            Assert.Equal(TrayIconService.MaxTooltipLength, tooltip.Length);
            Assert.EndsWith("...", tooltip);
        }

        [Fact]
        public void BuildOpenMenuText_FormatsHotkeyWhenPresent()
        {
            Assert.Equal("Open SMS Search (Ctrl + Shift + `)", TrayIconService.BuildOpenMenuText("Ctrl + Shift + `"));
            Assert.Equal("Open SMS Search", TrayIconService.BuildOpenMenuText(""));
            Assert.Equal("Open SMS Search", TrayIconService.BuildOpenMenuText(null));
        }

        [Fact]
        public void DefaultHotkey_ParsesToCtrlShiftBacktick()
        {
            var (key, modifiers) = HotkeyUtils.Parse(AppSettings.Defaults.Hotkey);
            Assert.Equal(Key.Oem3, key);
            Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, modifiers);
            Assert.Equal("Ctrl + Shift + `", HotkeyUtils.GetFriendlyString(key, modifiers));
        }

        [Theory]
        [InlineData(Keys.None, false)]
        [InlineData(Keys.Control, false)]
        [InlineData(Keys.Shift, false)]
        [InlineData(Keys.Alt, false)]
        [InlineData(Keys.Control | Keys.Shift, true)]
        [InlineData(Keys.Control | Keys.Shift | Keys.Alt, true)]
        public void ShouldShowEncryptionUtility_RequiresCtrlAndShift(Keys modifiers, bool expected)
        {
            Assert.Equal(expected, TrayIconService.ShouldShowEncryptionUtility(modifiers));
        }
    }
}
