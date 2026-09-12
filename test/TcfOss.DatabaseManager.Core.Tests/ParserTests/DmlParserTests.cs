using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

// ReSharper disable ClassNeverInstantiated.Global
public class DmlParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("INSERT mytable (col1, col2, col3) VALUES ('v1', 'v2', 'v3')")]
    [InlineData("INSERT INTO mytable (col1, col2, col3) VALUES ('v1', 'v2', 'v3')")]
    [InlineData("INSERT INTO mytable VALUES ('v1', 'v2', 'v3')")]
    [InlineData("INSERT INTO mytable (col1, col2, col3) VALUES ('v1', 'v2', 'v3'), ('v4', 'v5', 'v6'), ('v7', 'v8', 'v9')", Label = "INSERT multiple value-rows")]
    [InlineData("INSERT INTO mytable VALUES ('v1', 'v2', 'v3'), ('v4', 'v5', 'v6'), ('v7', 'v8', 'v9')", Label = "INSERT VALUES multiple")]
    [InlineData("INSERT INTO mytable (col1, col2, col3) SELECT a, b, c FROM mytable", Label = "INSERT SELECT simple")]
    [InlineData("INSERT INTO mytable SELECT a, b, c FROM mytable")]
    [InlineData("INSERT INTO mytable (col1, col2, col3) SELECT t.a, t.b, t.c FROM mytable AS t")]
    [InlineData("INSERT INTO mytable SELECT t.a, t.b, t.c FROM mytable AS t", Label = "INSERT SELECT with alias")]
    [InlineData("INSERT INTO mytable (col1, col2, col3) SELECT a, b, c FROM mytable ON DUPLICATE KEY UPDATE col2 = b, col3 = c", Label = "INSERT SELECT with ON DUPLICATE KEY UPDATE")]
    public static void Insert_Statement_TextMatch(string sql)
    {
        var parser = new Parser();
        var dmlParser = parser.DmlParser;

        var (expected, actual) = GetExpectedActual(s => dmlParser.ParseInsert(s, null), sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("UPDATE mytable SET col1 = 'val1'")]
    [InlineData("UPDATE mytable SET col1 = 'val1', col2 = 'val2'")]
    [InlineData("UPDATE mytable INNER JOIN othertable ON mytable.id = othertable.id SET col1 = 'val1'")]
    [InlineData("UPDATE mytable AS mt LEFT OUTER JOIN othertable AS ot ON mt.id = ot.id SET col1 = 'val1' WHERE ot.col2 IS NOT NULL", Label = "UPDATE with join and WHERE")]
    [InlineData("UPDATE mt SET mt.col1 = 'val1' FROM mytable AS mt WHERE mt.col2 IS NOT NULL")]
    [InlineData("UPDATE mytable SET (col1, col2, col3) = ('val1', 'val2', 3)")]
    public static void Update_Statement_TextMatch(string sql)
    {
        var parser = new Parser();
        var dmlParser = parser.DmlParser;

        var (expected, actual) = GetExpectedActual(s => dmlParser.ParseUpdate(s, null), sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("DELETE FROM mytable")]
    [InlineData("DELETE mt FROM mytable AS mt")]
    [InlineData("DELETE mt, ot FROM mytable AS mt INNER JOIN othertable AS ot ON mt.id = ot.id")]
    [InlineData("DELETE FROM mytable WHERE col1 < 10")]
    [InlineData("DELETE FROM mytable ORDER BY col1 LIMIT 100")]
    [InlineData("DELETE FROM mytable WHERE col1 < 10 RETURNING col2, col3")]
    [InlineData("DELETE FROM mytable AS mt WHERE mt.col1 < 10 RETURNING mt.col2, mt.col3")]
    [InlineData("DELETE FROM mytable WHERE col1 < 10 ORDER BY col2 DESC, col3 LIMIT 50 RETURNING col2, col3", Label = "DELETE ORDER BY LIMIT RETURNING")]
    public static void Delete_Statement_TextMatch(string sql)
    {
        var parser = new Parser();
        var dmlParser = parser.DmlParser;

        var (expected, actual) = GetExpectedActual(s => dmlParser.ParseDelete(s, null), sql);

        Assert.Equal(expected, actual);
    }
}
