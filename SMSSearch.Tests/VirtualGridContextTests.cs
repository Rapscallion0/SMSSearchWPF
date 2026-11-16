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
    }
}

