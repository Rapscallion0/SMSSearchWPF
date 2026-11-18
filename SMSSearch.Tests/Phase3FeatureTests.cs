using System;
using System.Collections.Generic;
using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using Xunit;

namespace SMSSearch.Tests
{
    public class Phase3FeatureTests
    {
        [Fact]
        public void ConnectionSettingsService_MigratesDefaultProfileFromConfig()
        {
            var configValues = new Dictionary<(string, string), string?>
            {
                [(AppSettings.Sections.Connection, AppSettings.Keys.Server)] = "SQL01",
                [(AppSettings.Sections.Connection, AppSettings.Keys.Database)] = "SMS_DB",
                [(AppSettings.Sections.Connection, AppSettings.Keys.WindowsAuth)] = "True"
            };
            var stateValues = new Dictionary<(string, string), string?>();

            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetValue(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns((string s, string k) => configValues.TryGetValue((s, k), out var v) ? v : null);

            var state = new Mock<IStateService>();
            state.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<string>()))
                 .Returns((string sec, string k) => stateValues.TryGetValue((sec, k), out var v) ? v : null);
            state.Setup(s => s.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Callback((string sec, string k, string v) => stateValues[(sec, k)] = v);

            var service = new ConnectionSettingsService(config.Object, state.Object);
            var profiles = service.GetProfiles();

            Assert.Single(profiles);
            Assert.Equal("Default", profiles[0].Name);
            Assert.Equal("SQL01", profiles[0].Server);
            Assert.Equal("SMS_DB", profiles[0].Database);
            Assert.Equal(EnvironmentTag.None, profiles[0].Environment);
        }

        [Fact]
        public void ConnectionSettingsService_SaveActivateAndDeleteProfile_RoundTrips()
        {
            var configValues = new Dictionary<(string, string), string?>();
            var stateValues = new Dictionary<(string, string), string?>();

            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetValue(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns((string s, string k) => configValues.TryGetValue((s, k), out var v) ? v : null);
            config.Setup(c => c.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Callback((string s, string k, string v) => configValues[(s, k)] = v);

            var state = new Mock<IStateService>();
            state.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<string>()))
                 .Returns((string sec, string k) => stateValues.TryGetValue((sec, k), out var v) ? v : null);
            state.Setup(s => s.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Callback((string sec, string k, string v) => stateValues[(sec, k)] = v);

            var service = new ConnectionSettingsService(config.Object, state.Object);
            _ = service.GetProfiles(); // migrates Default

            var prod = service.SaveProfile(new ConnectionProfile
            {
                Name = "Prod Store",
                Server = "PROD-SQL",
                Database = "SMS_LIVE",
                IsWindowsAuth = false,
                User = "sa",
                Environment = EnvironmentTag.Prod
            }, plainPassword: "Secret123!");

            service.ActivateProfile(prod.Id);

            var active = service.GetActiveProfile();
            Assert.NotNull(active);
            Assert.Equal("Prod Store", active!.Name);
            Assert.Equal(EnvironmentTag.Prod, active.Environment);

            var connInfo = service.GetConnectionSettings();
            Assert.Equal("PROD-SQL", connInfo.Server);
            Assert.Equal("SMS_LIVE", connInfo.Database);
            Assert.Equal("Secret123!", connInfo.Password);

            Assert.Equal(2, service.GetProfiles().Count);
            service.DeleteProfile(prod.Id);
            Assert.Single(service.GetProfiles());
            Assert.Equal("Default", service.GetActiveProfile()!.Name);
        }

        [Fact]
        public void ResultTabViewModel_TogglePinAndCloseCommand_WorkAsExpected()
        {
            var repoMock = new Mock<IDataRepository>();
            var gridContext = new VirtualGridContext(repoMock.Object);
            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());

            var resultsVm = new ResultsViewModel(
                gridContext,
                new Mock<ILoggerService>().Object,
                new Mock<IDialogService>().Object,
                new Mock<IConfigService>().Object,
                new Mock<IClipboardService>().Object,
                connSettings.Object,
                repoMock.Object,
                dictMock.Object);

            bool closedCalled = false;
            using var tab = new ResultTabViewModel(resultsVm, _ => closedCalled = true, "ITEM_TAB");

            Assert.Equal("ITEM_TAB", tab.Title);
            Assert.False(tab.IsPinned);

            tab.TogglePinCommand.Execute(null);
            Assert.True(tab.IsPinned);

            tab.CloseCommand.Execute(null);
            Assert.True(closedCalled);
        }

        [Fact]
        public void WorkspaceLayoutService_SavesActiveLayoutAndNamedProfiles()
        {
            var stateValues = new Dictionary<(string, string), string?>();
            var state = new Mock<IStateService>();
            state.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<string>()))
                 .Returns((string sec, string k) => stateValues.TryGetValue((sec, k), out var v) ? v : null);
            state.Setup(s => s.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Callback((string sec, string k, string v) => stateValues[(sec, k)] = v);

            var service = new WorkspaceLayoutService(state.Object, new Mock<ILoggerService>().Object);

            Assert.Null(service.LoadActiveLayout());
            service.SaveActiveLayout("<LayoutRoot>active</LayoutRoot>");
            Assert.Equal("<LayoutRoot>active</LayoutRoot>", service.LoadActiveLayout());

            service.SaveProfile("Multi-Monitor", "<LayoutRoot>multi</LayoutRoot>");
            service.SaveProfile("Compact", "<LayoutRoot>compact</LayoutRoot>");

            var profiles = service.GetProfiles();
            Assert.Equal(2, profiles.Count);
            Assert.Equal("<LayoutRoot>multi</LayoutRoot>", service.GetProfileLayout("multi-monitor"));

            service.DeleteProfile("Compact");
            Assert.Single(service.GetProfiles());
            Assert.Null(service.GetProfileLayout("Compact"));
        }

        [Fact]
        public void WorkspaceDtoSerializer_RoundTripsPolymorphicLayoutRootDto()
        {
            var dto = new AvalonDock.Core.Serialization.Dto.LayoutRootDto
            {
                RootPanel = new AvalonDock.Core.Serialization.Dto.LayoutPanelDto
                {
                    Orientation = "Horizontal",
                    Children =
                    {
                        new AvalonDock.Core.Serialization.Dto.LayoutAnchorablePaneDto
                        {
                            DockWidth = "480",
                            Children =
                            {
                                new AvalonDock.Core.Serialization.Dto.LayoutAnchorableDto
                                {
                                    ContentId = "SearchCriteria",
                                    Title = "Search Criteria"
                                }
                            }
                        },
                        new AvalonDock.Core.Serialization.Dto.LayoutDocumentPaneDto()
                    }
                }
            };

            string xml = WorkspaceDtoSerializer.SerializeDtoToXml(dto);
            var roundTripped = WorkspaceDtoSerializer.DeserializeDtoFromXml(xml);

            Assert.NotNull(roundTripped?.RootPanel);
            Assert.Equal(2, roundTripped!.RootPanel!.Children.Count);
            var anchorPane = Assert.IsType<AvalonDock.Core.Serialization.Dto.LayoutAnchorablePaneDto>(roundTripped.RootPanel.Children[0]);
            Assert.Equal("480", anchorPane.DockWidth);
            Assert.Equal("SearchCriteria", anchorPane.Children[0].ContentId);
        }
    }
}

