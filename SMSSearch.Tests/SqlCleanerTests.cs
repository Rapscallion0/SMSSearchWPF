using SMS_Search.Data;
using Xunit;

namespace SMSSearch.Tests
{
    public class SqlCleanerTests
    {
        [Fact]
        public void DefaultRules_ContainsExpectedHtmlAndBracketRules()
        {
            var rules = SqlCleaner.DefaultRules;

            Assert.NotEmpty(rules);
            Assert.Contains(rules, r => r.Pattern == "&amp;" && r.Replacement == "&");
            Assert.Contains(rules, r => r.Pattern == "&lt;" && r.Replacement == "<");
            Assert.Contains(rules, r => r.Pattern == "&gt;" && r.Replacement == ">");
            Assert.Contains(rules, r => r.Pattern == "&quot;" && r.Replacement == "'");
        }
    }
}

