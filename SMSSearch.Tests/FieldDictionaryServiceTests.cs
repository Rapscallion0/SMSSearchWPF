using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Moq;
using SMS_Search.Data;
using SMS_Search.Services;
using SMS_Search.Utils;
using Xunit;

namespace SMSSearch.Tests
{
    public class FieldDictionaryServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly Dictionary<string, string> _settings;
        private readonly Mock<IConfigService> _configMock;
        private readonly Mock<ILoggerService> _loggerMock;

        public FieldDictionaryServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SMSSearch_DictTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _configMock = new Mock<IConfigService>();
            _configMock
                .Setup(c => c.GetValue(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string section, string key) =>
                    _settings.TryGetValue($"{section}:{key}", out var v) ? v : null);
            _configMock
                .Setup(c => c.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string section, string key, string val) =>
                    _settings[$"{section}:{key}"] = val);

            _loggerMock = new Mock<ILoggerService>();
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private void WriteSampleFiles(bool includeFields2 = true, bool includeFieldsEs = true)
        {
            File.WriteAllText(Path.Combine(_tempDir, "Fields.csv"), string.Join(Environment.NewLine, new[]
            {
                "F,FIELD,TYPE,SIZE,DEC,DESCRIPTOR,Short,P,Translation,Short,S,OBJ,FOBJ,SCAL_ITM",
                "1,F01,C,13,,\"UPC, Primary\",Upc,,Code CUP,Cup,,P,P;F902,P",
                ",F02,C,40,,POS description,POSDesc,1,Description,DescPDV,,R,F,F",
                ",F902,C,10,,Batch ID,Batch,,Lot,Lot,, ,R,"
            }));

            if (includeFields2)
            {
                File.WriteAllText(Path.Combine(_tempDir, "Fields2.csv"), string.Join(Environment.NewLine, new[]
                {
                    "HOT,FIELD,TYPE,SIZE,DEC,DESCRIPTOR,Short,P,Translation,Short,S,OBJ,BUYERS",
                    ",F01,,,,,, , , , , ,P;F6000",
                    ",F6000,C,50,,Region,,,,,,F,R"
                }));
            }

            if (includeFieldsEs)
            {
                File.WriteAllText(Path.Combine(_tempDir, "Fields.es"), string.Join(Environment.NewLine, new[]
                {
                    "FIELD,Translation,Short",
                    "F01,Código UPC,UPC"
                }));
            }
        }

        [Fact]
        public void ParseAndMerge_HandlesDuplicateShortHeaders_QuotedFields_AndNonDestructiveFields2()
        {
            WriteSampleFiles();
            var svc = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, _tempDir);

            var status = svc.GetStatus();
            Assert.True(status.IsCsvAvailable);
            Assert.True(status.IsAutoDetected);
            Assert.True(status.HasFields2Csv);
            Assert.True(status.HasFieldsEs);
            Assert.Equal(4, status.FieldCount);

            // F01 preserves Type=C, Size=13 from Fields.csv even though Fields2.csv left them blank
            var dt = svc.SearchOffline(new SearchCriteria { Mode = SearchMode.Field, Type = SearchType.Number, Value = "01" });
            var objRow = dt.Rows.Cast<DataRow>().First(r => r["Table"].ToString() == "OBJ_TAB");
            Assert.Equal("UPC, Primary", objRow["Description"]);
            Assert.Equal("C", objRow["Type"]);
            Assert.Equal(13, objRow["Size"]);
            Assert.Equal("1", objRow["Key"]);
            Assert.Equal(1, objRow["Required"]);
        }

        [Fact]
        public void CompositePrimaryKey_MarksReferencedFieldAsKeyAndRequired()
        {
            WriteSampleFiles();
            var svc = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, _tempDir);

            // In FOBJ, F01 has "P;F902", so F902 is part of the composite primary key
            var dt = svc.SearchOffline(new SearchCriteria { Mode = SearchMode.Field, Type = SearchType.Table, ShowFields = true, Value = "FOBJ_BAT" });
            var f902Row = dt.Rows.Cast<DataRow>().First(r => r["Field"].ToString() == "F902");
            Assert.Equal("1", f902Row["Key"]);
            Assert.Equal(1, f902Row["Required"]);
            Assert.Equal("FOBJ", f902Row["Dictionary"]);
        }

        [Fact]
        public void SpanishFallback_UsesEnglishWhenFieldMissingFromFieldsEs()
        {
            WriteSampleFiles();
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.DictionaryLanguage}"] = "Spanish";
            var svc = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, _tempDir);

            Assert.Equal("Código UPC", svc.GetDescription("F01"));
            Assert.Equal("POS description", svc.GetDescription("F02"));
        }

        [Fact]
        public void SearchOffline_IncludesAdditionalLanguageAndDctColumns()
        {
            WriteSampleFiles();
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.DictionaryShowEn}"] = "1";
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.DictionaryShowFr}"] = "1";
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.DictionaryShowEs}"] = "1";
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.DictionaryShowDct}"] = "1";

            var svc = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, _tempDir);
            var dt = svc.SearchOffline(new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Number,
                Value = "6000"
            });

            Assert.False(dt.Columns.Contains("Description (EN)")); // Primary language is English
            Assert.True(dt.Columns.Contains("Description (FR)"));
            Assert.True(dt.Columns.Contains("Description (ES)"));
            Assert.True(dt.Columns.Contains("Dictionary"));
            Assert.Equal(2, dt.Rows.Count); // OBJ_TAB and BUYERS_TAB
        }

        [Fact]
        public void EnrichSqlMetadata_PreservesColumnsMissingFromCsvWithSqlFallback()
        {
            WriteSampleFiles();
            var svc = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, _tempDir);

            var sqlDt = new DataTable();
            sqlDt.Columns.Add("Field", typeof(string));
            sqlDt.Columns.Add("Description", typeof(string));
            sqlDt.Columns.Add("Table", typeof(string));
            sqlDt.Columns.Add("Type", typeof(string));
            sqlDt.Columns.Add("Key", typeof(string));
            sqlDt.Columns.Add("Size", typeof(int));
            sqlDt.Columns.Add("Dec", typeof(int));
            sqlDt.Columns.Add("Required", typeof(int));
            sqlDt.Columns.Add("Created", typeof(DateTime));

            sqlDt.Rows.Add("F01", "", "SCL_TAB", "varchar", "1", 13, 0, 1, DateTime.Today);
            sqlDt.Rows.Add("F9999", "", "SCL_TAB", "int", "0", 4, 0, 0, DateTime.Today);

            var enriched = svc.EnrichSqlMetadata(sqlDt, new SearchCriteria { Mode = SearchMode.Field, Type = SearchType.Table, ShowFields = true, Value = "SCL_TAB" });
            Assert.Equal(2, enriched.Rows.Count);

            var f01 = enriched.Rows[0];
            Assert.Equal("UPC, Primary", f01["Description"]);
            Assert.Equal("C", f01["Type"]);
            Assert.Equal("SCAL_ITM", f01["Dictionary"]);

            var f9999 = enriched.Rows[1];
            Assert.Equal("F9999", f9999["Field"]);
            Assert.Equal("", f9999["Description"]);
            Assert.Equal("int", f9999["Type"]);
            Assert.Equal("SCAL_ITM", f9999["Dictionary"]);
        }

        [Fact]
        public void SourceModePrecedence_RespectsDatabaseAndCsvOverrides()
        {
            WriteSampleFiles();
            var svc = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, _tempDir);

            Assert.True(svc.IsCsvActive()); // Auto with Fields.csv present

            svc.SetSourceMode(DictionarySourceMode.Database);
            Assert.False(svc.IsCsvActive());

            svc.SetSourceMode(DictionarySourceMode.Csv);
            Assert.True(svc.IsCsvActive());
        }

        [Theory]
        [InlineData("SCL_TAB", "SCAL_ITM")]
        [InlineData("RPT_ITM_D", "RPTITM")]
        [InlineData("SAL_HDR", "HDRSAL")]
        [InlineData("FOBJ_BAT", "FOBJ")]
        [InlineData("OBJ_TAB", "OBJ")]
        public void DictionaryTableMapper_MapsSqlToDct(string sqlTable, string expectedDct)
        {
            Assert.Equal(expectedDct, DictionaryTableMapper.GetDctForSqlTable(sqlTable));
        }

        [Fact]
        public void GetOptions_DoesNotDuplicatePrimaryLanguageColumn()
        {
            WriteSampleFiles();
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.DictionaryLanguage}"] = "English";
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.DictionaryShowEn}"] = "1";
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.DictionaryShowFr}"] = "1";

            var svc = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, _tempDir);
            var options = svc.GetOptions();

            Assert.False(options.ShowEn);
            Assert.True(options.ShowFr);
        }

        [Fact]
        public void StartupConditionEvaluator_AllowsStartWithDbOrValidCsv()
        {
            var emptyDir = Path.Combine(_tempDir, "empty");
            Directory.CreateDirectory(emptyDir);
            var svcNoCsv = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, emptyDir);

            // Neither DB nor CSV
            Assert.False(StartupConditionEvaluator.CanStart(_configMock.Object, svcNoCsv));

            // Valid CSV without DB
            WriteSampleFiles();
            var svcWithCsv = new FieldDictionaryService(_configMock.Object, _loggerMock.Object, _tempDir);
            Assert.True(StartupConditionEvaluator.CanStart(_configMock.Object, svcWithCsv));

            // DB without CSV
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.Server}"] = "localhost";
            _settings[$"{AppSettings.Sections.Connection}:{AppSettings.Keys.Database}"] = "SMS";
            Assert.True(StartupConditionEvaluator.CanStart(_configMock.Object, svcNoCsv));
        }

        [Fact]
        public void FileDropBehavior_ResolvesCsvFileAndFolderContainingFieldsCsv()
        {
            WriteSampleFiles();
            string fieldsCsv = Path.Combine(_tempDir, "Fields.csv");
            string txtFile = Path.Combine(_tempDir, "notes.txt");
            File.WriteAllText(txtFile, "test");

            Assert.Equal(fieldsCsv, FileDropBehavior.ResolveDroppedCsvPath(new[] { fieldsCsv }));
            Assert.Equal(fieldsCsv, FileDropBehavior.ResolveDroppedCsvPath(new[] { _tempDir }));
            Assert.Null(FileDropBehavior.ResolveDroppedCsvPath(new[] { txtFile }));

            var emptyDir = Path.Combine(_tempDir, "emptyDrop");
            Directory.CreateDirectory(emptyDir);
            Assert.Null(FileDropBehavior.ResolveDroppedCsvPath(new[] { emptyDir }));
        }

        [Fact]
        public void CopyDictionaryFiles_DetectsExistingFilesAndCopiesCompanions()
        {
            WriteSampleFiles();
            string srcCsv = Path.Combine(_tempDir, "Fields.csv");
            string targetDir = Path.Combine(_tempDir, "installDir");
            Directory.CreateDirectory(targetDir);

            // Initially no files in targetDir
            Assert.Empty(FieldDictionaryService.GetExistingDictionaryFiles(srcCsv, targetDir));

            // Copy all 3 files
            FieldDictionaryService.CopyDictionaryFilesToDirectory(srcCsv, targetDir);
            Assert.True(File.Exists(Path.Combine(targetDir, "Fields.csv")));
            Assert.True(File.Exists(Path.Combine(targetDir, "Fields2.csv")));
            Assert.True(File.Exists(Path.Combine(targetDir, "Fields.es")));

            // Now all 3 should be detected as existing
            var existing = FieldDictionaryService.GetExistingDictionaryFiles(srcCsv, targetDir);
            Assert.Equal(new[] { "Fields.csv", "Fields2.csv", "Fields.es" }, existing);
        }
    }
}

