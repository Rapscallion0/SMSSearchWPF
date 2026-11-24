using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Moq;
using SMS_Search.Data;
using Xunit;

namespace SMSSearch.Tests
{
    public class VirtualGridContextTests
    {
        [Fact]
        public async Task ApplyFilterAsync_UsesParameterizedFilterAndPassesFilterPattern()
        {
            var repoMock = new Mock<IDataRepository>();
            object? capturedParams = null;
            string? capturedFilter = null;

            repoMock
                .Setup(r => r.GetQueryCountAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string?, string?, string, object?, string?, CancellationToken>(
                    (_, _, _, _, _, p, f, _) => { capturedParams = p; capturedFilter = f; })
                .ReturnsAsync(5);

            using var context = new VirtualGridContext(repoMock.Object);
            context.SetConnection("srv", "db", null, null);
            await context.LoadAsync("SELECT * FROM Items", null);

            await context.ApplyFilterAsync("O'Brien", new[] { "Description" });

            Assert.NotNull(capturedFilter);
            Assert.Contains("@_SmsFilterPattern", capturedFilter);
            Assert.DoesNotContain("O'Brien", capturedFilter);

            var dp = Assert.IsType<DynamicParameters>(capturedParams);
            Assert.Equal("%O'Brien%", dp.Get<string>("_SmsFilterPattern"));
        }

        [Fact]
        public async Task PageFetch_RaisesPageLoadedWithoutDataReady_AndNotifiesVirtualRowPropertyChanged()
        {
            var repoMock = new Mock<IDataRepository>();
            repoMock
                .Setup(r => r.GetQueryCountAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            repoMock
                .Setup(r => r.GetQueryPageAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object?[]>
                {
                    new object?[] { "001", "Widget" },
                    new object?[] { "002", "Gadget" }
                });

            var schema = new DataTable();
            schema.Columns.Add("Code", typeof(string));
            schema.Columns.Add("Name", typeof(string));

            using var context = new VirtualGridContext(repoMock.Object);
            int dataReadyCount = 0;
            PageLoadedEventArgs? pageLoadedArgs = null;
            context.DataReady += (_, _) => dataReadyCount++;
            context.PageLoaded += (_, e) => pageLoadedArgs = e;

            context.SetConnection("srv", "db", null, null);
            await context.LoadAsync("SELECT Code, Name FROM Items", null);

            var collection = new VirtualizingCollection(context, schema);
            var row0 = Assert.IsType<VirtualRow>(collection[0]);
            int propertyChangedCount = 0;
            row0.PropertyChanged += (_, _) => propertyChangedCount++;

            await context.WaitForRowAsync(0);
            Assert.Equal(0, dataReadyCount);
            Assert.NotNull(pageLoadedArgs);
            Assert.Equal(0, pageLoadedArgs!.StartIndex);
            Assert.Equal(2, pageLoadedArgs.Count);

            collection.NotifyRangeUpdated(pageLoadedArgs.StartIndex, pageLoadedArgs.Count);
            Assert.Equal(1, propertyChangedCount);
            Assert.Equal(1, row0.DataVersion);
            Assert.Equal("Widget", row0.GetValue(1));
        }

        [Fact]
        public async Task ExportToJsonAndXml_ReusesLoadedTotalCount_AndExcludesHiddenColumns()
        {
            var repoMock = new Mock<IDataRepository>();
            repoMock
                .Setup(r => r.GetQueryCountAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            repoMock
                .Setup(r => r.GetQueryDataReaderAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var dt = new DataTable();
                    dt.Columns.Add("Code", typeof(string));
                    dt.Columns.Add("Secret", typeof(string));
                    dt.Rows.Add("A1", "Hidden1");
                    dt.Rows.Add("B2", "Hidden2");
                    return dt.CreateDataReader();
                });

            using var context = new VirtualGridContext(repoMock.Object);
            context.SetConnection("srv", "db", null, null);
            await context.LoadAsync("SELECT Code, Secret FROM Items", null);

            string jsonPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sms_export_test_{System.Guid.NewGuid():N}.json");
            string xmlPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sms_export_test_{System.Guid.NewGuid():N}.xml");
            try
            {
                var hidden = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Secret" };
                var criteria = new SearchCriteria { Mode = SearchMode.Field, Type = SearchType.Table, Value = "Items" };

                int jsonRows = await context.ExportToJsonAsync(jsonPath, criteria, hiddenColumns: hidden);
                int xmlRows = await context.ExportToXmlAsync(xmlPath, criteria, hiddenColumns: hidden);

                Assert.Equal(2, jsonRows);
                Assert.Equal(2, xmlRows);

                // Count query should only have been invoked once during LoadAsync, not repeated on export
                repoMock.Verify(r => r.GetQueryCountAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                    Times.Once);

                string json = await System.IO.File.ReadAllTextAsync(jsonPath);
                Assert.Contains("\"RecordCount\": 2", json);
                Assert.Contains("\"Code\": \"A1\"", json);
                Assert.DoesNotContain("Secret", json);

                string xml = await System.IO.File.ReadAllTextAsync(xmlPath);
                Assert.Contains("RecordCount=\"2\"", xml);
                Assert.Contains("<Code>A1</Code>", xml);
                Assert.DoesNotContain("Secret", xml);
            }
            finally
            {
                if (System.IO.File.Exists(jsonPath)) System.IO.File.Delete(jsonPath);
                if (System.IO.File.Exists(xmlPath)) System.IO.File.Delete(xmlPath);
            }
        }
    }
}

