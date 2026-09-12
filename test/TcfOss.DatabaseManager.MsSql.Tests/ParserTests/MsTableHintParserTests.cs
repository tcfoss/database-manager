using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsTableHintParserTests : ParserTestsBase<MsLexer, MsParser>
{
    [Theory]
    [InlineData("SELECT * FROM t WITH (NOLOCK)", Label = "Single NOLOCK hint")]
    [InlineData("SELECT * FROM t AS x WITH (NOLOCK)", Label = "Aliased table with hint")]
    [InlineData("SELECT * FROM t WITH (NOLOCK, READPAST)", Label = "Multiple simple hints")]
    [InlineData("SELECT * FROM t WITH (HOLDLOCK, ROWLOCK, UPDLOCK)", Label = "Three lock hints")]
    [InlineData("SELECT * FROM t WITH (INDEX(idx1))", Label = "INDEX hint single")]
    [InlineData("SELECT * FROM t WITH (INDEX(idx1, idx2))", Label = "INDEX hint multi")]
    [InlineData("SELECT * FROM t WITH (NOLOCK, INDEX(idx1))", Label = "Mixed hints")]
    [InlineData("SELECT a FROM t1 AS x WITH (NOLOCK) INNER JOIN t2 AS y WITH (READPAST) ON x.id = y.id", Label = "Hints on joined tables")]
    public void Select_TableHintTextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("UPDATE t WITH (TABLOCK) SET a = 1", Label = "UPDATE with table hint")]
    [InlineData("DELETE FROM t WITH (NOLOCK) WHERE id = 1", Label = "DELETE with table hint")]
    public void Dml_TableHintTextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }
}
