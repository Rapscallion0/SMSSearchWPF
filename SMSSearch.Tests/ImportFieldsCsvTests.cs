using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using SMS_Search.ViewModels;
using Xunit;

namespace SMSSearch.Tests
{
    public class ImportFieldsCsvTests
    {
        [Theory]
        [InlineData("C", "13", "", "VARCHAR(13)")]
        [InlineData("C", "9000", "", "VARCHAR(MAX)")]
        [InlineData("D", "7", "", "DATETIME")]
        [InlineData("F", "1", "", "BIT")]
        [InlineData("I", "4", "", "INT")]
        [InlineData("I", "10", "", "BIGINT")]
        [InlineData("M", "6", "2", "DECIMAL(6,2)")]
        [InlineData("M", "", "", "MONEY")]
        [InlineData("N", "5", "3", "DECIMAL(5,3)")]
        [InlineData("T", "4", "", "VARCHAR(4)")]
        [InlineData("X", "9000", "", "NVARCHAR(MAX)")]
        [InlineData("dtChar", "20", "", "VARCHAR(20)")]
        public void FieldDictionarySqlTypeMapper_MapsDictionaryTypesToSqlTypes(
            string type, string size, string dec, string expectedSqlType)
        {
            var entry = new FieldDictionaryEntry
            {
                Field = "F01",
                Type = type,
                Size = size,
                Dec = dec
            };

            Assert.Equal(expectedSqlType, FieldDictionarySqlTypeMapper.MapToSqlType(entry));
        }

        [Theory]
        [InlineData("C", "dtChar")]
        [InlineData("D", "dtDate")]
        [InlineData("F", "dtFlag")]
        [InlineData("I", "dtInt")]
        [InlineData("M", "dtMoney")]
        [InlineData("N", "dtNum")]
        [InlineData("T", "dtTime")]
        [InlineData("X", "dtXML")]
        public void FieldDictionarySqlTypeMapper_MapsToRbFieldsType(string csvType, string expectedRbType)
        {
            Assert.Equal(expectedRbType, FieldDictionarySqlTypeMapper.MapToRbFieldsType(csvType));
        }

        [Fact]
        public void ImportViewModel_SchemaSourceTransitionsAndCustomFieldsCsvPath_WorkAsExpected()
        {
            var dictMock = new Mock<IFieldDictionaryService>();
            dictMock.Setup(d => d.GetStatus()).Returns(new DictionaryStatus
            {
                IsCsvAvailable = false,
                ResolvedFieldsCsvPath = null
            });

            var vm = new ImportViewModel(
                new Mock<IDataRepository>().Object,
                new Mock<ILoggerService>().Object,
                new Mock<IDialogService>().Object,
                new Mock<IConfigService>().Object,
                new Mock<IConnectionSettingsService>().Object,
                dictMock.Object);

            Assert.Equal("No Fields.csv configured — click Browse to select one", vm.ActiveFieldsCsvDisplay);

            vm.IsFieldsCsvSource = true;
            Assert.True(vm.IsFieldsCsvSource);
            Assert.False(vm.UseTemplateDatabase);

            vm.CustomFieldsCsvPath = @"C:\Temp\Fields.csv";
            Assert.Equal(@"C:\Temp\Fields.csv", vm.ActiveFieldsCsvDisplay);

            vm.IsInferSource = true;
            Assert.True(vm.IsInferSource);
            Assert.False(vm.IsFieldsCsvSource);
        }
    }
}

