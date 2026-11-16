using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using Xunit;

namespace SMSSearch.Tests
{
    public class SearchViewModelTests
    {
        [Fact]
        public void Constructor_InitializesDefaultsAndDisposesCleanly()
        {
            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontFamily)).Returns("Consolas");
            config.Setup(c => c.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontSize)).Returns("12");

            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));

            using var vm = new SearchViewModel(
                new Mock<IDataRepository>().Object,
                new Mock<IDialogService>().Object,
                config.Object,
                new Mock<ILoggerService>().Object,
                new Mock<IQueryHistoryService>().Object,
                new Mock<IClipboardService>().Object,
                new Mock<IIntellisenseService>().Object,
                connSettings.Object);

            Assert.Equal("Consolas", vm.SqlFontFamily);
            Assert.Equal(12, vm.SqlFontSize);
        }
    }
}

