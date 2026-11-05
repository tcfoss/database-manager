using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Postgres.Lexing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgLexerTests
{
    [Theory]
    [InlineData("caf\u00e9")]
    [InlineData("\u00e9clair")]
    [InlineData("\u540d\u524d")]
    [InlineData("a$b")]
    [InlineData("name$tag$body$tag$")]
    public void Unquoted_Identifiers_Preserve_Dialect_Characters(string identifier)
    {
        List<Token> tokens = new PgLexer().Tokenize(identifier + ";");

        Assert.Equal(2, tokens.Count);
        Assert.Equal(identifier, Assert.IsType<Word>(tokens[0]).Value);
        Assert.Equal(identifier.Length, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(identifier.Length + 1, tokens[1].Location.Column);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "SELECT 'it''s fine';")]
    [InlineData("body", "BEGIN\nRETURN '$other$';\nEND")]
    [InlineData("_tag123", "$TAG$ and $$ and $body$")]
    [InlineData("Tag", "$tag$ is not the closing delimiter")]
    [InlineData("caf\u00e9", "Unicode tag")]
    [InlineData("\u540d\u524d", "Non-Latin tag")]
    public void DollarStrings_PreserveContentDelimiterAndSourceLength(string tag, string value)
    {
        string literal = $"${tag}${value}${tag}$";
        List<Token> tokens = new PgLexer().Tokenize(" \n" + literal + ";");
        var token = Assert.IsType<DollarQuotedStringLiteral>(tokens[0]);

        Assert.Equal(tag, token.Tag);
        Assert.Equal(value, token.Value);
        Assert.Equal($"${tag}$", token.Delimiter);
        Assert.Equal(literal, token.ToString());
        Assert.Equal(literal.Length, token.Length);
        Assert.Equal(2, token.Location.Position);
        Assert.Equal(2, token.Location.Line);
        Assert.Equal(literal.Length + 2, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
    }

    [Theory]
    [InlineData("$")]
    [InlineData("$1$body$1$")]
    [InlineData("$tag-name$body$tag-name$")]
    [InlineData("$tag")]
    public void InvalidDollarOpeners_AreRejected(string sql)
    {
        Assert.Throws<LexException.UnexpectedCharacter>(() => new PgLexer().Tokenize(sql));
    }

    [Theory]
    [InlineData("$$")]
    [InlineData("$$body")]
    [InlineData("$tag$body$TAG$")]
    public void UnterminatedDollarStrings_AreRejected(string sql)
    {
        var lexer = new PgLexer();
        Assert.Throws<LexException.UnterminatedString>(() => lexer.Tokenize(sql));
        Assert.Equal("ok", Assert.IsType<DollarQuotedStringLiteral>(lexer.Tokenize("$$ok$$")[0]).Value);
    }

    [Fact]
    public void DollarTokens_CompareTagAndContentButNotLocation()
    {
        var token = new DollarQuotedStringLiteral("tag", "value") { Location = new Location() };
        var same = new DollarQuotedStringLiteral("tag", "value") { Location = new Location(2, 3, 4) };
        Assert.True(token.Equals((object)same));
        Assert.True(token.Equals((Token)same));
        Assert.Equal(token.GetHashCode(), same.GetHashCode());
        Assert.False(token.Equals(new DollarQuotedStringLiteral("other", "value") { Location = new Location() }));
        Assert.False(token.Equals(new DollarQuotedStringLiteral("tag", "other") { Location = new Location() }));
        Assert.False(token.Equals((object?)null));
        Assert.False(token.Equals((Token)new StringLiteral("value") { Location = new Location() }));
    }

    [Fact]
    public void DollarValues_IgnoreTagInEqualityButPreserveItWhenWritten()
    {
        var value = new Value.DollarQuotedString("it''s \\ quoted");
        var tagged = value with { Tag = "body" };
        Assert.Equal(value, tagged);
        Assert.Equal(value.GetHashCode(), tagged.GetHashCode());
        Assert.NotEqual(value, tagged with { Value = "different" });
        Assert.False(value.Equals((Value.DollarQuotedString?)null));
        Assert.NotEqual<Value>(value, new Value.SingleQuotedString(value.Value));
        Assert.Equal("$$it''s \\ quoted$$", value.ToSql());
        Assert.Equal("$body$it''s \\ quoted$body$", tagged.ToSql());
    }

    [Fact]
    public void OrdinaryStrings_RetainEscapedTextWithoutDoubleEscaping()
    {
        const string sql = "'it''s fine'";
        var token = Assert.IsType<StringLiteral>(new PgLexer().Tokenize(sql)[0]);
        Assert.Equal("it''s fine", token.Value);
        Assert.Equal(sql, token.ToString());
        Assert.Equal(sql.Length, token.Length);
    }

    [Theory]
    [InlineData("\\")]
    [InlineData("before\\''after")]
    [InlineData("\\''")]
    [InlineData("line\\\n''tail\\")]
    public void Ordinary_Strings_Treat_Backslashes_As_Content(string contents)
    {
        string sql = $"'{contents}';";
        List<Token> tokens = new PgLexer().Tokenize(sql);

        Assert.Equal(contents, Assert.IsType<StringLiteral>(tokens[0]).Value);
        Assert.Equal(sql.Length - 1, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(224)]
    [InlineData(225)]
    [InlineData(300)]
    public void Long_Escaped_Strings_Preserve_Backslashes_And_Locations(int prefixLength)
    {
        string contents = new string('a', prefixLength) + "\\''line\n" + new string('b', 1024) + "\\";
        string sql = $"'{contents}';";
        List<Token> tokens = new PgLexer().Tokenize(sql);

        Assert.Equal(contents, Assert.IsType<StringLiteral>(tokens[0]).Value);
        int position = sql.Length - 1;
        Assert.Equal(new Location(2, position - sql.LastIndexOf('\n'), position), Assert.IsType<Semicolon>(tokens[1]).Location);
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public void Square_Brackets_Are_Symbol_Tokens()
    {
        List<Token> tokens = new PgLexer().Tokenize("a[1]");

        Assert.Equal(4, tokens.Count);
        var open = Assert.IsType<BracketOpen>(tokens[1]);
        var close = Assert.IsType<BracketClose>(tokens[3]);
        Assert.Equal(BracketOpen.TypeSymbol, open.ToString());
        Assert.Equal(BracketClose.TypeSymbol, close.ToString());
        Assert.Equal(new Location(1, 2, 1), open.Location);
        Assert.Equal(new Location(1, 4, 3), close.Location);
    }
}
