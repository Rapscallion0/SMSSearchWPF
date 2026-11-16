using Microsoft.Data.SqlClient;
using Moq;
using SMS_Search.Data;
using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class DataRepositoryTests
    {
        [Fact]
        public void GetConnectionString_WindowsAuth_SetsIntegratedSecurity()
        {
            var repo = new DataRepository(new Mock<ILoggerService>().Object);

            var connStr = repo.GetConnectionString("localhost", "TestDb", null, null);
            var parsed = new SqlConnectionStringBuilder(connStr);

            Assert.Equal("localhost", parsed.DataSource);
            Assert.Equal("TestDb", parsed.InitialCatalog);
            Assert.True(parsed.IntegratedSecurity);
            Assert.False(parsed.Pooling);
            Assert.False(parsed.Encrypt);
        }

        [Fact]
        public void GetConnectionString_SqlAuthWithSpecialCharacters_EscapesSafely()
        {
            var repo = new DataRepository(new Mock<ILoggerService>().Object);
            const string trickyPassword = "p@ss;word=1\"2";

            var connStr = repo.GetConnectionString("srv\\inst", "Db;1", "sa", trickyPassword);
            var parsed = new SqlConnectionStringBuilder(connStr);

            Assert.Equal("srv\\inst", parsed.DataSource);
            Assert.Equal("Db;1", parsed.InitialCatalog);
            Assert.Equal("sa", parsed.UserID);
            Assert.Equal(trickyPassword, parsed.Password);
            Assert.False(parsed.IntegratedSecurity);
        }
    }
}

