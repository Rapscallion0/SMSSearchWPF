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
    }
}

