using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgComponentParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("dog")]
    [InlineData("Dog")]
    [InlineData("_dog")]
    [InlineData("dog2")]
    [InlineData("\"Dog\"")]
    [InlineData("\"2\"")]
    [InlineData("\"@dog\"")]
    [InlineData("\"#dog\"")]
    public void Identifiers_PreserveSpellingQuotingAndSource(string sql)
    {
        var state = new ParserState(GetTokens(sql), sourceId: 7);
        Identifier identifier = new PgParser().ComponentParser.ParseIdentifier(state);

        Assert.Equal(sql, identifier.ToSql());
        Assert.Equal(new SourceRef(7, 0, sql.Length), identifier.Source);
        Assert.IsType<Eof>(state.Peek());
    }

    [Theory]
    [InlineData("'dog'")]
    [InlineData("[dog]")]
    [InlineData("`dog`")]
    [InlineData("@dog")]
    [InlineData("#dog")]
    [InlineData("\"\"")]
    [InlineData("2")]
    [InlineData("*")]
    public void IdentifierOverloads_ApplyPostgresRules(string sql)
    {
        ComponentParser parser = new PgParser().ComponentParser;

        var defaultException = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ParseIdentifier(GetState(sql)));
        var labelledException = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ParseIdentifier(GetState(sql), "column_name"));

        Assert.Contains("identifier", defaultException.Message);
        Assert.Contains("column_name", labelledException.Message);
    }

    [Theory]
    [InlineData("app.mood")]
    [InlineData("\"App\".\"Mood\"")]
    [InlineData("app.\"Mood\"")]
    public void CoreObjectNameOverloads_UsePostgresIdentifiers(string sql)
    {
        ComponentParser parser = new PgParser().ComponentParser;
        ObjectName defaultName = parser.ParseObjectName(GetState(sql));
        ObjectName labelledName = parser.ParseObjectName(GetState(sql), "data_type");

        Assert.Equal(sql, defaultName.ToSql());
        Assert.Equal(sql, labelledName.ToSql());
        Assert.All(labelledName.Values, identifier => Assert.NotNull(identifier.Source));
    }

    [Theory]
    [InlineData("app.'mood'")]
    [InlineData("app.[mood]")]
    [InlineData("app.`mood`")]
    [InlineData("app.@mood")]
    [InlineData("app.#mood")]
    [InlineData("app.\"\"")]
    [InlineData("app.")]
    [InlineData("app..mood")]
    public void ObjectNameValidation_UsesRequestedLabel(string sql)
    {
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => new PgParser().ComponentParser.ParseObjectName(GetState(sql), "data_type"));

        Assert.Contains("data_type", exception.Message);
    }

    [Theory]
    [InlineData("CREATE TABLE 'sample' (value INT)")]
    [InlineData("CREATE TABLE [sample] (value INT)")]
    [InlineData("CREATE TABLE sample ('value' INT)")]
    [InlineData("CREATE FUNCTION 'echo' (value INT) RETURNS INT RETURN value")]
    [InlineData("SELECT * FROM 'sample'")]
    [InlineData("INSERT INTO 'sample' VALUES (1)")]
    public void Subparsers_UsePostgresComponentParser(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }
}
