using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
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
    [InlineData("$$a dollar quoted string$$", "a dollar quoted string")]
    [InlineData("$tag$a tagged dollar quoted string$tag$", "a tagged dollar quoted string")]
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
    [InlineData("$$a dollar quoted string$$", "a dollar quoted string")]
    [InlineData("$tag$a tagged dollar quoted string$tag$", "a tagged dollar quoted string")]
    [InlineData("'a quoted string'", "a quoted string")]
    [InlineData("rawstring", "rawstring")]
    [InlineData("SELECT", "SELECT")]
    public static void ParseLiteralStringOptionalQuotes_Match(string sql, string expected)
    {
        var state = GetState(sql);

        Assert.Equal(expected, ValueParser.ParseLiteralStringOptionalQuotes(state));
    }

    [Theory]
    [InlineData("3", null)]
    [InlineData("3.14", null)]
    [InlineData("  ", "end of input")]
    public static void ParseLiteralStringOptionalQuotes_Fail(string sql, string? found)
    {
        var state = GetState(sql);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ValueParser.ParseLiteralStringOptionalQuotes(state));
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
    [InlineData("0", 0)]
    [InlineData("4294967295", uint.MaxValue)]
    public static void ParseLiteralUInt_Match(string sql, uint expected)
    {
        var state = GetState(sql);

        Assert.Equal(expected, ValueParser.ParseLiteralUInt(state));
    }

    [Theory]
    [InlineData("3.24")]
    [InlineData("SELECT")]
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
    [InlineData("0", 0)]
    [InlineData("-3", -3)]
    [InlineData("+3", 3)]
    [InlineData("- 3", -3)]
    [InlineData("-2147483648", int.MinValue)]
    [InlineData("2147483647", int.MaxValue)]
    public void ParseLiteralInt_Match(string sql, int expected)
    {
        Assert.Equal(expected, ValueParser.ParseLiteralInt(GetState(sql)));
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("-3", -3L)]
    [InlineData("+3", 3L)]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void ParseLiteralLong_Match(string sql, long expected)
    {
        Assert.Equal(expected, ValueParser.ParseLiteralLong(GetState(sql)));
    }

    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void ParseLiteralULong_Match(string sql, ulong expected)
    {
        Assert.Equal(expected, ValueParser.ParseLiteralULong(GetState(sql)));
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("999999999999999999999999")]
    public void ParseLiteralInt_Overflow(string sql)
    {
        var exception = Assert.Throws<ParseException.ValueOutOfRange>(() => ValueParser.ParseLiteralInt(GetState(sql)));

        Assert.Equal(sql, exception.Value);
        Assert.Equal(int.MinValue, exception.Minimum);
        Assert.Equal(int.MaxValue, exception.Maximum);
    }

    [Theory]
    [InlineData("4294967296")]
    public void ParseLiteralUInt_OutOfRange(string sql)
    {
        var exception = Assert.Throws<ParseException.ValueOutOfRange>(() => ValueParser.ParseLiteralUInt(GetState(sql)));

        Assert.Equal(sql, exception.Value);
        Assert.Equal(0, exception.Minimum);
        Assert.Equal(uint.MaxValue, exception.Maximum);
    }

    [Theory]
    [InlineData("+3")]
    [InlineData("-3")]
    [InlineData("+ 3")]
    [InlineData("- 3")]
    public void UnsignedIntegerLiterals_RejectSeparateSignTokens(string sql)
    {
        var uintState = GetState(sql);
        var ulongState = GetState(sql);

        Assert.Throws<ParseException.ExpectedButFound>(() => ValueParser.ParseLiteralUInt(uintState));
        Assert.Throws<ParseException.ExpectedButFound>(() => ValueParser.ParseLiteralULong(ulongState));
        Assert.IsType<NumericLiteral>(uintState.Peek());
        Assert.IsType<NumericLiteral>(ulongState.Peek());
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    public void ParseLiteralLong_Overflow(string sql)
    {
        var exception = Assert.Throws<ParseException.ValueOutOfRange>(() => ValueParser.ParseLiteralLong(GetState(sql)));

        Assert.Equal(sql, exception.Value);
        Assert.Equal(long.MinValue, exception.Minimum);
        Assert.Equal(long.MaxValue, exception.Maximum);
    }

    [Theory]
    [InlineData("3.24")]
    [InlineData("SELECT")]
    [InlineData("'string'")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("-+3")]
    public void SignedIntegerLiterals_RejectInvalidSyntax(string sql)
    {
        Assert.Throws<ParseException.ExpectedButFound>(() => ValueParser.ParseLiteralInt(GetState(sql)));
        Assert.Throws<ParseException.ExpectedButFound>(() => ValueParser.ParseLiteralLong(GetState(sql)));
    }

    [Theory]
    [InlineData("+3", 3)]
    [InlineData("-3", -3)]
    public void SignedNumericTokens_AreInterpretedByTryParse(string text, int expected)
    {
        var token = new NumericLiteral(text) { Location = default };

        Assert.Equal(expected, ValueParser.ParseLiteralInt(new ParserState([token])));
        Assert.Equal(expected, ValueParser.ParseLiteralLong(new ParserState([token])));
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
