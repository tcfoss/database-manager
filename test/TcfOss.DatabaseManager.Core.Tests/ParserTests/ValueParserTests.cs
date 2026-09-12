using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

// ReSharper disable once ClassNeverInstantiated.Global
public class ValueParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    [InlineData("NULL")]
    [InlineData("3")]
    [InlineData("3.14")]
    [InlineData("3L")]
    [InlineData("'astring'")]
    [InlineData("N'anationalstring'")]
    [InlineData("X'4F2A'")]
    public static void ParseValue_TextMatch(string sql)
    {
        var (expected, actual) = GetExpectedActual(ValueParser.ParseValue, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("rawstring", "rawstring")]
    [InlineData("'a quoted string'", "a quoted string")]
    public static void ParseLiteralString_Match(string sql, string expected)
    {
        var state = GetState(sql);

        Assert.Equal(expected, ValueParser.ParseLiteralString(state));
    }

    [Theory]
    [InlineData("3", null)]
    [InlineData("3.14", null)]
    [InlineData("SELECT", null)]
    [InlineData("  ", "end of input")]
    public static void ParseLiteralString_Fail(string sql, string? found)
    {
        var state = GetState(sql);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ValueParser.ParseLiteralString(state));
        var expectedTemplate = "Expected {0}. Found {1}.";
        found = found == null
            ? sql.Trim().Split(' ')[0]
            : found.Italic();
        var expectedMessage = string.Format(expectedTemplate, "literal_string".Italic(), found);
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("9", 9)]
    public static void ParseLiteralUInt_Match(string sql, uint expected)
    {
        var state = GetState(sql);

        Assert.Equal(expected, ValueParser.ParseLiteralUInt(state));
    }

    [Theory]
    [InlineData("3.24")]
    [InlineData("SELECT")]
    [InlineData("-3")]
    [InlineData("'string'")]
    public static void ParseLiteralUInt_Fail(string sql)
    {
        var state = GetState(sql);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ValueParser.ParseLiteralUInt(state));
        var expectedTemplate = "Expected {0}. Found {1}.";
        var found = sql.StartsWith('-') ? "-" : sql;
        var expectedMessage = string.Format(expectedTemplate, "literal_int".Italic(), found);
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("3", false, 3)]
    [InlineData("3L", true, 3)]
    [InlineData("1234789123", false, 1234789123)]
    [InlineData("1234789123L", true, 1234789123)]
    public static void ParseLiteralNumbers(string sql, bool isLong, int number)
    {
        var state = GetState(sql);

        var actual = ValueParser.ParseValue(state);
        var expected = new Value.Number(sql.EndsWith('L') ? sql[..^1] : sql, isLong);
        Assert.Equal(expected, actual);

        var actualAsNumber = actual as Value.Number;
        Assert.NotNull(actualAsNumber);

        Assert.Equal(number, actualAsNumber.AsInt());
    }

    [Fact]
    public static void Parse_LiteralNumber_TooBig_ReturnNull()
    {
        var state = GetState("9223372036854775808L");

        var actual = ValueParser.ParseValue(state);
        var expected = new Value.Number("9223372036854775808", true);

        Assert.Equal(expected, actual);

        var actualAsNumber = actual as Value.Number;
        Assert.NotNull(actualAsNumber);

        Assert.Null(actualAsNumber.AsInt());
    }
}
