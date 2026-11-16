using Moq;
using SMS_Search.Services;
using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class ConnectionSettingsServiceTests
    {
        [Fact]
        public void GetConnectionSettings_WindowsAuth_ReturnsEmptyUserAndNullPassword()
        {
            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Server)).Returns("myserver");
            config.Setup(c => c.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.Database)).Returns("mydb");
            config.Setup(c => c.GetValue(AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth)).Returns("true");

            var svc = new ConnectionSettingsService(config.Object);
            var info = svc.GetConnectionSettings();

            Assert.Equal("myserver", info.Server);
            Assert.Equal("mydb", info.Database);
            Assert.True(info.IsWindowsAuth);
            Assert.Equal("", info.User);
            Assert.Null(info.Password);
        }
    }
}

