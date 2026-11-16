using System.Collections.Generic;
using SMS_Search.Models.Gs1;
using SMS_Search.Services.Gs1;
using Xunit;

namespace SMSSearch.Tests
{
    public class Gs1ParserTests
    {
        [Fact]
        public void Parse_EmptyBarcode_ReturnsInvalidResult()
        {
            var parser = new Gs1Parser();
            var result = parser.Parse("", new List<Gs1AiDefinition>());

            Assert.False(result.IsValid);
            Assert.Equal("Barcode is empty.", result.ErrorMessage);
        }

        [Fact]
        public void Parse_ValidParenthesizedGtin_ParsesAi01()
        {
            var parser = new Gs1Parser();
            var defs = new List<Gs1AiDefinition>
            {
                new Gs1AiDefinition { Ai = "01", Title = "GTIN", MinLength = 14, MaxLength = 14, IsVariableLength = false }
            };

            var result = parser.Parse("(01)00012345678905", defs);

            Assert.Single(result.ParsedAis);
            Assert.Equal("01", result.ParsedAis[0].Ai);
            Assert.Equal("00012345678905", result.ParsedAis[0].RawValue);
        }
    }
}

