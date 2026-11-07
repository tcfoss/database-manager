using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Tests.LexerTests;
using TcfOss.DatabaseManager.Postgres.Lexing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgLexerTests : LexerTestBase<PgLexer>
{
    [Theory]
    [InlineData("caf\u00e9")]
    [InlineData("\u00e9clair")]
    [InlineData("\u540d\u524d")]
    [InlineData("a$b")]
    [InlineData("name$tag$body$tag$")]
    public void Unquoted_Identifiers_Preserve_Dialect_Characters(string identifier)
    {
        List<Token> tokens = GetTokens(identifier + ";");

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
        List<Token> tokens = GetTokens(" \n" + literal + ";");
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
    [InlineData("$tag-name$body$tag-name$")]
    [InlineData("$tag")]
    public void InvalidDollarOpeners_AreRejected(string sql)
    {
        Assert.Throws<LexException.UnexpectedCharacter>(() => GetTokens(sql));
    }

    [Theory]
    [InlineData("$$")]
    [InlineData("$$body")]
    [InlineData("$1$body$1$")]
    [InlineData("$tag$body$TAG$")]
    public void UnterminatedDollarStrings_AreRejected(string sql)
    {
        Assert.Throws<LexException.UnterminatedString>(() => GetTokens(sql));
        Assert.Equal("ok", Assert.IsType<DollarQuotedStringLiteral>(GetTokens("$$ok$$")[0]).Value);
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
        var token = Assert.IsType<StringLiteral>(GetTokens(sql)[0]);
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
        List<Token> tokens = GetTokens(sql);

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
        List<Token> tokens = GetTokens(sql);

        Assert.Equal(contents, Assert.IsType<StringLiteral>(tokens[0]).Value);
        int position = sql.Length - 1;
        Assert.Equal(new Location(2, position - sql.LastIndexOf('\n'), position), Assert.IsType<Semicolon>(tokens[1]).Location);
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public void Square_Brackets_Are_Symbol_Tokens()
    {
        List<Token> tokens = GetTokens("a[1]");

        Assert.Equal(4, tokens.Count);
        var open = Assert.IsType<BracketOpen>(tokens[1]);
        var close = Assert.IsType<BracketClose>(tokens[3]);
        Assert.Equal(BracketOpen.TypeSymbol, open.ToString());
        Assert.Equal(BracketClose.TypeSymbol, close.ToString());
        Assert.Equal(new Location(1, 2, 1), open.Location);
        Assert.Equal(new Location(1, 4, 3), close.Location);
    }

    [Theory]
    [InlineData("E''", "")]
    [InlineData("e'plain'", "plain")]
    [InlineData("E'it\\'s'", "it\\'s")]
    [InlineData("E'it''s'", "it''s")]
    [InlineData("E'\\\\'", "\\\\")]
    [InlineData("E'line\\nnext'", "line\\nnext")]
    public void EscapeStrings_PreserveRawContentAndSourceLength(string literal, string value)
    {
        List<Token> tokens = GetTokens(" \n" + literal + ";");
        var token = Assert.IsType<EscapeStringLiteral>(tokens[0]);

        Assert.Equal(value, token.Value);
        Assert.Equal("E'" + value + "'", token.ToString());
        Assert.Equal(literal.Length, token.Length);
        Assert.Equal(new Location(2, 1, 2), token.Location);
        Assert.Equal(literal.Length + 2, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData("E'")]
    [InlineData("E'body")]
    [InlineData("E'body\\'")]
    public void UnterminatedEscapeStrings_AreRejected(string sql)
    {
        Assert.Throws<LexException.UnterminatedString>(() => GetTokens(sql));
        Assert.Equal("ok", Assert.IsType<EscapeStringLiteral>(GetTokens("E'ok'")[0]).Value);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("it\\'s", "it's")]
    [InlineData("it''s", "it's")]
    [InlineData("\\\\", "\\")]
    [InlineData("\\b\\f\\n\\r\\t", "\b\f\n\r\t")]
    [InlineData("\\q\\v\\x", "qvx")]
    [InlineData("\\101\\x42\\x4Z", "AB\u0004Z")]
    [InlineData("\\1234", "S4")]
    [InlineData("\\303\\251", "\u00e9")]
    [InlineData("\\xC3\\xA9", "\u00e9")]
    [InlineData("\\u00e9", "\u00e9")]
    [InlineData("\\U0001F600", "\uD83D\uDE00")]
    [InlineData("\\uD83D\\uDE00", "\uD83D\uDE00")]
    [InlineData("\\uD83D\\U0000DE00", "\uD83D\uDE00")]
    [InlineData("caf\u00e9\\n", "caf\u00e9\n")]
    public void EscapeValues_DecodePostgresEscapes(string raw, string decoded)
    {
        var value = new Value.EscapeString(raw);

        Assert.Equal(decoded, value.DecodedValue);
        Assert.Equal("E'" + raw + "'", value.ToSql());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(224)]
    [InlineData(225)]
    [InlineData(1024)]
    public void LongEscapeStrings_PreserveRawTextAndFollowingLocation(int prefixLength)
    {
        string contents = new string('a', prefixLength) + "\\'line\n" + new string('b', 1024) + "\\\\";
        string sql = "E'" + contents + "';";
        List<Token> tokens = GetTokens(sql);

        Assert.Equal(contents, Assert.IsType<EscapeStringLiteral>(tokens[0]).Value);
        int position = sql.Length - 1;
        Assert.Equal(new Location(2, position - sql.LastIndexOf('\n'), position), Assert.IsType<Semicolon>(tokens[1]).Location);
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public void EscapeTokens_CompareRawContentButNotLocation()
    {
        var token = new EscapeStringLiteral("\\n") { Location = new Location() };
        var same = new EscapeStringLiteral("\\n") { Location = new Location(2, 3, 4) };

        Assert.True(token.Equals((object)same));
        Assert.True(token.Equals((Token)same));
        Assert.Equal(token.GetHashCode(), same.GetHashCode());
        Assert.False(token.Equals((object?)null));
        Assert.False(token.Equals((Token)new StringLiteral("\\n") { Location = new Location() }));
        Assert.False(token.Equals(new EscapeStringLiteral("other") { Location = new Location() }));
    }

    [Theory]
    [InlineData("example")]
    [InlineData("e$tag$body$tag$")]
    [InlineData("E")]
    public void EscapePrefix_OnlyAppliesImmediatelyBeforeQuote(string identifier)
    {
        Assert.Equal(identifier, Assert.IsType<Word>(Assert.Single(new PgLexer().Tokenize(identifier))).Value);
    }

    [Theory]
    [InlineData("U&''", "")]
    [InlineData("u&'d\\0061t\\+000061'", "d\\0061t\\+000061")]
    [InlineData("U&'it''s'", "it''s")]
    [InlineData("U&'\\\\'", "\\\\")]
    public void UnicodeStrings_PreserveRawContentAndLength(string literal, string raw)
    {
        List<Token> tokens = GetTokens(" \n" + literal + ";");
        var token = Assert.IsType<UnicodeStringLiteral>(tokens[0]);

        Assert.Equal(raw, token.Value);
        Assert.Equal(literal.Length, token.Length);
        Assert.Equal(new Location(2, 1, 2), token.Location);
        Assert.Equal(literal.Length + 2, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
    }

    [Theory]
    [InlineData("d\\0061t\\+000061", null, "data")]
    [InlineData("d!0061t!+000061", '!', "data")]
    [InlineData("\\D83D\\DE00", null, "\uD83D\uDE00")]
    [InlineData("\\+00D83D\\DE00", null, "\uD83D\uDE00")]
    [InlineData("\\D83D\\+00DE00", null, "\uD83D\uDE00")]
    [InlineData("\\+01F600", null, "\uD83D\uDE00")]
    [InlineData("\u00e9\uD83D\uDE00", null, "\u00e9\uD83D\uDE00")]
    [InlineData("it''s\\\\", null, "it's\\")]
    [InlineData("it''s!!", '!', "it's!")]
    public void UnicodeValues_DecodeCodePointsAndEscapes(string raw, char? escape, string expected)
    {
        var value = new Value.UnicodeString(raw) { EscapeCharacter = escape };
        Assert.Equal(expected, value.DecodedValue);
    }

    [Theory]
    [InlineData("U&\"d\\0061ta\"", "data")]
    [InlineData("u&\"d!0061ta\" UESCAPE '!'", "data")]
    [InlineData("U&\"\\+01F600\"", "\uD83D\uDE00")]
    [InlineData("U&\"has\\0022quote\"", "has\"\"quote")]
    [InlineData("U&\"has\"\"quote\"", "has\"\"quote")]
    [InlineData("U&\"data\" /* nested /* note */ */ UESCAPE '!'", "data")]
    public void UnicodeIdentifiers_DecodeAndPreserveOriginalSourceSpan(string literal, string expected)
    {
        List<Token> tokens = GetTokens(" \n" + literal + ";");
        var word = Assert.IsType<Word>(tokens[0]);

        Assert.Equal(expected, word.Value);
        Assert.Equal(QuoteStyle.Ansi, word.QuoteStyle);
        Assert.Equal(literal.Length, word.Length);
        Assert.Equal(2, word.ToIdentifier(7).Source!.Value.Start);
        Assert.Equal(literal.Length + 2, word.ToIdentifier(7).Source!.Value.End);
        Assert.Equal(literal.Length + 2, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData("U&'d!0061ta' UESCAPE '!'", '!', "data")]
    [InlineData("U&'d!0061ta'\n-- note\nUESCAPE '!'", '!', "data")]
    [InlineData("U&'d!0061ta' UESCAPE E'\\x21'", '!', "data")]
    [InlineData("U&'d!0061ta' UESCAPE $$!$$", '!', "data")]
    [InlineData("U&'\\0061' UESCAPE '\\'", '\\', "a")]
    public void UnicodeStrings_ReadEscapeClauseAndPreserveLength(string literal, char escape, string expected)
    {
        List<Token> tokens = GetTokens(literal + ";");
        var token = Assert.IsType<UnicodeStringLiteral>(tokens[0]);

        Assert.Equal(escape, token.EscapeCharacter);
        Assert.Equal(literal.Length, token.Length);
        Assert.Equal(expected, new Value.UnicodeString(token.Value) { EscapeCharacter = token.EscapeCharacter }.DecodedValue);
        Assert.Equal(literal.Length, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public void UnicodeLiterals_LookaheadDoesNotConsumeFollowingTokensOrTrivia()
    {
        const int count = 1024;
        List<Token> tokens = GetTokens(string.Join(" /* note */ ", Enumerable.Repeat("U&'x'", count)));

        Assert.Equal(count, tokens.Count);
        Assert.All(tokens, token => Assert.Equal(5, Assert.IsType<UnicodeStringLiteral>(token).Length));
        Assert.NotNull(tokens[1].PreNonSql);
    }

    [Theory]
    [InlineData("U & 'body'")]
    [InlineData("U& 'body'")]
    [InlineData("u& 'body'")]
    public void UnicodePrefixes_RequireImmediateQuotedContent(string sql)
    {
        List<Token> tokens = GetTokens(sql);
        Assert.IsType<Word>(tokens[0]);
        Assert.IsType<Ampersand>(tokens[1]);
        Assert.IsType<StringLiteral>(tokens[2]);
        Assert.Equal(3, tokens.Count);
    }

    [Fact]
    public void UnicodeTokenEquality_UsesRawContentsAndEscapeCharacterButNotSourceLength()
    {
        Token token = new UnicodeStringLiteral("!0061") { EscapeCharacter = '!', Location = new Location(1, 1, 0) };
        var same = new UnicodeStringLiteral("!0061") { EscapeCharacter = '!', SourceLength = 30, Location = new Location(2, 1, 8) };
        var different = new UnicodeStringLiteral("!0061") { EscapeCharacter = '#', Location = token.Location };

        Assert.Equal(token, same);
        Assert.True(token.Equals((object)same));
        Assert.Equal(token.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(token, different);
        Assert.False(token.Equals((Token?)null));
        Assert.False(token.Equals((object?)null));
        Assert.False(token.Equals(new StringLiteral("!0061") { Location = token.Location }));
    }

    [Theory]
    [InlineData("U&'x' UESCAPE ''")]
    [InlineData("U&'x' UESCAPE '!!'")]
    [InlineData("U&'x' UESCAPE '0'")]
    [InlineData("U&'x' UESCAPE 'a'")]
    [InlineData("U&'x' UESCAPE '+'")]
    [InlineData("U&'x' UESCAPE ''''")]
    [InlineData("U&'x' UESCAPE '\"'")]
    [InlineData("U&'x' UESCAPE ' '")]
    [InlineData("U&'x' UESCAPE '\u00e9'")]
    [InlineData("U&'x' UESCAPE E'\\t'")]
    [InlineData("U&'x' UESCAPE E'\\x00'")]
    [InlineData("U&'x' UESCAPE E'\\u123'")]
    [InlineData("U&'x' UESCAPE 1")]
    [InlineData("U&'x' UESCAPE identifier")]
    [InlineData("U&'x' UESCAPE U&'!'")]
    [InlineData("U&'x' UESCAPE")]
    [InlineData("U&'x' UESCAPE '!")]
    [InlineData("U&\"\"")]
    [InlineData("U&\"\\0000\"")]
    [InlineData("U&\"\\D800\"")]
    [InlineData("U&\"\\+110000\"")]
    [InlineData("U&\"unterminated")]
    [InlineData("U&'unterminated")]
    public void InvalidUnicodeLiteralsAndEscapeClauses_AreRejectedAndLexerRemainsReusable(string sql)
    {
        Assert.ThrowsAny<LexException>(() => GetTokens(sql));
        Assert.Equal("data", Assert.IsType<Word>(Assert.Single(GetTokens("U&\"d\\0061ta\""))).Value);
    }

    [Theory]
    [InlineData("B''", "", false)]
    [InlineData("B'10101'", "10101", false)]
    [InlineData("b'001'", "001", false)]
    [InlineData("X''", "", true)]
    [InlineData("X'f'", "f", true)]
    [InlineData("x'0aF'", "0aF", true)]
    public void BitStrings_PreserveContentRadixAndSourceLength(string literal, string expected, bool hexadecimal)
    {
        List<Token> tokens = GetTokens(" \n" + literal + ";");
        var token = Assert.IsType<BitStringLiteral>(tokens[0]);

        Assert.Equal(expected, token.Value);
        Assert.Equal(hexadecimal, token.IsHexadecimal);
        Assert.Equal(literal.Length, token.Length);
        Assert.Equal(new Location(2, 1, 2), token.Location);
        Assert.Equal(literal.Length + 2, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal($"{(hexadecimal ? 'X' : 'B')}'{expected}'", token.ToString());
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(224, false)]
    [InlineData(225, false)]
    [InlineData(1024, false)]
    [InlineData(0, true)]
    [InlineData(224, true)]
    [InlineData(225, true)]
    [InlineData(1024, true)]
    public void BitStrings_LongContentsPreserveTextAndFollowingLocation(int length, bool hexadecimal)
    {
        string raw = new('0', length);
        string literal = $"{(hexadecimal ? 'X' : 'B')}'{raw}'";
        List<Token> tokens = GetTokens(literal + ";");

        Assert.Equal(raw, Assert.IsType<BitStringLiteral>(tokens[0]).Value);
        Assert.Equal(literal.Length, tokens[0].Length);
        Assert.Equal(literal.Length, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("b")]
    [InlineData("X")]
    [InlineData("x")]
    public void BitStrings_PrefixRequiresImmediateSingleQuote(string prefix)
    {
        List<Token> tokens = GetTokens(prefix + " '01'");

        Assert.Equal(prefix, Assert.IsType<Word>(tokens[0]).Value);
        Assert.Equal("01", Assert.IsType<StringLiteral>(tokens[1]).Value);
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public void BitStrings_TokenEqualityIncludesRadixButNotLocation()
    {
        Token token = new BitStringLiteral("01") { Location = new Location() };
        var same = new BitStringLiteral("01") { Location = new Location(2, 3, 4) };

        Assert.Equal(token, same);
        Assert.True(token.Equals((object)same));
        Assert.Equal(token.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(token, new BitStringLiteral("01", true) { Location = token.Location });
        Assert.NotEqual(token, new BitStringLiteral("10") { Location = token.Location });
        Assert.False(token.Equals((Token?)null));
        Assert.False(token.Equals((object?)null));
        Assert.False(token.Equals(new HexStringLiteral("01") { Location = token.Location }));
    }

    [Theory]
    [InlineData("B'\0'")]
    [InlineData("X'\0'")]
    [InlineData("B'unterminated")]
    [InlineData("X'unterminated")]
    public void BitStrings_UnterminatedAndNulContentAreRejectedAndLexerRemainsReusable(string sql)
    {
        var lexer = new PgLexer();

        Assert.ThrowsAny<LexException>(() => GetTokens(sql));
        Assert.Equal("01", Assert.IsType<BitStringLiteral>(Assert.Single(GetTokens("B'01'"))).Value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData("0001")]
    [InlineData("2147483647")]
    public void PositionalParameters_PreserveDigitsAndSourceLength(string digits)
    {
        List<Token> tokens = GetTokens(" \n$" + digits + ";");
        var token = Assert.IsType<PositionalParameterToken>(tokens[0]);

        Assert.Equal(digits, token.Value);
        Assert.Equal(digits.Length + 1, token.Length);
        Assert.Equal(new Location(2, 1, 2), token.Location);
        Assert.Equal("$" + digits, token.ToString());
        Assert.Equal(digits.Length + 3, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public void PositionalParameters_DoNotDisruptDollarQuotedStringsOrIdentifierDollars()
    {
        List<Token> tokens = GetTokens("$1, $$literal $2$$, $tag$literal $3$tag$, field$4");

        Assert.IsType<PositionalParameterToken>(tokens[0]);
        Assert.Equal("literal $2", Assert.IsType<DollarQuotedStringLiteral>(tokens[2]).Value);
        Assert.Equal("literal $3", Assert.IsType<DollarQuotedStringLiteral>(tokens[4]).Value);
        Assert.Equal("field$4", Assert.IsType<Word>(tokens[6]).Value);
        Assert.Equal(7, tokens.Count);
    }

    [Theory]
    [InlineData("$1name")]
    [InlineData("$1_name")]
    [InlineData("$1\u00e9")]
    [InlineData("$2147483648")]
    [InlineData("$9999999999999999999999999999999999")]
    public void PositionalParameters_InvalidIndicesAndTrailingIdentifierTextAreRejected(string sql)
    {
        Assert.ThrowsAny<LexException>(() => GetTokens(sql));
    }

    [Fact]
    public void PositionalParameters_TokenEqualityIgnoresLocation()
    {
        Token token = new PositionalParameterToken("1") { Location = new Location() };
        var same = new PositionalParameterToken("1") { Location = new Location(2, 3, 4) };

        Assert.Equal(token, same);
        Assert.True(token.Equals((object)same));
        Assert.Equal(token.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(token, new PositionalParameterToken("2") { Location = token.Location });
        Assert.False(token.Equals((Token?)null));
        Assert.False(token.Equals((object?)null));
        Assert.False(token.Equals(new Word("1") { Location = token.Location }));
    }

    [Theory]
    [InlineData("0xFF")]
    [InlineData("0X_Ff_01")]
    [InlineData("0o755")]
    [InlineData("0O_7_5_5")]
    [InlineData("0b101")]
    [InlineData("0B_1_0_1")]
    [InlineData("1_000")]
    [InlineData("1_000.2_5")]
    [InlineData(".2_5")]
    [InlineData("1_0e+1_2")]
    public void ExtendedNumbers_PreserveNumericSpellingAndSourceLength(string sql)
    {
        List<Token> tokens = GetTokens("\n  " + sql + ";");

        var number = Assert.IsType<NumericLiteral>(tokens[0]);
        Assert.Equal(sql, number.Value);
        Assert.False(number.IsLong);
        Assert.Equal(sql, number.ToString());
        Assert.Equal(sql.Length, number.Length);
        Assert.Equal(3, number.Location.Position);
        Assert.Equal(2, number.Location.Line);
        Assert.Equal(3, number.Location.Column);
        Assert.Equal(sql.Length + 3, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(sql.Length + 3, tokens[1].Location.Column);
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData("1..2", "1", "2")]
    [InlineData("1_000..2_000", "1_000", "2_000")]
    public void ExtendedNumbers_DoubleDotsDoNotBecomeDecimalPoints(string sql, string left, string right)
    {
        List<Token> tokens = GetTokens(sql);

        Assert.Equal(left, Assert.IsType<NumericLiteral>(tokens[0]).Value);
        Assert.Equal("..", Assert.IsType<DotDot>(tokens[1]).ToString());
        Assert.Equal(2, tokens[1].Length);
        Assert.Equal(right, Assert.IsType<NumericLiteral>(tokens[2]).Value);
        Assert.Equal(3, tokens.Count);
    }

    [Theory]
    [InlineData("0x")]
    [InlineData("0x_")]
    [InlineData("0x__1")]
    [InlineData("0x1_")]
    [InlineData("0x1g")]
    [InlineData("0x1p2")]
    [InlineData("0o")]
    [InlineData("0o8")]
    [InlineData("0o78")]
    [InlineData("0b")]
    [InlineData("0b2")]
    [InlineData("0b102")]
    [InlineData("0b1e2")]
    [InlineData("1_")]
    [InlineData("1__2")]
    [InlineData("1_.2")]
    [InlineData("1._2")]
    [InlineData("1.2_")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("1e-")]
    [InlineData("1e_2")]
    [InlineData("1e2_")]
    [InlineData("1e2__3")]
    [InlineData("1name")]
    [InlineData("1.2name")]
    [InlineData("1e2name")]
    [InlineData("1L")]
    [InlineData("1l")]
    [InlineData("1\u00e9")]
    public void ExtendedNumbers_InvalidDigitsSeparatorsAndTrailingJunkAreRejected(string sql)
    {
        Assert.Throws<LexException.ExpectedSymbolSequence>(() => GetTokens(sql));
    }
}
