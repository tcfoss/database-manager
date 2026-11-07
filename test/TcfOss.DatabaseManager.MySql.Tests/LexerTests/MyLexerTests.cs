using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Tests.LexerTests;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MySql.Tests.LexerTests;

public class MyLexerTests : LexerTestBase
{
    [Theory]
    [InlineData("caf\u00e9")]
    [InlineData("\u00e9clair")]
    [InlineData("\u540d\u524d")]
    [InlineData("\u00a9name")]
    [InlineData("name\u0301tail")]
    [InlineData("\u0661name")]
    [InlineData("name\ufffftail")]
    [InlineData("a$b")]
    [InlineData("$name")]
    public void Unquoted_Identifiers_Preserve_Dialect_Characters(string identifier)
    {
        List<Token> tokens = new MyLexer().Tokenize(identifier + ";");

        Assert.Equal(2, tokens.Count);
        Assert.Equal(identifier, Assert.IsType<Word>(tokens[0]).Value);
        Assert.Equal(identifier.Length, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(identifier.Length + 1, tokens[1].Location.Column);
    }

    [Theory]
    [InlineData(0x10400, false)]
    [InlineData(0x10400, true)]
    [InlineData(0xd800, false)]
    [InlineData(0xdc00, true)]
    public void Unquoted_Identifiers_Reject_Surrogates(int codePoint, bool hasPrefix)
    {
        string identifier = codePoint <= char.MaxValue ? new string((char)codePoint, 1) : char.ConvertFromUtf32(codePoint);
        if (hasPrefix)
        {
            identifier = "a" + identifier;
        }
        Assert.Throws<LexException.UnexpectedCharacter>(() => new MyLexer().Tokenize(identifier));
    }

    [Fact]
    public void Select_String_With_Charset()
    {
        var test = "SELECT _utf8'blah'";
        var actual = new MyLexer().Tokenize(test);

        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("blah") { Location = new Location(), CharSet = "_utf8" }
        };

        Compare(expected, actual);
    }
    [Fact]

    public void Select_String_With_Charset_With_Space()
    {
        var test = "SELECT _utf8 'blah'";
        var actual = new MyLexer().Tokenize(test);

        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("blah") { Location = new Location(), CharSet = "_utf8" }
        };

