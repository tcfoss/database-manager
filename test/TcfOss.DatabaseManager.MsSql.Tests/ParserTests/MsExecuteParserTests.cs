using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;
using TcfOss.DatabaseManager.MsSql.Statements;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsExecuteParserTests : ParserTestsBase<MsLexer, MsParser>
{
    [Theory]
    [InlineData("EXEC dbo.MyProc", Label = "EXEC no args")]
    [InlineData("EXECUTE dbo.MyProc", Label = "EXECUTE no args")]
    [InlineData("EXEC MyProc 1, 2, 3", Label = "Positional args")]
    [InlineData("EXEC dbo.MyProc @a = 1, @b = N'x'", Label = "Named args")]
    [InlineData("EXEC dbo.MyProc @a = @v OUTPUT", Label = "OUTPUT named")]
    [InlineData("EXEC dbo.MyProc @v OUTPUT", Label = "OUTPUT positional")]
    [InlineData("EXEC dbo.MyProc @a = DEFAULT", Label = "DEFAULT named")]
    [InlineData("EXEC dbo.MyProc DEFAULT, 2", Label = "DEFAULT positional")]
    [InlineData("EXEC @ret = dbo.MyProc 1", Label = "Return-status capture")]
    [InlineData("EXECUTE @ret = dbo.MyProc @a = 1, @b = @v OUTPUT", Label = "Full form")]
    public void Execute_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("EXEC ('SELECT 1')", Label = "Dynamic SQL literal")]
    [InlineData("EXECUTE (@sql)", Label = "Dynamic SQL variable")]
    public void Execute_DynamicSql_Parses(string text)
    {
        var (_, actual) = GetExpectedActual(text, normalizeActualWhitespace: true);
        Assert.False(string.IsNullOrWhiteSpace(actual));
    }

    [Fact]
    public void Execute_OutKeyword_Accepted()
    {
        // T-SQL accepts OUT as a synonym for OUTPUT. Round-trip
        // canonicalizes to OUTPUT.
        var (_, actual) = GetExpectedActual("EXEC p @a = @v OUT", normalizeActualWhitespace: true);
        Assert.Contains("OUTPUT", actual);
    }

    [Fact]
    public void Execute_TerminatesBeforeNextStatement()
    {
        // No semicolon between EXEC (no args) and the SELECT — the EXEC
        // parser must not consume the SELECT as an argument list.
        var state = GetState("EXEC dbo.MyProc\nGO\nSELECT 1");
        var parser = new MsParser();
        var statements = parser.Parse(state, null);

        Assert.Equal(3, statements.Count);
        Assert.IsType<MsExecute.ProcedureCall>(statements[0]);
        Assert.IsType<MsBatchSeparator>(statements[1]);
    }

    [Fact]
    public void Execute_InsideBeginEnd()
    {
        var state = GetState("BEGIN EXEC dbo.MyProc; END");
        var parser = new MsParser();
        var statements = parser.Parse(state, null);

        Assert.Single(statements);
    }
}
