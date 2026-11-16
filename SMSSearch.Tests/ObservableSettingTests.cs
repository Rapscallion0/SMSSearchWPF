using System.Threading.Tasks;
using Moq;
using SMS_Search.Services;
using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class ObservableSettingTests
    {
        [Fact]
        public async Task ValueChanged_DebouncesAndSavesToRepository()
        {
            var repoMock = new Mock<ISettingsRepository>();
            repoMock
                .Setup(r => r.SaveAsync("SEC", "KEY", "updated"))
                .Returns(Task.CompletedTask);

            using var setting = new ObservableSetting<string>(repoMock.Object, "SEC", "KEY", "initial");
            setting.Value = "intermediate";
            setting.Value = "updated";

            await Task.Delay(750);

            repoMock.Verify(r => r.SaveAsync("SEC", "KEY", "intermediate"), Times.Never);
            repoMock.Verify(r => r.SaveAsync("SEC", "KEY", "updated"), Times.Once);
        }

        [Fact]
        public async Task Dispose_CancelsPendingDebouncedSave()
        {
            var repoMock = new Mock<ISettingsRepository>();
            var setting = new ObservableSetting<string>(repoMock.Object, "SEC", "KEY", "initial");

            setting.Value = "changed";
            setting.Dispose();

            await Task.Delay(700);

            repoMock.Verify(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}

