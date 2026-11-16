using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels.Settings;
using Xunit;

namespace SMSSearch.Tests
{
    public class SearchSectionViewModelTests
    {
        [Fact]
        public void ValidateColumnList_RequiresMandatoryColumnAndValidIdentifiers()
        {
            Assert.True(SearchSectionViewModel.ValidateColumnList("F1063, F1064", "F1063", out _));
            Assert.False(SearchSectionViewModel.ValidateColumnList("F1064, F1051", "F1063", out var missingErr));
            Assert.Contains("F1063", missingErr);

            Assert.False(SearchSectionViewModel.ValidateColumnList("F1063, F1064; DROP TABLE", "F1063", out _));
            Assert.False(SearchSectionViewModel.ValidateColumnList("F1063, ", "F1063", out _));
            Assert.False(SearchSectionViewModel.ValidateColumnList("", "F1034", out _));
        }

        [Fact]
        public async Task SaveColumnsAsync_MissingRequiredColumn_BlocksSaveAndFlagsError()
        {
            var repo = new Mock<ISettingsRepository>();
            var dataRepo = new Mock<IDataRepository>();
            var connSettings = new Mock<IConnectionSettingsService>();
            var dialog = new Mock<IDialogService>();

            var vm = new SearchSectionViewModel(
                repo.Object,
                dataRepo.Object,
                connSettings.Object,
                new Mock<ILoggerService>().Object,
                dialog.Object);

            vm.FunctionColumns = "F1064, F1051"; // Missing required F1063
            await vm.SaveColumnsAsync();

            Assert.True(vm.HasFunctionError);
            dialog.Verify(d => d.ShowError(It.Is<string>(s => s.Contains("F1063")), "Validation Error"), Times.Once);
            dataRepo.Verify(d => d.GetQuerySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SaveColumnsAsync_UnknownColumn_ReportsSpecificInvalidColumns()
        {
            var repo = new Mock<ISettingsRepository>();
            var dataRepo = new Mock<IDataRepository>();
            var connSettings = new Mock<IConnectionSettingsService>();
            var dialog = new Mock<IDialogService>();

            connSettings.Setup(c => c.GetConnectionSettings())
                .Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));

            var fctTable = new DataTable();
            fctTable.Columns.Add("F1063");
            fctTable.Columns.Add("F1064");

            var tlzTable = new DataTable();
            tlzTable.Columns.Add("F1034");
            tlzTable.Columns.Add("F1039");

            dataRepo.Setup(d => d.GetQuerySchemaAsync(
                    "srv", "db", null, null, "SELECT TOP 0 * FROM [FCT_TAB]", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fctTable);
            dataRepo.Setup(d => d.GetQuerySchemaAsync(
                    "srv", "db", null, null, "SELECT TOP 0 * FROM [TLZ_TAB]", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tlzTable);

            var vm = new SearchSectionViewModel(
                repo.Object,
                dataRepo.Object,
                connSettings.Object,
                new Mock<ILoggerService>().Object,
                dialog.Object);

            vm.FunctionColumns = "F1063, F9999";
            vm.TotalizerColumns = "F1034, F1039";

            await vm.SaveColumnsAsync();

            Assert.True(vm.HasFunctionError);
            Assert.False(vm.HasTotalizerError);
            Assert.Contains("F9999", vm.InvalidFunctionColumns);
            Assert.Empty(vm.InvalidTotalizerColumns);
            dialog.Verify(d => d.ShowError(It.Is<string>(s => s.Contains("F9999")), "Validation Error"), Times.Once);
        }
    }
}

