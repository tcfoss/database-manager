using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsMessageParserTests : ParserTestsBase<MsLexer, MsParser>
{
    [Theory]
    [InlineData("PRINT 'hello'", Label = "PRINT string literal")]
    [InlineData("PRINT @msg", Label = "PRINT variable")]
    [InlineData("PRINT 'value: ' + CAST(@x AS VARCHAR(10))", Label = "PRINT expression")]
    public void Print_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("THROW", Label = "Bare THROW (rethrow)")]
    [InlineData("THROW 50000, 'oops', 1", Label = "THROW with arguments")]
    [InlineData("THROW @num, @msg, @state", Label = "THROW with variables")]
    public void Throw_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("RAISERROR('oops', 16, 1)", Label = "RAISERROR no args")]
    [InlineData("RAISERROR('hello %s', 16, 1, @name)", Label = "RAISERROR with one arg")]
    [InlineData("RAISERROR(@msg, @sev, @st, @a, @b)", Label = "RAISERROR all variables")]
    public void RaiseError_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryCatchWithThrow_RoundTrips()
    {
        var text = "BEGIN TRY SELECT 1; END TRY BEGIN CATCH PRINT 'caught'; THROW; END CATCH";
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }
}
