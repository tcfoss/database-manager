using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsSelectParserTests : ParserTestsBase<MsLexer, MsParser>
{
    [Theory]
    // Round-trip canonical form always uses parens around the TOP argument.
    [InlineData("SELECT TOP 10 * FROM t", "SELECT TOP (10) * FROM t", Label = "TOP without parens")]
    [InlineData("SELECT TOP (10) * FROM t", "SELECT TOP (10) * FROM t", Label = "TOP with parens")]
    [InlineData("SELECT TOP (10) PERCENT * FROM t", "SELECT TOP (10) PERCENT * FROM t", Label = "TOP PERCENT")]
    [InlineData("SELECT TOP (10) WITH TIES * FROM t ORDER BY x", "SELECT TOP (10) WITH TIES * FROM t ORDER BY x", Label = "TOP WITH TIES")]
    [InlineData("SELECT TOP (10) PERCENT WITH TIES * FROM t ORDER BY x", "SELECT TOP (10) PERCENT WITH TIES * FROM t ORDER BY x", Label = "TOP PERCENT WITH TIES")]
    [InlineData("SELECT DISTINCT TOP 5 col FROM t", "SELECT DISTINCT TOP (5) col FROM t", Label = "DISTINCT TOP")]
    public void Top_TextMatch(string text, string expectedSql)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: expectedSql, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SELECT * FROM a FULL OUTER JOIN b ON a.id = b.id", Label = "FULL OUTER JOIN")]
    [InlineData("SELECT * FROM a FULL JOIN b ON a.id = b.id", Label = "FULL JOIN")]
    [InlineData("SELECT * FROM a CROSS APPLY (SELECT * FROM b WHERE b.id = a.id) x", Label = "CROSS APPLY subquery")]
    [InlineData("SELECT * FROM a OUTER APPLY (SELECT * FROM b WHERE b.id = a.id) x", Label = "OUTER APPLY subquery")]
    public void Joins_Parse(string text)
    {
        var (_, actual) = GetExpectedActual(text, normalizeActualWhitespace: true);
        Assert.False(string.IsNullOrWhiteSpace(actual));
    }

    [Theory]
    [InlineData("SELECT ROW_NUMBER() OVER (PARTITION BY x ORDER BY y) AS rn FROM t", Label = "ROW_NUMBER PARTITION + ORDER")]
    [InlineData("SELECT ROW_NUMBER() OVER (ORDER BY y) AS rn FROM t", Label = "ROW_NUMBER ORDER only")]
    [InlineData("SELECT RANK() OVER (PARTITION BY x ORDER BY y) AS r FROM t", Label = "RANK OVER")]
    [InlineData("SELECT DENSE_RANK() OVER (ORDER BY y) AS r FROM t", Label = "DENSE_RANK OVER")]
    public void WindowFunctions_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("UPDATE t SET col = 1 FROM tbl AS t INNER JOIN other AS o ON o.id = t.id WHERE t.x = 1", Label = "UPDATE FROM with JOIN")]
    [InlineData("UPDATE t SET col = 1 FROM tbl AS t WHERE t.x = 1", Label = "UPDATE FROM simple")]
    [InlineData("UPDATE t SET t.col = 1, t.other_col = 2 FROM tbl AS t WHERE t.x = 1", Label = "UPDATE with target alias")]
    public void UpdateFrom_Parses(string text)
    {
        var (_, actual) = GetExpectedActual(text, normalizeActualWhitespace: true);
        Assert.False(string.IsNullOrWhiteSpace(actual));
    }

    [Theory]
    [InlineData("DELETE t FROM tbl AS t INNER JOIN other AS o ON o.id = t.id WHERE t.x = 1", Label = "DELETE alias FROM with JOIN")]
    [InlineData("DELETE FROM tbl WHERE x = 1", Label = "DELETE FROM simple")]
    public void DeleteAlias_Parses(string text)
    {
        var (_, actual) = GetExpectedActual(text, normalizeActualWhitespace: true);
        Assert.False(string.IsNullOrWhiteSpace(actual));
    }

    [Theory]
    [InlineData("SELECT @x", Label = "Bare variable")]
    [InlineData("SELECT @a + @b", Label = "Variable arithmetic")]
    [InlineData("SELECT @x FROM t WHERE c = @y", Label = "Variables in select and where")]
    public void Variables_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SELECT * FROM t ORDER BY id OFFSET 10 ROWS", "SELECT * FROM t ORDER BY id OFFSET 10 ROWS", Label = "OFFSET only")]
    [InlineData("SELECT * FROM t ORDER BY id OFFSET 0 ROW", "SELECT * FROM t ORDER BY id OFFSET 0 ROWS", Label = "OFFSET ROW singular")]
    [InlineData("SELECT * FROM t ORDER BY id OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY", "SELECT * FROM t ORDER BY id OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY", Label = "OFFSET FETCH NEXT")]
    [InlineData("SELECT * FROM t ORDER BY id OFFSET 5 ROWS FETCH FIRST 10 ROW ONLY", "SELECT * FROM t ORDER BY id OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY", Label = "OFFSET FETCH FIRST normalized")]
    [InlineData("SELECT * FROM t ORDER BY id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", "SELECT * FROM t ORDER BY id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", Label = "OFFSET FETCH with variables")]
    public void OffsetFetch_TextMatch(string text, string expectedSql)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: expectedSql, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }
}
