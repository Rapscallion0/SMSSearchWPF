using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using Xunit;

namespace SMSSearch.Tests
{
    public class SearchViewModelTests
    {
        [Fact]
        public void Constructor_InitializesDefaultsAndDisposesCleanly()
        {
            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontFamily)).Returns("Consolas");
            config.Setup(c => c.GetValue(AppSettings.Sections.Editor, AppSettings.Keys.SqlFontSize)).Returns("12");

            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));

            using var vm = new SearchViewModel(
                new Mock<IDataRepository>().Object,
                new Mock<IDialogService>().Object,
                config.Object,
                new Mock<ILoggerService>().Object,
                new Mock<IQueryHistoryService>().Object,
                new Mock<IClipboardService>().Object,
                new Mock<IIntellisenseService>().Object,
                connSettings.Object);

            Assert.Equal("Consolas", vm.SqlFontFamily);
            Assert.Equal(12, vm.SqlFontSize);
        }

        private static SearchViewModel CreateViewModel()
        {
            var config = new Mock<IConfigService>();
            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));

            return new SearchViewModel(
                new Mock<IDataRepository>().Object,
                new Mock<IDialogService>().Object,
                config.Object,
                new Mock<ILoggerService>().Object,
                new Mock<IQueryHistoryService>().Object,
                new Mock<IClipboardService>().Object,
                new Mock<IIntellisenseService>().Object,
                connSettings.Object);
        }

        [Fact]
        public void UnifiedProperties_PreserveSeparateInputsPerMode()
        {
            using var vm = CreateViewModel();

            vm.SelectedMode = SearchMode.Function;
            vm.ActiveNumberText = "101";

            vm.SelectedMode = SearchMode.Totalizer;
            Assert.Equal("", vm.ActiveNumberText);
            vm.ActiveNumberText = "202";

            vm.SelectedMode = SearchMode.Field;
            Assert.Equal("F", vm.NumberPrefixText);
            vm.ActiveNumberText = "1032";

            vm.SelectedMode = SearchMode.Function;
            Assert.Equal("101", vm.ActiveNumberText);
            Assert.Equal("", vm.NumberPrefixText);
        }

        [Fact]
        public void MultiClause_NestedGroupAndUngroup_BuildsNestedAst()
        {
            using var vm = CreateViewModel();
            vm.SelectedMode = SearchMode.Field;
            vm.ActiveDescriptionText = "Cash";
            vm.ToggleMultiClauseCommand.Execute(null);

            Assert.True(vm.IsMultiClauseMode);
            Assert.Equal(2, vm.Clauses.Count);

            vm.AddClauseCommand.Execute(null);
            Assert.Equal(3, vm.Clauses.Count);

            // Group rows 1 and 2
            vm.Clauses[1].IsSelectedForGroup = true;
            vm.Clauses[2].IsSelectedForGroup = true;
            Assert.True(vm.GroupSelectedClausesCommand.CanExecute(null));
            vm.GroupSelectedClausesCommand.Execute(null);

            // Group all 3 rows (outer group around row 0 + inner group [1..2])
            vm.Clauses[0].IsSelectedForGroup = true;
            vm.Clauses[1].IsSelectedForGroup = true;
            vm.Clauses[2].IsSelectedForGroup = true;
            Assert.True(vm.GroupSelectedClausesCommand.CanExecute(null));
            vm.GroupSelectedClausesCommand.Execute(null);

            var root = vm.BuildRootGroup();
            Assert.Single(root.Children);
            var outer = Assert.IsType<SearchClauseGroup>(root.Children[0]);
            Assert.Equal(2, outer.Children.Count);
            Assert.IsType<SearchClause>(outer.Children[0]);
            Assert.IsType<SearchClauseGroup>(outer.Children[1]);

            // Ungroup innermost group containing row 1
            vm.Clauses[1].IsSelectedForGroup = true;
            Assert.True(vm.UngroupSelectedClausesCommand.CanExecute(null));
            vm.UngroupSelectedClausesCommand.Execute(null);

            var rootAfterUngroup = vm.BuildRootGroup();
            var remainingOuter = Assert.IsType<SearchClauseGroup>(rootAfterUngroup.Children[0]);
            Assert.Equal(3, remainingOuter.Children.Count);
        }

        [Fact]
        public void RecordQueryMultiClause_SetsShowFieldsFalseAndPreservesColumnName()
        {
            using var vm = CreateViewModel();
            vm.SelectedMode = SearchMode.Field;
            vm.SelectedTable = "SAL_REG";
            vm.ShowRecords = true;
            vm.ToggleMultiClauseCommand.Execute(null);

            Assert.True(vm.IsRecordMultiClauseMode);
            Assert.True(vm.Clauses[0].IsRecordQueryMode);

            vm.Clauses[0].ColumnName = "F1063";
            vm.Clauses[0].Operator = SearchComparisonOperator.Equals;
            vm.Clauses[0].Value = "101";

            var criteria = vm.GetSearchCriteria();

            Assert.Equal(SearchMode.Field, criteria.Mode);
            Assert.Equal(SearchType.Table, criteria.Type);
            Assert.False(criteria.ShowFields);
            Assert.Equal("SAL_REG", criteria.Value);
            Assert.NotNull(criteria.RootGroup);
            var firstClause = Assert.IsType<SearchClause>(criteria.RootGroup!.Children[0]);
            Assert.Equal("F1063", firstClause.ColumnName);
            Assert.Equal("101", firstClause.Value);
        }

        [Fact]
        public void ExportAndLoadAdvancedQueryDefinition_RoundTripsClausesGroupsAndRecordMode()
        {
            using var vm = CreateViewModel();
            vm.SelectedMode = SearchMode.Field;
            vm.SelectedTable = "SAL_REG";
            vm.ShowRecords = true;
            vm.ToggleMultiClauseCommand.Execute(null);

            vm.AddClauseCommand.Execute(null);
            vm.Clauses[0].ColumnName = "F1032";
            vm.Clauses[0].Operator = SearchComparisonOperator.Equals;
            vm.Clauses[0].Value = "123";

            vm.Clauses[1].LogicalOperator = SearchLogicalOperator.And;
            vm.Clauses[1].ColumnName = "F1063";
            vm.Clauses[1].Operator = SearchComparisonOperator.Equals;
            vm.Clauses[1].Value = "101";

            vm.Clauses[2].LogicalOperator = SearchLogicalOperator.Or;
            vm.Clauses[2].ColumnName = "F1064";
            vm.Clauses[2].Operator = SearchComparisonOperator.Contains;
            vm.Clauses[2].Value = "Void";

            vm.Clauses[1].IsSelectedForGroup = true;
            vm.Clauses[2].IsSelectedForGroup = true;
            vm.GroupSelectedClausesCommand.Execute(null);

            var exported = vm.ExportAdvancedQueryDefinition();

            using var vm2 = CreateViewModel();
            vm2.LoadAdvancedQueryDefinition(exported);

            Assert.True(vm2.IsMultiClauseMode);
            Assert.True(vm2.IsRecordMultiClauseMode);
            Assert.Equal("SAL_REG", vm2.SelectedTable);
            Assert.Equal(3, vm2.Clauses.Count);
            Assert.Equal("F1064", vm2.Clauses[2].ColumnName);
            Assert.Equal("Void", vm2.Clauses[2].Value);

            var root = vm2.BuildRootGroup();
            Assert.Equal(2, root.Children.Count);
            Assert.IsType<SearchClause>(root.Children[0]);
            Assert.IsType<SearchClauseGroup>(root.Children[1]);

            var item = new SavedQueryItem { Name = "Test", Sql = "SELECT 1", AdvancedQuery = exported };
            Assert.True(item.IsAdvanced);
            Assert.Equal("ADV", item.QueryKindLabel);
            Assert.Equal("SQL", new SavedQueryItem { Name = "SqlOnly", Sql = "SELECT 1" }.QueryKindLabel);
        }

        [Fact]
        public void ToggleMultiClause_DefaultClauses_AreNumberEqualsAndDescriptionContains()
        {
            using var vm = CreateViewModel();
            vm.SelectedMode = SearchMode.Function;

            vm.ToggleMultiClauseCommand.Execute(null);

            Assert.True(vm.IsMultiClauseMode);
            Assert.Equal(2, vm.Clauses.Count);
            Assert.Equal(SearchFieldTarget.Number, vm.Clauses[0].Field);
            Assert.Equal(SearchComparisonOperator.Equals, vm.Clauses[0].Operator);
            Assert.Equal(SearchFieldTarget.Description, vm.Clauses[1].Field);
            Assert.Equal(SearchComparisonOperator.Contains, vm.Clauses[1].Operator);
        }

        [Fact]
        public void MoveClauseUpAndDown_ReordersClausesAndMaintainsValidGroups()
        {
            using var vm = CreateViewModel();
            vm.ToggleMultiClauseCommand.Execute(null);
            vm.AddClauseCommand.Execute(null);

            vm.Clauses[0].Value = "First";
            vm.Clauses[1].Value = "Second";
            vm.Clauses[2].Value = "Third";

            Assert.False(vm.Clauses[0].CanMoveUp);
            Assert.True(vm.Clauses[0].CanMoveDown);
            Assert.False(vm.Clauses[2].CanMoveDown);

            // Group rows 1 and 2, then swap them (both inside group -> group preserved)
            vm.Clauses[1].IsSelectedForGroup = true;
            vm.Clauses[2].IsSelectedForGroup = true;
            vm.GroupSelectedClausesCommand.Execute(null);

            vm.MoveClauseUpCommand.Execute(vm.Clauses[2]);
            Assert.Equal("Third", vm.Clauses[1].Value);
            Assert.Equal("Second", vm.Clauses[2].Value);

            var root = vm.BuildRootGroup();
            Assert.Equal(2, root.Children.Count);
            var group = Assert.IsType<SearchClauseGroup>(root.Children[1]);
            Assert.Equal(2, group.Children.Count);

            // Move row 1 up out of the group -> group (<2 rows) cleanly dissolves
            vm.MoveClauseUpCommand.Execute(vm.Clauses[1]);
            Assert.Equal("Third", vm.Clauses[0].Value);
            Assert.Equal("First", vm.Clauses[1].Value);
            Assert.True(vm.Clauses[0].IsFirstRow);
            Assert.True(vm.Clauses[2].IsLastRow);
            Assert.Equal(3, vm.BuildRootGroup().Children.Count);
        }

        [Fact]
        public void SaveSqlQueryCommand_SavesCustomSqlViaSavedQueriesService()
        {
            var config = new Mock<IConfigService>();
            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));

            var dialog = new Mock<IDialogService>();
            string outName = "My Custom SQL";
            string outCat = "Reports";
            dialog.Setup(d => d.TryPromptSaveQuery(It.IsAny<string>(), It.IsAny<System.Collections.Generic.IEnumerable<string>>(), out outName, out outCat, "Save SQL Query"))
                  .Returns(true);

            var savedQueries = new Mock<ISavedQueriesService>();
            savedQueries.Setup(s => s.GetCategories()).Returns(new System.Collections.Generic.List<string> { "General" });
            savedQueries.Setup(s => s.FindDuplicatesBySql(It.IsAny<string>())).Returns(new System.Collections.Generic.List<SavedQueryItem>());
            savedQueries.Setup(s => s.Save("My Custom SQL", "Reports", "SELECT * FROM FCT_TAB", null))
                        .Returns(new SavedQueryItem { Name = "My Custom SQL", Category = "Reports", Sql = "SELECT * FROM FCT_TAB" });

            using var vm = new SearchViewModel(
                new Mock<IDataRepository>().Object,
                dialog.Object,
                config.Object,
                new Mock<ILoggerService>().Object,
                new Mock<IQueryHistoryService>().Object,
                new Mock<IClipboardService>().Object,
                new Mock<IIntellisenseService>().Object,
                connSettings.Object,
                savedQueries.Object);

            vm.CustomSqlText = "SELECT * FROM FCT_TAB";
            vm.SaveSqlQueryCommand.Execute(null);

            savedQueries.Verify(s => s.Save("My Custom SQL", "Reports", "SELECT * FROM FCT_TAB", null), Times.Once);
        }

        [Fact]
        public void ApplyLayoutSearchPreferences_AppliesExplicitAndLastCorrectly()
        {
            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastTab)).Returns("Totalizer");
            config.Setup(c => c.GetValue(AppSettings.Sections.Main, AppSettings.Keys.LastSearchMode)).Returns("Advanced");

            var connSettings = new Mock<IConnectionSettingsService>();
            connSettings.Setup(c => c.GetConnectionSettings()).Returns(new ConnectionSettingsInfo("srv", "db", "", null, true));

            using var vm = new SearchViewModel(
                new Mock<IDataRepository>().Object,
                new Mock<IDialogService>().Object,
                config.Object,
                new Mock<ILoggerService>().Object,
                new Mock<IQueryHistoryService>().Object,
                new Mock<IClipboardService>().Object,
                new Mock<IIntellisenseService>().Object,
                connSettings.Object);

            vm.SelectedMode = SearchMode.Function;
            vm.IsMultiClauseMode = false;

            // Explicit -> sets Field + Advanced
            vm.ApplyLayoutSearchPreferences(LayoutSearchTabPreference.Field, LayoutSearchModePreference.Advanced);
            Assert.Equal(SearchMode.Field, vm.SelectedMode);
            Assert.True(vm.IsMultiClauseMode);

            // Explicit -> sets Function + Simple
            vm.ApplyLayoutSearchPreferences(LayoutSearchTabPreference.Function, LayoutSearchModePreference.Simple);
            Assert.Equal(SearchMode.Function, vm.SelectedMode);
            Assert.False(vm.IsMultiClauseMode);

            // Last -> restores Totalizer + Advanced from config
            vm.ApplyLayoutSearchPreferences(LayoutSearchTabPreference.Last, LayoutSearchModePreference.Last);
            Assert.Equal(SearchMode.Totalizer, vm.SelectedMode);
            Assert.True(vm.IsMultiClauseMode);

            // Records -> activates Field + ShowRecords + IsRecordsTabActive
            vm.ApplyLayoutSearchPreferences(LayoutSearchTabPreference.Records, LayoutSearchModePreference.Simple);
            Assert.Equal(SearchMode.Field, vm.SelectedMode);
            Assert.True(vm.IsRecordsTabActive);
            Assert.False(vm.IsFieldsTabActive);
            Assert.True(vm.ShowRecords);
            Assert.False(vm.IsMultiClauseMode);

            // Switching to Fields tab activates ShowFields
            vm.IsFieldsTabActive = true;
            Assert.True(vm.IsFieldsTabActive);
            Assert.False(vm.IsRecordsTabActive);
            Assert.True(vm.ShowFields);

            // Selecting Number on Fields, switching to Records, and back to Fields preserves Number
            vm.IsFieldNumber = true;
            vm.SelectedTable = "SAL_REG";
            vm.IsRecordsTabActive = true;
            Assert.True(vm.IsFieldTable);
            var recCriteria = vm.GetSearchCriteria();
            Assert.Equal(SearchType.Table, recCriteria.Type);
            Assert.False(recCriteria.ShowFields);
            Assert.Equal("SAL_REG", recCriteria.Value);
            vm.IsFieldsTabActive = true;
            Assert.True(vm.IsFieldNumber);
            Assert.False(vm.IsFieldTable);

            // Selecting Table on Fields, switching to Function, and back to Fields preserves Table
            vm.IsFieldTable = true;
            vm.IsFieldNumber = false;
            vm.SelectedMode = SearchMode.Function;
            vm.IsFieldTable = false; // simulates WPF RadioButton GroupName clearing IsFieldTable
            vm.IsFieldsTabActive = true;
            Assert.True(vm.IsFieldTable);

            // GroupName pushing false to IsActiveNumber/Description/FieldTable must never leave a tab unselected
            vm.SelectedMode = SearchMode.Function;
            vm.IsActiveNumber = true;
            vm.IsActiveNumber = false;
            Assert.True(vm.IsFunctionNumber);
            Assert.True(vm.IsActiveNumber);

            vm.IsActiveDescription = true;
            vm.IsActiveDescription = false;
            Assert.True(vm.IsFunctionDescription);
            Assert.True(vm.IsActiveDescription);

            // Setting IsFieldTable clears IsFieldNumber so Table search filters by SelectedTable
            vm.IsFieldsTabActive = true;
            vm.IsFieldNumber = true;
            vm.SelectedTable = "SYS_TAB";
            vm.IsFieldTable = true;
            var tblCriteria = vm.GetSearchCriteria();
            Assert.Equal(SearchType.Table, tblCriteria.Type);
            Assert.True(tblCriteria.ShowFields);
            Assert.Equal("SYS_TAB", tblCriteria.Value);
        }

        [Fact]
        public void ResultTabViewModel_CustomTitleFlag_PersistsOnRename()
        {
            var repo = new Mock<IDataRepository>().Object;
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus());
            var resultsVm = new ResultsViewModel(
                new VirtualGridContext(repo),
                new Mock<ILoggerService>().Object,
                new Mock<IDialogService>().Object,
                new Mock<IConfigService>().Object,
                new Mock<IClipboardService>().Object,
                new Mock<IConnectionSettingsService>().Object,
                repo,
                dictMock.Object);

            using var tab = new ResultTabViewModel(resultsVm, _ => { }, "Initial");
            Assert.False(tab.HasCustomTitle);

            tab.BeginRename();
            Assert.True(tab.IsRenaming);
            Assert.Equal("Initial", tab.EditingTitle);

            tab.EditingTitle = "Cancelled";
            tab.CancelRename();
            Assert.False(tab.IsRenaming);
            Assert.Equal("Initial", tab.Title);
            Assert.False(tab.HasCustomTitle);

            tab.BeginRename();
            tab.EditingTitle = "Pinned Summary";
            tab.CommitRename();

            Assert.False(tab.IsRenaming);
            Assert.Equal("Pinned Summary", tab.Title);
            Assert.True(tab.HasCustomTitle);
        }

        [Fact]
        public void WorkspaceLayoutService_SetProfileSuppressCloseSavePrompt_PersistsFlag()
        {
            var state = new Mock<IStateService>();
            string? savedJson = null;
            state.Setup(s => s.GetValue("WORKSPACE_LAYOUT", "PROFILES")).Returns(() => savedJson);
            state.Setup(s => s.SetValue("WORKSPACE_LAYOUT", "PROFILES", It.IsAny<string>()))
                 .Callback<string, string, string>((_, _, v) => savedJson = v);

            var svc = new WorkspaceLayoutService(state.Object, new Mock<ILoggerService>().Object);
            svc.SaveProfile("Custom", "<LayoutRoot/>");
            Assert.False(svc.GetProfile("Custom")!.SuppressCloseSavePrompt);

            svc.SetProfileSuppressCloseSavePrompt("Custom", true);
            Assert.True(svc.GetProfile("Custom")!.SuppressCloseSavePrompt);

            svc.SetProfileSuppressCloseSavePrompt("Custom", false);
            Assert.False(svc.GetProfile("Custom")!.SuppressCloseSavePrompt);
        }
    }
}

