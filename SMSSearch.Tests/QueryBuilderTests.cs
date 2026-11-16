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
    }
}

