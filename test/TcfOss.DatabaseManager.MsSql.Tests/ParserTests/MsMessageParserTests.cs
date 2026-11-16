using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
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

    [Theory]
    [InlineData("CLOSE cursor_name", false)]
    [InlineData("CLOSE GLOBAL cursor_name", true)]
    public void Close_RetainsGlobalCursorScope(string sql, bool global)
    {
        var close = Assert.IsType<Close.Named>(ParseStatement(sql));

        Assert.Equal(global, close.Global);
        Assert.Equal(sql, close.ToSql());
        Assert.Equal(close, ParseStatement(close.ToSql()));
        Assert.Equal(close, ParseStatement(Format(close)));
    }

    [Theory]
    [InlineData("FETCH NEXT FROM GLOBAL cursor_name INTO @first, @second", true, true)]
    [InlineData("FETCH ABSOLUTE -2 FROM cursor_name INTO @first, @second", false, true)]
    [InlineData("FETCH RELATIVE @offset FROM cursor_name INTO @first, @second", false, true)]
    [InlineData("FETCH NEXT FROM cursor_name", false, false)]
    public void Fetch_RetainsDirectionConnectorGlobalScopeAndOptionalTargets(
        string sql, bool global, bool hasTargets)
    {
        var fetch = Assert.IsType<Fetch>(ParseStatement(sql));

        Assert.NotNull(fetch.Direction);
        Assert.Equal(CursorNameConnector.From, fetch.Connector);
        Assert.Equal(global, fetch.Global);
        Assert.Equal("cursor_name", fetch.CursorName.Name);
        if (hasTargets)
        {
            Assert.NotNull(fetch.Targets);
            Assert.Equal(["@first", "@second"], [.. fetch.Targets.Select(target => target.ToSql())]);
        }
        else
        {
            Assert.Null(fetch.Targets);
        }
        Assert.Equal(sql, fetch.ToSql());
        Assert.Equal(fetch, ParseStatement(fetch.ToSql()));
        Assert.Equal(fetch, ParseStatement(Format(fetch)));
    }
}
