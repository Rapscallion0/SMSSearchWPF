using SMS_Search.Data;
using Xunit;

namespace SMSSearch.Tests
{
    public class QueryBuilderTests
    {
        [Fact]
        public void Build_FieldNumber_GeneratesParameterizedQuery()
        {
            var qb = new QueryBuilder("", "");
            var criteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Number,
                Value = "1032",
                AnyMatch = false
            };

            var result = qb.Build(criteria);

            Assert.Contains("col.name = @Val", result.Sql);
            Assert.NotNull(result.Parameters);
        }

        [Fact]
        public void Build_FieldAnyMatch_UsesLikeWildcard()
        {
            var qb = new QueryBuilder("", "");
            var criteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Description,
                Value = "Item",
                AnyMatch = true
            };

            var result = qb.Build(criteria);

            Assert.Contains("RBF.F1454 LIKE @Val", result.Sql);
        }

        [Theory]
        [InlineData(SearchMode.Function, SearchType.Number)]
        [InlineData(SearchMode.Function, SearchType.Description)]
        [InlineData(SearchMode.Totalizer, SearchType.Number)]
        [InlineData(SearchMode.Totalizer, SearchType.Description)]
        [InlineData(SearchMode.Field, SearchType.Number)]
        [InlineData(SearchMode.Field, SearchType.Description)]
        public void Build_EmptyValue_OmitsWhereClauseToReturnFullResultSet(SearchMode mode, SearchType type)
        {
            var qb = new QueryBuilder("", "");
            var criteria = new SearchCriteria
            {
                Mode = mode,
                Type = type,
                Value = "   ",
                AnyMatch = true
            };

            var result = qb.Build(criteria);

            Assert.DoesNotContain("WHERE", result.Sql);
        }

        [Fact]
        public void Build_FieldTableShowFieldsEmptyValue_OmitsWhereClause()
        {
            var qb = new QueryBuilder("", "");
            var criteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Table,
                Value = "",
                ShowFields = true
            };

            var result = qb.Build(criteria);

            Assert.DoesNotContain("WHERE", result.Sql);
        }

        [Fact]
        public void Build_FieldWithoutRbFields_OmitsRbFieldsJoin()
        {
            var qb = new QueryBuilder("", "");
            var criteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Number,
                Value = "1032",
                AnyMatch = false
            };

            var result = qb.Build(criteria, hasRbFields: false);

            Assert.DoesNotContain("RB_FIELDS", result.Sql);
            Assert.Contains("col.name = @Val", result.Sql);
        }

        [Fact]
        public void Build_CompoundNestedGroup_GeneratesParenthesizedSql()
        {
            var qb = new QueryBuilder("", "");
            var innerGroup = new SearchClauseGroup
            {
                LogicalOperator = SearchLogicalOperator.And,
                Children =
                {
                    new SearchClause { LogicalOperator = SearchLogicalOperator.And, Field = SearchFieldTarget.Description, Operator = SearchComparisonOperator.Contains, Value = "Cash" },
                    new SearchClause { LogicalOperator = SearchLogicalOperator.Or, Field = SearchFieldTarget.Description, Operator = SearchComparisonOperator.Contains, Value = "Tender" }
                }
            };

            var root = new SearchClauseGroup
            {
                Children =
                {
                    new SearchClause { LogicalOperator = SearchLogicalOperator.And, Field = SearchFieldTarget.Table, Operator = SearchComparisonOperator.Equals, Value = "BAL_TAB" },
                    innerGroup
                }
            };

            var criteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Description,
                RootGroup = root
            };

            var result = qb.Build(criteria);

            Assert.Contains("WHERE TAB.name = @p0 AND (RBF.F1454 LIKE @p1 OR RBF.F1454 LIKE @p2)", result.Sql);
            Assert.Equal("BAL_TAB", result.Parameters!.Get<string>("p0"));
            Assert.Equal("%Cash%", result.Parameters!.Get<string>("p1"));
            Assert.Equal("%Tender%", result.Parameters!.Get<string>("p2"));
        }

        [Fact]
        public void Build_CompoundFieldInOperator_PrefixesFieldNumbers()
        {
            var qb = new QueryBuilder("", "");
            var root = new SearchClauseGroup
            {
                Children =
                {
                    new SearchClause { Field = SearchFieldTarget.Number, Operator = SearchComparisonOperator.In, Value = "100, F101, 102" }
                }
            };

            var criteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Number,
                RootGroup = root
            };

            var result = qb.Build(criteria);

            Assert.Contains("WHERE col.name IN (@p0, @p1, @p2)", result.Sql);
            Assert.Equal("F100", result.Parameters!.Get<string>("p0"));
            Assert.Equal("F101", result.Parameters!.Get<string>("p1"));
            Assert.Equal("F102", result.Parameters!.Get<string>("p2"));
        }

        [Fact]
        public void Build_CompoundRecordQueryWithLastTransaction_GeneratesCombinedWhereClause()
        {
            var qb = new QueryBuilder("", "");
            var inner = new SearchClauseGroup
            {
                LogicalOperator = SearchLogicalOperator.And,
                Children =
                {
                    new SearchClause { LogicalOperator = SearchLogicalOperator.And, ColumnName = "F1063", Operator = SearchComparisonOperator.Equals, Value = "101" },
                    new SearchClause { LogicalOperator = SearchLogicalOperator.Or, ColumnName = "F1064", Operator = SearchComparisonOperator.Contains, Value = "Void" }
                }
            };

            var criteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Table,
                Value = "SAL_REG",
                ShowFields = false,
                LastTransaction = true,
                RootGroup = new SearchClauseGroup { Children = { inner } }
            };

            var result = qb.Build(criteria);

            Assert.Contains("SELECT * FROM [SAL_REG] WHERE F1032 = (SELECT DISTINCT TOP 1 F1032 FROM [SAL_REG] ORDER BY F1032 DESC) AND (([F1063] = @p0 OR [F1064] LIKE @p1))", result.Sql);
            Assert.Equal("101", result.Parameters!.Get<string>("p0"));
            Assert.Equal("%Void%", result.Parameters!.Get<string>("p1"));
        }

        [Fact]
        public void Build_DoesNotContain_GeneratesNotLikeForMetadataAndRecordQueries()
        {
            var qb = new QueryBuilder("", "");

            var metaCriteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Description,
                ShowFields = true,
                RootGroup = new SearchClauseGroup
                {
                    Children =
                    {
                        new SearchClause { Field = SearchFieldTarget.Description, Operator = SearchComparisonOperator.DoesNotContain, Value = "Temp" }
                    }
                }
            };

            var metaResult = qb.Build(metaCriteria);
            Assert.Contains("WHERE COALESCE(RBF.F1454, '') NOT LIKE @p0", metaResult.Sql);
            Assert.Equal("%Temp%", metaResult.Parameters!.Get<string>("p0"));

            var recCriteria = new SearchCriteria
            {
                Mode = SearchMode.Field,
                Type = SearchType.Table,
                Value = "SAL_REG",
                ShowFields = false,
                RootGroup = new SearchClauseGroup
                {
                    Children =
                    {
                        new SearchClause { ColumnName = "F1064", Operator = SearchComparisonOperator.DoesNotContain, Value = "Void" }
                    }
                }
            };

            var recResult = qb.Build(recCriteria);
            Assert.Contains("WHERE ([F1064] NOT LIKE @p0 OR [F1064] IS NULL)", recResult.Sql);
            Assert.Equal("%Void%", recResult.Parameters!.Get<string>("p0"));
        }
    }
}

