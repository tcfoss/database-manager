using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsControlFlowParserTests : ParserTestsBase<MsLexer, MsParser>
{
    [Theory]
    [InlineData("DECLARE @i INT", Label = "Single, no default")]
    [InlineData("DECLARE @i INT = 0", Label = "Single, with default")]
    [InlineData("DECLARE @a INT, @b INT", Label = "Two, no defaults")]
    [InlineData("DECLARE @a INT = 1, @b VARCHAR(10) = 'x'", Label = "Two, mixed types and defaults")]
    public void Declare_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SET @x = 1", Label = "Numeric literal")]
    [InlineData("SET @x = @y + 1", Label = "Expression with variable")]
    [InlineData("SET @x = 'hello'", Label = "String literal")]
    public void Set_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("IF @x = 1 SET @y = 2", Label = "Simple IF, single statement")]
    [InlineData("IF @x = 1 SET @y = 2 ELSE SET @y = 3", Label = "IF / ELSE")]
    [InlineData("IF @x = 1 BEGIN SET @y = 2; SET @z = 3; END", Label = "IF with BEGIN/END block")]
    [InlineData("IF @x = 1 BEGIN SET @y = 2; END ELSE BEGIN SET @y = 3; END", Label = "IF/ELSE with BEGIN/END")]
    public void If_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("WHILE @i < 10 SET @i = @i + 1", Label = "Simple WHILE")]
    [InlineData("WHILE @i < 10 BEGIN SET @i = @i + 1; END", Label = "WHILE with BEGIN/END")]
    [InlineData("WHILE @i < 10 BEGIN IF @i = 5 BREAK; SET @i = @i + 1; END", Label = "WHILE with BREAK")]
    [InlineData("WHILE @i < 10 BEGIN IF @i = 5 CONTINUE; SET @i = @i + 1; END", Label = "WHILE with CONTINUE")]
    public void While_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("RETURN", Label = "Bare RETURN")]
    [InlineData("RETURN 0", Label = "RETURN literal")]
    [InlineData("RETURN @x", Label = "RETURN variable")]
    [InlineData("RETURN @x + 1", Label = "RETURN expression")]
    public void Return_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ProcedureBody_FullRoundTrip()
    {
        var text = """
        BEGIN
            DECLARE @i INT = 0, @sum INT = 0;
            WHILE @i < 10
            BEGIN
                IF @i = 5 BREAK;
                IF @i = 3
                BEGIN
                    SET @i = @i + 1; CONTINUE;
                END;

                SET @sum = @sum + @i;
                SET @i = @i + 1;
            END;
            IF @sum > 0 SET @sum = @sum * 2 ELSE SET @sum = -1;
            RETURN @sum;
        END
        """;
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }
}
