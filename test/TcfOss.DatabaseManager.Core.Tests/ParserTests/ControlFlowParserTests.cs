using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

public class ControlFlowParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("  IF 1 = 1 SELECT 1", "IF syntax", 1)]
    [InlineData("  DECLARE myvar INT", "Local variable declaration", 1)]
    [InlineData("  DECLARE mycon CONDITION", "Local variable declaration", 1)]
    [InlineData("\n  IF 1 = 1 SELECT 1", "IF syntax", 2)]
    [InlineData("\n  DECLARE myvar INT", "Local variable declaration", 2)]
    public void DialectSpecificSyntax_ThrowsLocatedParseException(string sql, string syntax, int line)
    {
        var exception = Assert.Throws<ParseException.DialectRequired>(() => new Parser().Parse(GetTokens(sql)));

        Assert.Contains(syntax + " requires a dialect-specific parser.", exception.Message);
        Assert.Contains("Select a SQL dialect", exception.Message);

        var formatted = new ParseException.WithText(exception, "routine.sql", sql);

        Assert.Contains($"In file 'routine.sql' at line {line}, column 3", formatted.Message);
        Assert.Contains("\n  ^", formatted.Message);
        Assert.Same(exception, formatted.InnerException);
    }

    [Theory]
    [InlineData("DECLARE")]
    [InlineData("DECLARE +")]
    [InlineData("DECLARE INVALID HANDLER FOR SQLWARNING SELECT 1")]
    public void InvalidHandlerAction_Throws(string sql)
    {
        var parser = new Parser();

        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() =>
            parser.ControlFlowParser.ParseDeclareConditionHandler(new ParserState(GetTokens(sql))));

        Assert.Contains("CONTINUE", exception.Message);
        Assert.Contains("EXIT", exception.Message);
        Assert.Contains("UNDO", exception.Message);
    }

    [Fact]
    public void InvalidConditionValue_Throws()
    {
        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() =>
            new Parser().Parse(GetTokens("DECLARE mycon CONDITION FOR UNKNOWN")));

        Assert.Contains("SQLSTATE", exception.Message);
    }

    [Theory]
    [InlineData("DECLARE mycon CONDITION FOR SQLSTATE VALUE 'ABCDE'", "DECLARE mycon CONDITION FOR SQLSTATE VALUE 'ABCDE'")]
    [InlineData("DECLARE CONTINUE HANDLER FOR SQLSTATE VALUE 'ABCDE' SELECT 1", "DECLARE CONTINUE HANDLER FOR SQLSTATE 'ABCDE' SELECT 1")]
    public void Statement_TextMatch(string sql, string expectedSql)
    {
        var (expected, actual) = GetExpectedActual(new Parser().Parse, sql, expectedSql);

        Assert.Equal(expected, actual);
    }
}
