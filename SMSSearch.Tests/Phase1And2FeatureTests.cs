using System;
using System.Collections.Generic;
using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class Phase1And2FeatureTests
    {
        [Fact]
        public void MarkdownTableFormatter_FormatsAndEscapesCells()
        {
            var headers = new[] { "Col A", "Col|B" };
            var rows = new List<IReadOnlyList<string?>>
            {
                new[] { "val1", "a|b\nc" },
                new[] { "val2", null }
            };

            string result = MarkdownTableFormatter.Format(headers, rows);

            Assert.Contains("| Col A | Col\\|B |", result);
            Assert.Contains("| --- | --- |", result);
            Assert.Contains("| val1 | a\\|b<br/>c |", result);
            Assert.Contains("| val2 |  |", result);
        }

        [Fact]
        public void MarkdownTableFormatter_EmptyHeaders_ReturnsEmpty()
        {
            string result = MarkdownTableFormatter.Format(Array.Empty<string>(), Array.Empty<IReadOnlyList<string?>>());
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void SelectionStatsCalculator_SingleValue_ReturnsEmptySummary()
        {
            var stats = SelectionStatsCalculator.Calculate(new object?[] { 42 });
            Assert.Equal(1, stats.SelectedCount);
            Assert.Equal(string.Empty, stats.SummaryText);
        }

        [Fact]
        public void SelectionStatsCalculator_NumericValues_ComputesSumAvgMinMax()
        {
            var stats = SelectionStatsCalculator.Calculate(new object?[] { 10, "20", 30m, 20 });

            Assert.Equal(4, stats.SelectedCount);
            Assert.Equal(3, stats.DistinctCount);
            Assert.Equal(4, stats.NumericCount);
            Assert.Equal(80m, stats.Sum);
            Assert.Equal(20m, stats.Average);
            Assert.Equal(10m, stats.Min);
            Assert.Equal(30m, stats.Max);
            Assert.Contains("Selected: 4", stats.SummaryText);
            Assert.Contains("Distinct: 3", stats.SummaryText);
            Assert.Contains("Sum: 80", stats.SummaryText);
        }

        [Fact]
        public void SelectionStatsCalculator_TextOnly_OmitsNumericStats()
        {
            var stats = SelectionStatsCalculator.Calculate(new object?[] { "A", "B", "A" });

            Assert.Equal(3, stats.SelectedCount);
            Assert.Equal(2, stats.DistinctCount);
            Assert.Equal(0, stats.NumericCount);
            Assert.Null(stats.Sum);
            Assert.DoesNotContain("Sum:", stats.SummaryText);
            Assert.Contains("Selected: 3 | Distinct: 2", stats.SummaryText);
        }

        [Fact]
        public void SavedQueriesService_SaveGetDelete_RoundTripsThroughStateService()
        {
            string? storedJson = null;
            var state = new Mock<IStateService>();
            state.Setup(s => s.GetValue("SAVED_QUERIES", "ITEMS")).Returns(() => storedJson);
            state.Setup(s => s.SetValue("SAVED_QUERIES", "ITEMS", It.IsAny<string>()))
                 .Callback<string, string, string>((_, _, val) => storedJson = val);

            var logger = new Mock<ILoggerService>();
            var service = new SavedQueriesService(state.Object, logger.Object);

            var item = service.Save("Top Items", "Inventory", "SELECT TOP 10 * FROM ITEM_TAB");
            var all = service.GetAll();

            Assert.Single(all);
            Assert.Equal("Top Items", all[0].Name);
            Assert.Equal("Inventory", all[0].Category);

            // Update same name+category overwrites SQL instead of duplicating
            service.Save("Top Items", "Inventory", "SELECT TOP 20 * FROM ITEM_TAB");
            all = service.GetAll();
            Assert.Single(all);
            Assert.Equal("SELECT TOP 20 * FROM ITEM_TAB", all[0].Sql);

            service.Delete(item.Id);
            Assert.Empty(service.GetAll());
        }

        [Fact]
        public void SavedQueriesService_GetCategories_AlwaysIncludesGeneralAndDistinctCategories()
        {
            string? storedJson = null;
            var state = new Mock<IStateService>();
            state.Setup(s => s.GetValue("SAVED_QUERIES", "ITEMS")).Returns(() => storedJson);
            state.Setup(s => s.SetValue("SAVED_QUERIES", "ITEMS", It.IsAny<string>()))
                 .Callback<string, string, string>((_, _, val) => storedJson = val);

            var service = new SavedQueriesService(state.Object, new Mock<ILoggerService>().Object);
            service.Save("Q1", "POS", "SELECT 1");
            service.Save("Q2", "Inventory", "SELECT 2");
            service.Save("Q3", "pos", "SELECT 3");

            var categories = service.GetCategories();
            Assert.Contains("General", categories);
            Assert.Contains(categories, c => c.Equals("POS", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Inventory", categories);
            Assert.Equal(3, categories.Count);
        }

        [Fact]
        public void SavedQueriesService_SuggestName_StripsCommentsAndTruncates()
        {
            string sql = "-- Header comment\r\nSELECT  F01,  F02\r\nFROM ITEM_TAB WHERE F01 = '12345678901234567890'";
            string suggested = SavedQueriesService.SuggestName(sql, maxLength: 35);

            Assert.DoesNotContain("--", suggested);
            Assert.EndsWith("...", suggested);
            Assert.True(suggested.Length <= 35);
            Assert.StartsWith("SELECT F01, F02 FROM ITEM_TAB", suggested);
        }

        [Fact]
        public void SelectionStatsCalculator_CalculateDetailed_ComputesSpreadAndFrequencies()
        {
            var stats = SelectionStatsCalculator.CalculateDetailed(new object?[] { 10, 20, 20, -5, 0, null, "" });

            Assert.Equal(7, stats.SelectedCount);
            Assert.Equal(5, stats.NonEmptyCount);
            Assert.Equal(2, stats.NullOrEmptyCount);
            Assert.Equal(5, stats.DistinctCount);
            Assert.Equal(5, stats.NumericCount);
            Assert.Equal(45m, stats.Sum);
            Assert.Equal(9m, stats.Average);
            Assert.Equal(10m, stats.Median);
            Assert.Equal(-5m, stats.Min);
            Assert.Equal(20m, stats.Max);
            Assert.Equal(25m, stats.Range);
            Assert.Equal(1, stats.ZeroCount);
            Assert.Equal(1, stats.NegativeCount);
            Assert.NotNull(stats.StdDev);
            Assert.Equal(2, stats.TopFrequencies[0].Count);
        }

        [Fact]
        public async System.Threading.Tasks.Task ResultsViewModel_SetInspectedRow_DoesNotModifySelectedRow()
        {
            var repoMock = new Mock<IDataRepository>();
            var dt = new System.Data.DataTable();
            dt.Columns.Add("F1000", typeof(string));
            dt.Columns.Add("F1001", typeof(string));
            dt.Rows.Add("001", "Widget");

            repoMock.Setup(r => r.GetQuerySchemaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(dt);
            repoMock.Setup(r => r.GetQueryCountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(1);
            repoMock.Setup(r => r.GetQueryPageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(new List<object?[]> { new object?[] { "001", "Widget" } });
            repoMock.Setup(r => r.GetColumnDescriptionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(new List<string> { "Item Code", "Description" });

            var gridContext = new VirtualGridContext(repoMock.Object);
            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());

            using var vm = new SMS_Search.ViewModels.ResultsViewModel(
                gridContext,
                new Mock<ILoggerService>().Object,
                new Mock<IDialogService>().Object,
                new Mock<IConfigService>().Object,
                new Mock<IClipboardService>().Object,
                connSettings.Object,
                repoMock.Object,
                dictMock.Object);

            await vm.ExecuteSearchAsync(new SearchCriteria { Mode = SearchMode.Field, Type = SearchType.Table, Value = "ITEM_TAB", ShowFields = false });
            gridContext.GetValue(0, 0);
            await System.Threading.Tasks.Task.Delay(100);

            var row = Assert.IsType<VirtualRow>(vm.SearchResults![0]);
            vm.IsInspectorOpen = true;
            vm.SetInspectedRow(row);
            await System.Threading.Tasks.Task.Delay(100);

            Assert.Null(vm.SelectedRow);
            Assert.Equal(2, vm.InspectorFields.Count);
            Assert.Equal("001", vm.InspectorFields[0].Value);
            Assert.Equal("Widget", vm.InspectorFields[1].Value);
        }

        [Fact]
        public void SavedQueriesService_DuplicateDetection_FindsByNameCategoryAndNormalizedSql()
        {
            string? storedJson = null;
            var state = new Mock<IStateService>();
            state.Setup(s => s.GetValue("SAVED_QUERIES", "ITEMS")).Returns(() => storedJson);
            state.Setup(s => s.SetValue("SAVED_QUERIES", "ITEMS", It.IsAny<string>()))
                 .Callback<string, string, string>((_, _, val) => storedJson = val);

            var service = new SavedQueriesService(state.Object, new Mock<ILoggerService>().Object);
            service.Save("Item Lookup", "Inventory", "SELECT  F01,\r\n  F02\r\nFROM ITEM_TAB");

            var nameMatch = service.FindDuplicateByNameAndCategory("item lookup", "INVENTORY");
            Assert.NotNull(nameMatch);
            Assert.Equal("Item Lookup", nameMatch!.Name);

            var sqlMatches = service.FindDuplicatesBySql("select f01, f02 from item_tab");
            Assert.Single(sqlMatches);
            Assert.Equal("Item Lookup", sqlMatches[0].Name);

            Assert.Empty(service.FindDuplicatesBySql("SELECT * FROM POS_TAB"));
        }
    }
}

