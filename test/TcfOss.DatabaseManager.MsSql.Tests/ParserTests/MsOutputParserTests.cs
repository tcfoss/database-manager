using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsOutputParserTests : ParserTestsBase<MsLexer, MsParser>
{
    [Theory]
    [InlineData(
        "INSERT INTO t (a, b) OUTPUT inserted.a, inserted.b VALUES (1, 2)",
        Label = "INSERT with OUTPUT")]
    [InlineData(
        "INSERT INTO t (a) OUTPUT inserted.* VALUES (1)",
        Label = "INSERT with OUTPUT inserted.*")]
    [InlineData(
        "INSERT INTO t (a, b) OUTPUT inserted.a AS a, inserted.b AS b VALUES (1, 2)",
        Label = "INSERT with OUTPUT and column aliases")]
    [InlineData(
        "INSERT INTO t (a) OUTPUT inserted.a + 1 AS next_a VALUES (1)",
        Label = "INSERT with OUTPUT scalar expression")]
    public void Insert_OutputTextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(
        "UPDATE t SET a = 1 OUTPUT inserted.a, deleted.a WHERE id = 1",
        Label = "UPDATE with OUTPUT before WHERE")]
    [InlineData(
        "UPDATE t SET a = 1 OUTPUT inserted.* FROM t2 WHERE t.id = t2.id",
        Label = "UPDATE with OUTPUT before FROM")]
    public void Update_OutputTextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(
        "DELETE FROM t OUTPUT deleted.a WHERE id = 1",
        Label = "DELETE with OUTPUT before WHERE")]
    [InlineData(
        "DELETE FROM t OUTPUT deleted.*",
        Label = "DELETE with OUTPUT no WHERE")]
    public void Delete_OutputTextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Insert_Output_PlainWildcard_Throws()
    {
        var text = "INSERT INTO t (a, b) OUTPUT * VALUES (1, 2)";
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true));
        Assert.Contains("identifier or qualified wildcard", ex.Message);
    }
}
