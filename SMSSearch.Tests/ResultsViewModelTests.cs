using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using Xunit;

namespace SMSSearch.Tests
{
    public class ResultsViewModelTests
    {
        [Fact]
        public async Task ExecuteSearchAsync_LoadsCountAndSetsStatusText()
        {
            var repoMock = new Mock<IDataRepository>();
            repoMock
                .Setup(r => r.GetQuerySchemaAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DataTable());
            repoMock
                .Setup(r => r.GetQueryCountAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(42);

            var gridContext = new VirtualGridContext(repoMock.Object);
            var logger = new Mock<ILoggerService>();
            var dialog = new Mock<IDialogService>();
            var config = new Mock<IConfigService>();
            var clipboard = new Mock<IClipboardService>();
            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());

            using var vm = new ResultsViewModel(gridContext, logger.Object, dialog.Object, config.Object, clipboard.Object, connSettings.Object, repoMock.Object, dictMock.Object);

            await vm.ExecuteSearchAsync(new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Number,
                Value = "1032"
            });

            Assert.Equal(42, vm.TotalRecords);
            Assert.Equal("Found 42 records", vm.StatusText);
        }

        [Fact]
        public void Dispose_CanBeCalledSafelyMultipleTimes()
        {
            var repoMock = new Mock<IDataRepository>();
            var gridContext = new VirtualGridContext(repoMock.Object);
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());
            var vm = new ResultsViewModel(
                gridContext,
                new Mock<ILoggerService>().Object,
                new Mock<IDialogService>().Object,
                new Mock<IConfigService>().Object,
                new Mock<IClipboardService>().Object,
                new Mock<IConnectionSettingsService>().Object,
                repoMock.Object,
                dictMock.Object);

            vm.Dispose();
            vm.Dispose();
        }

        [Fact]
        public async Task ExecuteSearchAsync_DbOnlyModeWithoutConnection_ShowsWarning()
        {
            var repoMock = new Mock<IDataRepository>();
            var gridContext = new VirtualGridContext(repoMock.Object);
            var dialog = new Mock<IDialogService>();
            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("", "", "", null, true));
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());

            using var vm = new ResultsViewModel(
                gridContext,
                new Mock<ILoggerService>().Object,
                dialog.Object,
                new Mock<IConfigService>().Object,
                new Mock<IClipboardService>().Object,
                connSettings.Object,
                repoMock.Object,
                dictMock.Object);

            await vm.ExecuteSearchAsync(new SearchCriteria
            {
                Mode = SearchMode.Function,
                Value = "100"
            });

            Assert.Equal("Connection required", vm.StatusText);
            dialog.Verify(d => d.ShowWarning(It.IsAny<string>(), "Connection Required"), Times.Once);
        }
    }
}

