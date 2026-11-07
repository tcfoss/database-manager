using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgValueParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("0x10", 16)]
    [InlineData("0o20", 16)]
    [InlineData("0b1_0000", 16)]
    [InlineData("1_6", 16)]
    public void ExtendedNumbers_IntegerConsumersInterpretRadixAndSeparators(string sql, int expected)
    {
        Assert.Equal(expected, ValueParser.ParseLiteralInt(GetState(sql)));
        Assert.Equal((uint)expected, ValueParser.ParseLiteralUInt(GetState(sql)));
        Assert.Equal((long)expected, ValueParser.ParseLiteralLong(GetState(sql)));
        Assert.Equal((ulong)expected, ValueParser.ParseLiteralULong(GetState(sql)));
        Assert.Equal(expected, new Core.Common.Value.Number(sql, false).AsInt());
    }



    [Theory]
    [InlineData("-0x8000000000000000", long.MinValue)]
    [InlineData("+0x7fffffffffffffff", long.MaxValue)]
    [InlineData("-9_223_372_036_854_775_808", long.MinValue)]
    public void ExtendedNumbers_SignedIntegerConsumersRetainFullRange(string sql, long expected)
    {
        Assert.Equal(expected, ValueParser.ParseLiteralLong(GetState(sql)));
    }

    [Theory]
    [InlineData("0x8000000000000000")]
    [InlineData("-0x8000000000000001")]
    public void ExtendedNumbers_IntegerOverflowUsesExistingRangeDiagnostics(string sql)
    {
        Assert.Throws<ParseException.ValueOutOfRange>(() =>
            ValueParser.ParseLiteralLong(GetState(sql)));
    }
}
