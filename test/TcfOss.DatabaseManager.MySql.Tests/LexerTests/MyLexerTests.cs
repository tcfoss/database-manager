using FluentAssertions;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MySql.Tests.LexerTests;

public class MyLexerTests
{
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

    private static void Compare(List<Token> expected, List<Token> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.IsType(expected[i].GetType(), actual[i]);
            expected[i].Should().BeEquivalentTo(actual[i], options =>
            {
                return options.RespectingRuntimeTypes().Excluding(t => t.Location).Excluding(t => t.PreNonSql);
            });
        }
    }
}