        Compare(expected, actual);
    }

    [Fact]
    public void Leading_Charset_Takes_Whitespace_From_String()
    {
        List<Token> tokens = new MyLexer().Tokenize("_utf8 'blah'");

        var literal = Assert.IsType<StringLiteral>(Assert.Single(tokens));
        Assert.Equal("_utf8", literal.CharSet);
        Assert.Equal(0, literal.Location.Position);
        Assert.NotNull(literal.PreNonSql);
        Assert.Equal(new Core.Statements.Components.NonSql.Spaces(1), Assert.Single(literal.PreNonSql));
        Assert.Equal(5, literal.PreNonSql.Start);
        Assert.Equal(6, literal.PreNonSql.End);
    }

    [Theory]
    [InlineData("@_var", "_var")]
    [InlineData("@$var", "$var")]
    public void User_Variable_May_Start_With_Underscore_Or_Dollar(string sql, string expected)
    {
        var word = Assert.IsType<Word>(Assert.Single(new MyLexer().Tokenize(sql)));

        Assert.Equal(expected, word.Value);
        Assert.Equal(SigilKind.Variable, word.Sigil);
    }

    [Fact]
    public void Select_UnderscoreVar_NotCharset()
    {
        var test = "SELECT _var;";
        var actual = new MyLexer().Tokenize(test);

        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new Word("_var") { Location = new Location() },
            new Semicolon { Location = new Location() }
        };

        Compare(expected, actual);
    }

    [Fact]
    public void Select_UnderscoreVar_NotCharset_AtEnd()
    {
        var test = "SELECT _var";
        var actual = new MyLexer().Tokenize(test);

        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new Word("_var") { Location = new Location() },
        };

        Compare(expected, actual);
    }

    [Fact]
    public void Delimiter()
    {
        var lexer = new MyLexer();
        var tokens = lexer.Tokenize("DELIMITER $$ SELECT 1 $$ DELIMITER ;");

        var expected = new List<Token>
        {
            new SetDelimiter("$$") { Location = new Location() },
            new Word("SELECT") { Location = new Location() },
            new NumericLiteral("1") { Location = new Location() },
            new MyTerminator("$$") { Location = new Location() },
            new SetDelimiter(";") { Location = new Location() },
        };
        Compare(expected, tokens);
    }

    [Fact]
    public void Delimiter_ValueRequired()
    {
        var lexer = new MyLexer();
        var exception = Assert.Throws<LexException.ExpectedSymbolSequence>(() => lexer.Tokenize("DELIMITER  SELECT 1"));
        var expectedMessage = "Expected one or more symbol characters.";

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("$$")]
    [InlineData("$!$")]
    [InlineData("//")]
    public void Delimiter_Immediately_After_Identifier(string delimiter)
    {
        List<Token> tokens = new MyLexer().Tokenize($"DELIMITER {delimiter}\nSELECT name{delimiter}");

        Assert.Equal(4, tokens.Count);
        Assert.IsType<SetDelimiter>(tokens[0]);
        Assert.Equal("SELECT", Assert.IsType<Word>(tokens[1]).Value);
        Assert.Equal("name", Assert.IsType<Word>(tokens[2]).Value);
        Assert.Equal(delimiter, Assert.IsType<MyTerminator>(tokens[3]).Delimiter);
    }

    [Fact]
    public void Delimiter_Nonmatching_Dollar_Sequence_Is_Identifier()
    {
        var lexer = new MyLexer();
        List<Token> tokens = lexer.Tokenize("DELIMITER $$ SELECT 1 $1");

        Assert.Equal(4, tokens.Count);
        Assert.Equal("$1", Assert.IsType<Word>(tokens[3]).Value);
        Assert.DoesNotContain(tokens, token => token is MyTerminator);
    }

    [Fact]
    public void SetDelimiter_Equality()
    {
        var token1 = new SetDelimiter("//") { Location = new Location(1, 2, 3) };
        var token2 = new SetDelimiter("//") { Location = new Location(4, 5, 6) };

        Assert.Equal(token1, token2);
        Assert.Equal(token1, (object)token2);
        Assert.Equal(token1, (Token)token2);
        Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
    }


    [Fact]
    public void Select_String_With_Quote_Escapes()
    {
        var test = "SELECT 'bl''ah'";
        var actual = new MyLexer().Tokenize(test);
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("bl''ah") { Location = new Location() }
        };

        Compare(expected, actual);
    }

    [Fact]
    public void Select_String_With_Backslash_Escapes()
    {
        var test = @"SELECT 'bl\'ah'";
        var actual = new MyLexer().Tokenize(test);
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("bl\\'ah") { Location = new Location() }
        };

        Compare(expected, actual);
    }

    [Fact]
    public void SetDelimiter_Inequality()
    {
        var token1 = new SetDelimiter("//") { Location = new Location(1, 2, 3) };
        var token2 = new SetDelimiter(";;") { Location = new Location(4, 5, 6) };

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, (object)token2);
        Assert.NotEqual(token1, (Token)token2);
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());

#pragma warning disable xUnit2000 // Constants and literals should be the expected argument
        Assert.NotEqual(token1, null);
        Assert.False(token1.Equals(null));
#pragma warning restore xUnit2000 // Constants and literals should be the expected argument
    }
    [Theory]
    [InlineData("E")]
    [InlineData("e")]
    public void EscapeStringPrefixes_RemainSeparateIdentifierAndString(string prefix)
    {
        List<Token> tokens = new MyLexer().Tokenize(prefix + "'body'");

        Assert.Equal(prefix, Assert.IsType<Word>(tokens[0]).Value);
        Assert.Equal("body", Assert.IsType<StringLiteral>(tokens[1]).Value);
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData("U")]
    [InlineData("u")]
    public void UnicodePrefixes_RemainSeparateIdentifierOperatorAndString(string prefix)
    {
        List<Token> tokens = new MyLexer().Tokenize(prefix + "&'body'");

        Assert.Equal(prefix, Assert.IsType<Word>(tokens[0]).Value);
        Assert.IsType<Ampersand>(tokens[1]);
        Assert.Equal("body", Assert.IsType<StringLiteral>(tokens[2]).Value);
        Assert.Equal(3, tokens.Count);
    }

    [Fact]
    public void BackslashPairs_RetainExistingQuoteEscaping()
    {
        List<Token> tokens = new MyLexer().Tokenize("'\\\\'tail';");

        Assert.Equal("\\\\'tail", Assert.IsType<StringLiteral>(tokens[0]).Value);
        Assert.IsType<Semicolon>(tokens[1]);
        Assert.Equal(2, tokens.Count);
    }
}
