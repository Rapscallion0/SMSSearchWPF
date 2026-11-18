using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SMS_Search.Data;
using SMS_Search.Models;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using SMS_Search.Views;
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
            vm.ColumnOrder.AddRange(new[] { "Description", "Field" });

            await vm.ExecuteSearchAsync(new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Number,
                Value = "1032"
            });

            Assert.Empty(vm.ColumnOrder);

            Assert.Equal(42, vm.TotalRecords);
            Assert.StartsWith("Found 42 records (", vm.StatusText);
            Assert.True(vm.StatusText.EndsWith("ms)") || vm.StatusText.EndsWith("s)"));
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

        [Fact]
        public async Task SortAsync_AndApplyFilterAsync_KeepSortColumnAndSortDirectionInSync()
        {
            var schema = new DataTable();
            schema.Columns.Add("Field", typeof(string));
            schema.Columns.Add("Description", typeof(string));

            var repoMock = new Mock<IDataRepository>();
            repoMock
                .Setup(r => r.GetQuerySchemaAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(schema);
            repoMock
                .Setup(r => r.GetQueryCountAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);

            var gridContext = new VirtualGridContext(repoMock.Object);
            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());

            using var vm = new ResultsViewModel(
                gridContext,
                new Mock<ILoggerService>().Object,
                new Mock<IDialogService>().Object,
                new Mock<IConfigService>().Object,
                new Mock<IClipboardService>().Object,
                connSettings.Object,
                repoMock.Object,
                dictMock.Object);

            await vm.ExecuteSearchAsync(new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Number,
                Value = "1032"
            });

            Assert.Null(vm.SortColumn);
            Assert.Null(vm.SortDirection);

            await vm.SortAsync("Field");
            Assert.Equal("Field", vm.SortColumn);
            Assert.Equal(ListSortDirection.Ascending, vm.SortDirection);

            await vm.SortAsync("Field");
            Assert.Equal("Field", vm.SortColumn);
            Assert.Equal(ListSortDirection.Descending, vm.SortDirection);

            await vm.ApplyFilterAsync("test");
            Assert.Equal("Field", vm.SortColumn);
            Assert.Equal(ListSortDirection.Descending, vm.SortDirection);

            await vm.SortAsync("Description");
            Assert.Equal("Description", vm.SortColumn);
            Assert.Equal(ListSortDirection.Ascending, vm.SortDirection);
        }

        [Fact]
        public void CopyInspectorValueCommand_EmptyValue_DoesNotCopyAndShowsInfoToast()
        {
            var repoMock = new Mock<IDataRepository>();
            var gridContext = new VirtualGridContext(repoMock.Object);
            var clipboard = new Mock<IClipboardService>();
            var dialog = new Mock<IDialogService>();
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());

            using var vm = new ResultsViewModel(
                gridContext,
                new Mock<ILoggerService>().Object,
                dialog.Object,
                new Mock<IConfigService>().Object,
                clipboard.Object,
                new Mock<IConnectionSettingsService>().Object,
                repoMock.Object,
                dictMock.Object);

            var emptyField = new RowInspectorField("1", "F100", "Empty Field", "");
            vm.CopyInspectorValueCommand.Execute(emptyField);

            clipboard.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
            dialog.Verify(d => d.ShowToast("No value to copy for F100", "Inspector", ToastType.Info), Times.Once);
        }

        [Fact]
        public void CopyInspectorMultiRowCommands_CopyOrderedContentAndHandleEmptyValues()
        {
            var repoMock = new Mock<IDataRepository>();
            var gridContext = new VirtualGridContext(repoMock.Object);
            var clipboard = new Mock<IClipboardService>();
            var dialog = new Mock<IDialogService>();
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());

            using var vm = new ResultsViewModel(
                gridContext,
                new Mock<ILoggerService>().Object,
                dialog.Object,
                new Mock<IConfigService>().Object,
                clipboard.Object,
                new Mock<IConnectionSettingsService>().Object,
                repoMock.Object,
                dictMock.Object);

            var f1 = new RowInspectorField("1", "F01", "Item Code", "12345");
            var f2 = new RowInspectorField("2", "F02", "Notes", "");
            var f3 = new RowInspectorField("3", "F03", "Price", "9.99");
            vm.InspectorFields.Add(f1);
            vm.InspectorFields.Add(f2);
            vm.InspectorFields.Add(f3);

            // Pass out of order to verify InspectorFields ordering is preserved
            var selected = new List<RowInspectorField> { f3, f1, f2 };

            vm.CopyInspectorValuesCommand.Execute(selected);
            clipboard.Verify(c => c.SetText($"12345{System.Environment.NewLine}9.99"), Times.Once);
            dialog.Verify(d => d.ShowToast("Copied 2 values", "Inspector", ToastType.Success), Times.Once);

            vm.CopyInspectorColumnsCommand.Execute(selected);
            clipboard.Verify(c => c.SetText($"F01{System.Environment.NewLine}F02{System.Environment.NewLine}F03"), Times.Once);

            vm.CopyInspectorDescriptionsCommand.Execute(selected);
            clipboard.Verify(c => c.SetText($"Item Code{System.Environment.NewLine}Notes{System.Environment.NewLine}Price"), Times.Once);

            vm.CopyInspectorRowsCommand.Execute(selected);
            clipboard.Verify(c => c.SetText($"F01\tItem Code\t12345{System.Environment.NewLine}F02\tNotes\t{System.Environment.NewLine}F03\tPrice\t9.99"), Times.Once);
            dialog.Verify(d => d.ShowToast("Copied 3 rows", "Inspector", ToastType.Success), Times.Once);

            // Empty values only
            clipboard.Invocations.Clear();
            vm.CopyInspectorValuesCommand.Execute(new List<RowInspectorField> { f2 });
            clipboard.Verify(c => c.SetText(It.IsAny<string>()), Times.Never);
            dialog.Verify(d => d.ShowToast("No values to copy", "Inspector", ToastType.Info), Times.Once);
        }
    }
}

