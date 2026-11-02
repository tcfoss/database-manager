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
    [Theory]
    [InlineData("caf\u00e9")]
    [InlineData("\u00e9clair")]
    [InlineData("\u540d\u524d")]
    [InlineData("a\u0661b")]
    [InlineData("name$tail")]
    [InlineData("name@tail")]
    [InlineData("name#tail")]
    [InlineData("##temporary")]
    [InlineData("GO$tail")]
    [InlineData("GO#tail")]
    [InlineData("GO@tail")]
    [InlineData("GO\u00e9")]
    public void Unquoted_Identifiers_Preserve_Dialect_Characters(string identifier)
    {
        List<Token> tokens = new MsLexer().Tokenize(identifier + ";");

        Assert.Equal(2, tokens.Count);
        Assert.Equal(identifier, Assert.IsType<Word>(tokens[0]).Value);
        Assert.Equal(identifier.Length, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(identifier.Length + 1, tokens[1].Location.Column);
    }

    [Fact]
    public void Variable_Preserves_Dialect_Continuation_Characters()
    {
        var word = Assert.IsType<Word>(Assert.Single(new MsLexer().Tokenize("@name$#@\u00e9")));

        Assert.Equal("name$#@\u00e9", word.Value);
        Assert.Equal(SigilKind.Variable, word.Sigil);
    }

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

    [Theory]
    [InlineData("\\")]
    [InlineData("before\\''after")]
    [InlineData("\\''")]
    [InlineData("line\\\n''tail\\")]
    public void Ordinary_Strings_Treat_Backslashes_As_Content(string contents)
    {
        string sql = $"'{contents}';";
        var tokens = new MsLexer().Tokenize(sql);

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
        var tokens = new MsLexer().Tokenize(sql);

        Assert.Equal(contents, Assert.IsType<StringLiteral>(tokens[0]).Value);
        int position = sql.Length - 1;
        Assert.Equal(new Location(2, position - sql.LastIndexOf('\n'), position), Assert.IsType<Semicolon>(tokens[1]).Location);
        Assert.Equal(2, tokens.Count);
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
