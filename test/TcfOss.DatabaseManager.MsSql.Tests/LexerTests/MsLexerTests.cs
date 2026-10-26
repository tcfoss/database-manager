using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Tests.LexerTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MsSql.Tests.LexerTests;

public class MsLexerTests : LexerTestBase
{
    [Fact]
    public void BracketedIdentifier_Tokenized()
    {
        var lexer = new MsLexer();
        var tokens = lexer.Tokenize("SELECT [col1] FROM [dbo].[Users]");

        // Bracketed identifiers become Word tokens; just verify the shape
        // and the identifier text (quote-style metadata aside).
        Assert.Equal(6, tokens.Count);
        Assert.IsType<Word>(tokens[0]);
        Assert.Equal("col1", ((Word)tokens[1]).Value);
        Assert.IsType<Word>(tokens[2]);
        Assert.Equal("dbo", ((Word)tokens[3]).Value);
        Assert.IsType<Dot>(tokens[4]);
        Assert.Equal("Users", ((Word)tokens[5]).Value);
    }

    [Fact]
    public void Go_AsLineStartedBatchSeparator()
    {
        var lexer = new MsLexer();
        var tokens = lexer.Tokenize("SELECT 1\nGO\nSELECT 2");

        var expected = new List<Token>
        {
            new Word("SELECT") { Location = new Location() },
            new NumericLiteral("1") { Location = new Location() },
            new MsBatchSeparator { Location = new Location() },
            new Word("SELECT") { Location = new Location() },
            new NumericLiteral("2") { Location = new Location() },
        };

        Compare(expected, tokens);
    }

    [Fact]
    public void Go_WithCount()
    {
        var lexer = new MsLexer();
        var tokens = lexer.Tokenize("SELECT 1\nGO 5\nSELECT 2");

        var expected = new List<Token>
        {
            new Word("SELECT") { Location = new Location() },
            new NumericLiteral("1") { Location = new Location() },
            new MsBatchSeparator(5) { Location = new Location() },
            new Word("SELECT") { Location = new Location() },
            new NumericLiteral("2") { Location = new Location() },
        };

        Compare(expected, tokens);
    }

    [Fact]
    public void Go_AfterLeadingWhitespace_IsBatchSeparator()
    {
        var lexer = new MsLexer();
        var tokens = lexer.Tokenize("SELECT 1\n    GO\nSELECT 2");

        // The GO has only whitespace before it on its line, so it counts as
        // a line-start batch separator.
        Assert.Contains(tokens, t => t is MsBatchSeparator);
    }

    [Fact]
    public void Go_NotAtLineStart_IsIdentifier()
    {
        var lexer = new MsLexer();
        var tokens = lexer.Tokenize("SELECT GO FROM t");

        Assert.DoesNotContain(tokens, t => t is MsBatchSeparator);
    }

    [Fact]
    public void Go_FollowedByIdentifierContinuation_IsNotBatchSeparator()
    {
        // "GOAL" is just an identifier — even at line start it must NOT be
        // treated as the GO batch separator.
        var lexer = new MsLexer();
        var tokens = lexer.Tokenize("SELECT 1\nGOAL\n");

        Assert.DoesNotContain(tokens, t => t is MsBatchSeparator);
    }

    [Fact]
    public void AtVariable_TokenizedAsSigilWord()
    {
        var lexer = new MsLexer();
        var tokens = lexer.Tokenize("SELECT @x");

        Assert.Equal(2, tokens.Count);
        var word = Assert.IsType<Word>(tokens[1]);
        Assert.Equal("x", word.Value);
        Assert.Equal(SigilKind.Variable, word.Sigil);
        Assert.Equal(QuoteStyle.None, word.QuoteStyle);
    }

    [Fact]
    public void Select_String_With_Quote_Escapes()
    {
        var test = "SELECT 'bl''ah'";
        var actual = new MsLexer().Tokenize(test);
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("bl''ah") { Location = new Location() }
        };

        Compare(expected, actual);
    }

    [Fact]
    public void Select_String_With_Backslash_Escapes_Throws()
    {
        var lexer = new MsLexer();
        Assert.Throws<LexException.UnterminatedString>(() => lexer.Tokenize(@"SELECT 'bl\'ah'"));
    }

    [Fact]
    public void Select_Long_String_With_Backslash_Escapes_Throws()
    {
        var lexer = new MsLexer();
        var contents = new string('a', 200) + @"bl\'ah" + new string('b', 200);
        var sql = "SELECT '" + contents + "'";
        Assert.Throws<LexException.UnterminatedString>(() => lexer.Tokenize(sql));
    }
}
