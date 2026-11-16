using System.Collections.Generic;
using SMS_Search.Data;
using Xunit;

namespace SMSSearch.Tests
{
    public class SqlImportSchemaInferrerTests
    {
        [Fact]
        public void InferColumns_FromCreateViewAndInserts_InfersExpectedSqlTypes()
        {
            var statements = new List<string>
            {
                "CREATE VIEW vTemp AS SELECT F1001, F1002, F1003, F1004, F1005 FROM TEST_TAB",
                "INSERT INTO vTemp VALUES (123, 45.67, '2026045', 'Hello', NULL)",
                "INSERT INTO vTemp VALUES (456, 10, '2026046 12:30:00', 'World', NULL)"
            };

            var columns = SqlImportSchemaInferrer.InferColumns(statements);

            Assert.Equal(5, columns.Count);
            Assert.Equal(InferredColumnType.Int, columns[0].Type);
            Assert.Equal(InferredColumnType.Decimal, columns[1].Type);
            Assert.Equal(InferredColumnType.DateTime, columns[2].Type);
            Assert.Equal(InferredColumnType.String, columns[3].Type);
            Assert.Equal(InferredColumnType.Unknown, columns[4].Type);

            string createSql = SqlImportSchemaInferrer.BuildCreateTableSql("TEST_TAB", statements);
            Assert.Contains("[F1001] INT NULL", createSql);
            Assert.Contains("[F1002] DECIMAL(38, 10) NULL", createSql);
            Assert.Contains("[F1003] DATETIME NULL", createSql);
            Assert.Contains("[F1004] NVARCHAR(MAX) NULL", createSql);
            Assert.Contains("[F1005] NVARCHAR(MAX) NULL", createSql);
        }

        [Fact]
        public void InferColumns_WidensMixedTypesToStringAndIntToBigInt()
        {
            var statements = new List<string>
            {
                "CREATE VIEW vTemp AS SELECT F1, F2 FROM MIX_TAB",
                "INSERT INTO vTemp VALUES (100, 42)",
                "INSERT INTO vTemp VALUES (9999999999, 'abc')"
            };

            var columns = SqlImportSchemaInferrer.InferColumns(statements);

            Assert.Equal(2, columns.Count);
            Assert.Equal(InferredColumnType.BigInt, columns[0].Type);
            Assert.Equal(InferredColumnType.String, columns[1].Type);
        }

        [Fact]
        public void InferColumns_RespectsExplicitInsertColumnMapping()
        {
            var statements = new List<string>
            {
                "CREATE VIEW vTemp AS SELECT A, B, C FROM MAP_TAB",
                "INSERT INTO vTemp (C, A) VALUES (10, 'text')"
            };

            var columns = SqlImportSchemaInferrer.InferColumns(statements);

            Assert.Equal(InferredColumnType.String, columns[0].Type);
            Assert.Equal(InferredColumnType.Unknown, columns[1].Type);
            Assert.Equal(InferredColumnType.Int, columns[2].Type);
        }

        [Fact]
        public void BuildCreateTableSql_WithExplicitTypes_UsesConfirmedSqlTypes()
        {
            var cols = new List<(string Name, string SqlType)>
            {
                ("F1001", "BIGINT"),
                ("F1002", "DECIMAL(18, 2)"),
                ("F1003", "")
            };

            string sql = SqlImportSchemaInferrer.BuildCreateTableSql("CONF_TAB", cols);

            Assert.Equal("CREATE TABLE [dbo].[CONF_TAB] ([F1001] BIGINT NULL, [F1002] DECIMAL(18, 2) NULL, [F1003] NVARCHAR(MAX) NULL)", sql);
        }
    }
}

