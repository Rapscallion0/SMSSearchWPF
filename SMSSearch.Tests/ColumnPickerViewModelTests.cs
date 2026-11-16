using Moq;
using SMS_Search.Data;
using SMS_Search.Models;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels.Settings;
using Xunit;

namespace SMSSearch.Tests
{
    public class ColumnPickerViewModelTests
    {
        private static ColumnPickerViewModel CreateVm(
            string current = "F1063, F1064",
            string defaults = "F1063, F1064, F1051",
            string[]? required = null)
        {
            var vm = new ColumnPickerViewModel(
                new Mock<IDataRepository>().Object,
                new Mock<IConnectionSettingsService>().Object,
                new Mock<ILoggerService>().Object,
                "Test Picker",
                "FCT_TAB",
                current,
                defaults,
                required);

            vm.SetAvailableColumnsForTest(new[]
            {
                new ColumnItem("F1063", "Function Number"),
                new ColumnItem("F1064", "Description"),
                new ColumnItem("F1051", "Action Code"),
                new ColumnItem("F1081", "Flag")
            });

            return vm;
        }

        [Fact]
        public void SearchText_FiltersAvailableColumnsByNameOrDescription()
        {
            using var vm = CreateVm(current: "");

            vm.SearchText = "Action";

            Assert.Single(vm.FilteredAvailableColumns);
            Assert.Equal("F1051", vm.FilteredAvailableColumns[0].Name);
        }

        [Fact]
        public void AddAndRemove_UpdatesSelectedAndAvailableLists()
        {
            using var vm = CreateVm(current: "F1063");

            vm.SelectedAvailableColumn = vm.FilteredAvailableColumns[0]; // F1064
            vm.AddSelectedCommand.Execute(null);

            Assert.Equal("F1063, F1064", vm.GetSelectedColumnsString());
            Assert.DoesNotContain(vm.FilteredAvailableColumns, c => c.Name == "F1064");

            vm.SelectedChosenColumn = vm.SelectedColumns[0]; // F1063
            vm.RemoveSelectedCommand.Execute(null);

            Assert.Equal("F1064", vm.GetSelectedColumnsString());
            Assert.Contains(vm.FilteredAvailableColumns, c => c.Name == "F1063");
        }

        [Fact]
        public void MoveUpDownAndDragDrop_ReordersSelectedColumns()
        {
            using var vm = CreateVm(current: "F1063, F1064, F1051");

            vm.SelectedChosenColumn = vm.SelectedColumns[2]; // F1051
            vm.MoveUpCommand.Execute(null);
            Assert.Equal("F1063, F1051, F1064", vm.GetSelectedColumnsString());

            vm.MoveColumn(0, 2);
            Assert.Equal("F1051, F1064, F1063", vm.GetSelectedColumnsString());

            vm.SelectedChosenColumn = vm.SelectedColumns[0]; // F1051
            vm.MoveDownCommand.Execute(null);
            Assert.Equal("F1064, F1051, F1063", vm.GetSelectedColumnsString());
        }

        [Fact]
        public void ResetDefaults_RestoresDefaultColumnOrder()
        {
            using var vm = CreateVm(current: "F1081", defaults: "F1063, F1064, F1051");

            vm.ResetDefaultsCommand.Execute(null);

            Assert.Equal("F1063, F1064, F1051", vm.GetSelectedColumnsString());
        }

        [Fact]
        public void SelectedColumns_EnrichDescriptionsAndDisplayText()
        {
            using var vm = CreateVm(current: "F1063, F1064", required: new[] { "F1063" });

            Assert.Equal("Function Number", vm.SelectedColumns[0].Description);
            Assert.Contains("Function Number", vm.SelectedColumns[0].DisplayText);
            Assert.Contains("[Required]", vm.SelectedColumns[0].DisplayText);
            Assert.Equal("F1064 — Description", vm.SelectedColumns[1].DisplayText);
        }

        [Fact]
        public void RequiredColumns_AreAutoIncludedAndCannotBeRemoved()
        {
            using var vm = CreateVm(current: "F1064", required: new[] { "F1063" });

            Assert.Contains(vm.SelectedColumns, c => c.Name == "F1063" && c.IsRequired);

            vm.SelectedChosenColumn = vm.SelectedColumns[0]; // F1063
            Assert.False(vm.RemoveSelectedCommand.CanExecute(null));
            vm.RemoveSelectedCommand.Execute(null);
            Assert.Contains(vm.SelectedColumns, c => c.Name == "F1063");
        }

        [Fact]
        public void InvalidColumns_AreFlaggedInSelectedColumnsAndClearedWhenRemoved()
        {
            using var vm = CreateVm(current: "F1063, F9999", required: new[] { "F1063" });

            Assert.True(vm.SelectedColumns[1].IsInvalid);
            Assert.Contains("[Invalid]", vm.SelectedColumns[1].DisplayText);
            Assert.Contains("F9999", vm.ErrorMessage);

            vm.SelectedChosenColumn = vm.SelectedColumns[1];
            vm.RemoveSelectedCommand.Execute(null);

            Assert.Null(vm.ErrorMessage);
        }
    }
}

