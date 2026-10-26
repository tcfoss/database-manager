using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Tests.LexerTests;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MySql.Tests.LexerTests;

public class MyLexerTests : LexerTestBase
{
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

    [Fact]
    public void Delimiter_Unmatched()
    {
        var lexer = new MyLexer();
        Assert.Throws<LexException.UnexpectedCharacter>(() => lexer.Tokenize("DELIMITER $$ SELECT 1 $1"));
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
}
