using TcfOss.DatabaseManager.Core.IO;
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
    [InlineData(";")]
    [InlineData(")")]
    [InlineData("GO")]
    [InlineData("END")]
    [InlineData("ELSE")]
    [InlineData("SELECT")]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("EXEC")]
    [InlineData("EXECUTE")]
    [InlineData("CREATE")]
    [InlineData("ALTER")]
    [InlineData("DROP")]
    [InlineData("TRUNCATE")]
    [InlineData("DECLARE")]
    [InlineData("SET")]
    [InlineData("IF")]
    [InlineData("WHILE")]
    [InlineData("BEGIN")]
    [InlineData("RETURN")]
    [InlineData("USE")]
    [InlineData("OPEN")]
    [InlineData("CLOSE")]
    [InlineData("FETCH")]
    [InlineData("BREAK")]
    [InlineData("CONTINUE")]
    public void ReturnAndThrow_TerminatorsRemainUnconsumed(string terminator)
    {
        string[] keywords = ["RETURN", "THROW"];
        foreach (string keyword in keywords)
        {
            var state = GetState($"{keyword}\n{terminator}");
            var parser = new MsParser();
            var next = state.PeekNth(1);

            var statement = parser.ParseStatement(state);

            Assert.Equal(keyword, statement.ToSql());
            Assert.Same(next, state.Peek());
        }
    }

    [Theory]
    [InlineData("RETURN result")]
    [InlineData("THROW error_number, error_message, error_state")]
    public void ReturnAndThrow_IdentifierExpressions_TextMatch(string text)
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
