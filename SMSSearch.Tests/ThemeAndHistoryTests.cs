using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class ThemeAndHistoryTests
    {
        [Theory]
        [InlineData(AppThemeMode.Dark, true)]
        [InlineData(AppThemeMode.Light, false)]
        public void ThemeService_ResolveIsDark_ReturnsExpectedForExplicitModes(AppThemeMode mode, bool expected)
        {
            Assert.Equal(expected, ThemeService.ResolveIsDark(mode));
        }

        [Fact]
        public void ThemeService_ApplyTheme_UpdatesCurrentModeAndIsDarkTheme()
        {
            var config = new Mock<IConfigService>();
            var logger = new Mock<ILoggerService>();
            var service = new ThemeService(config.Object, logger.Object);

            service.ApplyTheme(AppThemeMode.Dark);
            Assert.Equal(AppThemeMode.Dark, service.CurrentMode);
            Assert.True(service.IsDarkTheme);

            service.ApplyTheme(AppThemeMode.Light);
            Assert.Equal(AppThemeMode.Light, service.CurrentMode);
            Assert.False(service.IsDarkTheme);
        }

        [Fact]
        public void QueryHistoryService_GetHistory_LogsWarningOnMalformedJson()
        {
            var state = new Mock<IStateService>();
            state.Setup(s => s.GetValue("QUERY_HISTORY", "Test")).Returns("{invalid-json");
            var logger = new Mock<ILoggerService>();

            var service = new QueryHistoryService(state.Object, logger.Object);
            var result = service.GetHistory("Test");

            Assert.Empty(result);
            logger.Verify(
                l => l.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()),
                Times.Once);
        }
    }
}

