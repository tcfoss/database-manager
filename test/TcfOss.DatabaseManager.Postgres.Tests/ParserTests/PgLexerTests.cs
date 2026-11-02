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
    [InlineData("", "")]
    [InlineData("", "SELECT 'it''s fine';")]
    [InlineData("body", "BEGIN\nRETURN '$other$';\nEND")]
    [InlineData("_tag123", "$TAG$ and $$ and $body$")]
    [InlineData("Tag", "$tag$ is not the closing delimiter")]
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
}
