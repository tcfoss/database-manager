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
}
