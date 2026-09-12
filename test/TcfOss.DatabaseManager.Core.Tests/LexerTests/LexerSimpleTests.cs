using System.Reflection;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Tests.LexerTests;

public class LexerSimpleTests : LexerTestBase
{
    [Theory]
    [InlineData("1", "1", false)]
    [InlineData("1L", "1", true)]
    [InlineData("3.14", "3.14", false)]
    [InlineData("3.", "3.", false)]
    [InlineData(".3", ".3", false)]
    [InlineData("1.5E3", "1.5E3", false)]
    [InlineData("1.5E-3", "1.5E-3", false)]
    [InlineData("1E+3", "1E+3", false)]
    public void Select_Numerics(string given, string expectedNumber, bool expectedIsLong)
    {
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new NumericLiteral(expectedNumber, expectedIsLong) { Location = new Location() }
        };
        var actual = new GenericLexer().Tokenize($"SELECT {given}");
        Compare(expected, actual);
    }

    [Fact]
    public void Select_Almost_Numerics()
    {
        var actual = new GenericLexer().Tokenize("SELECT 1ea, 1e-10a, 1e-10-10");
        var expected = new Token[]
        {
            new Word("SELECT") { Location = new Location() },
            new NumericLiteral("1") { Location = new Location() },
            new Word("ea") { Location = new Location() },
            new Comma() { Location = new Location() },
            new NumericLiteral("1e-10") { Location = new Location() },
            new Word("a") { Location = new Location() },
            new Comma() { Location = new Location() },
            new NumericLiteral("1e-10") { Location = new Location() },
            new Minus() { Location = new Location() },
            new NumericLiteral("10") { Location = new Location() },
        };
        Compare(expected, actual);
    }

    [Theory]
    [InlineData("X'1A3F'", "1A3F")]
    [InlineData("x'1A3F'", "1A3F")]
    [InlineData("0x1A3F", "1A3F")]
    public void Select_Hex_Literal(string hexText, string hexValue)
    {
        var test = $"SELECT {hexText}";
        var actual = new GenericLexer().Tokenize(test);
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new HexStringLiteral(hexValue) { Location = new Location() }
        };

        Compare(expected, actual);
        Assert.Equal($"X'{hexValue}'", actual[1].ToString());

        var other = new HexStringLiteral(hexValue) { Location = new Location() };
        Assert.Equal(other, actual[1]);
        Assert.True(actual[1].Equals(other));
        Assert.True(actual[1].Equals((object)other));
        Assert.Equal(other.GetHashCode(), actual[1].GetHashCode());

        other = null;
        Assert.NotEqual(other, actual[1]);
        Assert.False(actual[1].Equals(other));
        Assert.False(actual[1].Equals((object?)other));
    }


    [Fact]
    public void Select_String()
    {
        var test = "SELECT 'blah'";
        var actual = new GenericLexer().Tokenize(test);
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("blah") { Location = new Location() }
        };

        Compare(expected, actual);
        Assert.Equal("'blah'", actual[1].ToString());

        var other = new StringLiteral("blah") { Location = new Location() };
        Assert.Equal(other, actual[1]);
        Assert.True(actual[1].Equals(other));
        Assert.True(actual[1].Equals((object)other));
        Assert.Equal(other.GetHashCode(), actual[1].GetHashCode());

        other = null;
        Assert.NotEqual(other, actual[1]);
        Assert.False(actual[1].Equals(other));
        Assert.False(actual[1].Equals((object?)other));
    }


    [Fact]
    public void Select_National_String()
    {
        var test = "SELECT N'blah'";
        var actual = new GenericLexer().Tokenize(test);
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new NationalStringLiteral("blah") { Location = new Location() }
        };

        Compare(expected, actual);
        Assert.Equal("N'blah'", actual[1].ToString());

        var other = new NationalStringLiteral("blah") { Location = new Location() };
        Assert.Equal(other, actual[1]);
        Assert.True(actual[1].Equals(other));
        Assert.True(actual[1].Equals((object)other));
        Assert.Equal(other.GetHashCode(), actual[1].GetHashCode());

        other = null;
        Assert.NotEqual(other, actual[1]);
        Assert.False(actual[1].Equals(other));
        Assert.False(actual[1].Equals((object?)other));
    }

    [Fact]
    public void Select_String_With_Quotes()
    {
        var test = "SELECT 'bl''ah'";
        var actual = new GenericLexer().Tokenize(test);
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
        var actual = new GenericLexer().Tokenize(test);
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("bl\\'ah") { Location = new Location() }
        };

        Compare(expected, actual);
    }

    [Theory]
    [InlineData("=", typeof(Equal))]
    [InlineData("<>", typeof(NotEqual))]
    [InlineData("!=", typeof(NotEqual))]
    [InlineData(">", typeof(GreaterThan))]
    [InlineData(">=", typeof(GreaterThanOrEqual))]
    [InlineData("<", typeof(LessThan))]
    [InlineData("<=", typeof(LessThanOrEqual))]
    [InlineData("+", typeof(Plus))]
    [InlineData("-", typeof(Minus))]
    [InlineData("*", typeof(Asterisk))]
    [InlineData("/", typeof(Slash))]
    [InlineData("%", typeof(Modulo))]
    public void Select_Operators_Tokenize(string opVal, Type type)
    {
        var ctorInfo = type.GetConstructor([]);

        var test = $"SELECT a {opVal} b";
        var expected = new[]
        {
            new Word("SELECT") { Location = new Location() },
            new Word("a") { Location = new Location() },
            (ctorInfo!.Invoke([]) as Token)!,
            new Word("b") { Location = new Location() },
        };
        var actual = new GenericLexer().Tokenize(test);
        Compare(expected, actual);
    }


    [Theory]
    [InlineData("=", typeof(Equal))]
    [InlineData("<>", typeof(NotEqual))]
    [InlineData("!=", typeof(NotEqual))]
    [InlineData(">", typeof(GreaterThan))]
    [InlineData(">=", typeof(GreaterThanOrEqual))]
    [InlineData("<", typeof(LessThan))]
    [InlineData("<=", typeof(LessThanOrEqual))]
    [InlineData("+", typeof(Plus))]
    [InlineData("-", typeof(Minus))]
    [InlineData("*", typeof(Asterisk))]
    [InlineData("/", typeof(Slash))]
    [InlineData("%", typeof(Modulo))]
    [InlineData(".", typeof(Dot))]
    [InlineData(",", typeof(Comma))]
    [InlineData(";", typeof(Semicolon))]
    [InlineData(":", typeof(Colon))]
    [InlineData("(", typeof(ParenOpen))]
    [InlineData(")", typeof(ParenClose))]
    [InlineData("{", typeof(BraceOpen))]
    [InlineData("}", typeof(BraceClose))]
    [InlineData(@"\", typeof(Backslash))]
    [InlineData("@", typeof(At))]
    [InlineData("!", typeof(Exclamation))]
    [InlineData("=>", typeof(FatArrow))]
    [InlineData(":=", typeof(Walrus))]
    public void SymbolOperatorsTokenize(string opVal, Type type)
    {
        var actual = new GenericLexer().Tokenize(opVal);
        Assert.True(typeof(ITypeSymbol).IsAssignableFrom(type));
        var prop = type.GetProperty("TypeSymbol", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
        Assert.Equal(prop, actual[0].ToString());
    }

    [Theory]
    [InlineData("dog", "dog", QuoteStyle.None)]
    [InlineData("\"dog\"", "dog", QuoteStyle.Ansi)]
    [InlineData("`dog`", "dog", QuoteStyle.Backticks)]
    [InlineData("[dog]", "dog", QuoteStyle.Brackets)]
    [InlineData("`dog cat **!/ fish`", "dog cat **!/ fish", QuoteStyle.Backticks)]
    [InlineData("\"do\"\"g\"", "do\"\"g", QuoteStyle.Ansi)]
    [InlineData("`do``g`", "do``g", QuoteStyle.Backticks)]
    [InlineData("[do]]g]", "do]]g", QuoteStyle.Brackets)]
    [InlineData("\"\"\"\"", "\"\"", QuoteStyle.Ansi)]
    public void Select_Quoted_Identifiers(string value, string unquoted, QuoteStyle quoteStyle)
    {
        var expected = new Token[]
        {
            new Word("SELECT") { Location = new Location(1, 1, 0) },
            new Word(unquoted, quoteStyle) { Location = new Location(1, 8, 7) },
        };
        var actual = new GenericLexer().Tokenize($"SELECT {value}");
        Compare(expected, actual);
    }

    [Theory]
    [InlineData("SELECT", Keyword.SELECT)]
    [InlineData("sElECt", Keyword.SELECT)]
    [InlineData("create", Keyword.CREATE)]
    [InlineData("If", Keyword.IF)]
    [InlineData("IS", Keyword.IS)]
    [InlineData("view", Keyword.VIEW)]
    public void Matching_Keywords_Match(string value, Keyword keyword)
    {
        var actualFull = new GenericLexer().Tokenize($"{value}");
        var actual = Assert.Single(actualFull) as Word;
        Assert.NotNull(actual);
        Assert.Equal(keyword, actual.Keyword);
    }

    [Theory]
    [InlineData("SELECT", Keyword.CREATE)]
    [InlineData("sElECt", Keyword.VERBOSE)]
    [InlineData("create", Keyword.SELECT)]
    [InlineData("If", Keyword.ELSEIF)]
    [InlineData("IS", Keyword.NOT)]
    [InlineData("view", Keyword.CHARACTER)]
    public void Mismatched_Keywords_Do_Not_Match(string value, Keyword keyword)
    {
        var actual = Assert.Single(new GenericLexer().Tokenize(value)) as Word;
        Assert.NotNull(actual);
        Assert.NotEqual(Keyword.undefined, actual.Keyword);
        Assert.NotEqual(keyword, actual.Keyword);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("SLECT")]
    [InlineData("SELECTO")]
    [InlineData("UNSELECT")]
    public void Non_Keywords_Are_Undefined(string value)
    {
        var actualFull = new GenericLexer().Tokenize(value);

        var actual = Assert.Single(actualFull) as Word;

        Assert.NotNull(actual);
        Assert.Equal(Keyword.undefined, actual.Keyword);
    }

    [Fact]
    public void At_As_Just_At_When_String_Ensues()
    {
        var actual = new GenericLexer().Tokenize("@'dog'");
        var expected = new Token[]
        {
            new At() { Location = new Location(1, 1, 0) },
            new StringLiteral("dog") { Location = new Location(1, 2, 1) },
        };
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("`dog`", "dog", QuoteStyle.Backticks)]
    [InlineData("\"dog\"", "dog", QuoteStyle.Ansi)]
    [InlineData("[dog]", "dog", QuoteStyle.Brackets)]
    public void At_As_Just_At_When_Quoted_Identifier_Ensues(string value, string unquoted, QuoteStyle quoteStyle)
    {
        var actual = new GenericLexer().Tokenize("@" + value);
        var expected = new Token[]
        {
            new At() { Location = new Location() },
            new Word(unquoted, quoteStyle) { Location = new Location() }
        };
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("dog")]
    [InlineData("_dog")]
    [InlineData("DOG")]
    [InlineData("select")]
    public void At_As_Variable_Start_When_Word_Ensues(string value)
    {
        var actual = new GenericLexer().Tokenize("@" + value);
        var expected = new Token[]
        {
            new Word(value, QuoteStyle.None, SigilKind.Variable) { Location = new Location() }
        };
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("my block comment", null)]
    [InlineData("my block /*nested stuff*/ comment", null)]
    [InlineData("my inline comment\n", "--")]
    [InlineData("my inline comment", "--")]
    public void Comment(string body, string? prefix)
    {
        NonSqlType nonSqlType = prefix is null ? NonSqlType.BlockComment : NonSqlType.InlineComment;
        string commentBody = prefix is null ? $"/*{body}*/" : $"{prefix}{body}";
        var terminator = prefix is not null && !body.EndsWith('\n') ? StringSymbols.EndOfFile : null;

        var actual = new GenericLexer().Tokenize(commentBody);
        var expected = new List<Token>()
        {
            new NonSqlContainer(
            [
                new(nonSqlType, $"{body}{terminator}") { Prefix = prefix, Location = new Location() }
            ]) { Location = new Location() }
        };

        Compare(expected, actual);
    }

    [Theory]
    [InlineData("SELECT \"unclosed_ansi", "\"")]
    [InlineData("SELECT `unclosed_mysql", "`")]
    [InlineData("SELECT [unclosed_mssql", "]")]
    public void Unclosed_QuotedIdentifier_Throws(string sql, string expectedCloseChar)
    {
        var lexer = new GenericLexer();
        var exception = Assert.Throws<LexException.UnterminatedQuotedIdentifier>(() => lexer.Tokenize(sql));
        var expectedMessage = $"Unterminated quoted identifier. Expected '{expectedCloseChar}'.";

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void UnclosedBlockComment_Throws()
    {
        var lexer = new GenericLexer();
        var exception = Assert.Throws<LexException.UnterminatedBlockComment>(() => lexer.Tokenize("SELECT /* unclosed comment "));
        var expectedMessage = "Unterminated block comment.";

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void LexLabel()
    {
        var lexer = new GenericLexer();
        var tokens = lexer.Tokenize("f:");

        var expected = new List<Token>
        {
            new Label(new Identifier("f")) { Location = new Location() },
        };
        Compare(expected, tokens);

        Assert.True(expected[0].Equals(tokens[0]));

        var other = new Label(new Identifier("f")) { Location = new Location() };
        Assert.True(tokens[0].Equals(other));
        Assert.True(tokens[0].Equals((object)other));
        Assert.Equal(expected[0].GetHashCode(), tokens[0].GetHashCode());
        Assert.Equal(2, tokens[0].Length);

        other = null;
        Assert.NotEqual(other, tokens[0]);
        Assert.False(tokens[0].Equals(other));
        Assert.False(tokens[0].Equals((object?)other));
    }

    [Fact]
    public void LexQuotedLabel()
    {
        var lexer = new GenericLexer();
        var tokens = lexer.Tokenize("`my label`:");

        var expected = new List<Token>
        {
            new Label(new Identifier("my label", QuoteStyle.Backticks)) { Location = new Location() },
        };
        Compare(expected, tokens);

        var other = new Label(new Identifier("my label", QuoteStyle.Backticks)) { Location = new Location() };
        Assert.True(tokens[0].Equals(other));
        Assert.True(tokens[0].Equals((object)other));
        Assert.Equal(expected[0].GetHashCode(), other.GetHashCode());
        Assert.Equal(11, tokens[0].Length);

        other = null;
        Assert.NotEqual(other, tokens[0]);
        Assert.False(tokens[0].Equals(other));
        Assert.False(tokens[0].Equals((object?)other));
    }

    [Theory]
    [InlineData("\n", 1, NonSqlType.Newline)]
    [InlineData("\r", 1, NonSqlType.Newline)]
    [InlineData("\r\n", 1, NonSqlType.Newline)]
    [InlineData("\n\r", 2, NonSqlType.Newline)]
    [InlineData("\t", 1, NonSqlType.Tab)]
    [InlineData("    ", 4, NonSqlType.Space)]
    public void Whitespace(string sql, int length, NonSqlType expectedType)
    {
        var lexer = new GenericLexer();
        List<Token> tokens = lexer.Tokenize(sql);

        var first = Assert.Single(tokens) as NonSqlContainer;
        Assert.NotNull(first);

        Assert.Equal(length, first.Length);

        var subTokens = first.SubTokens;
        Assert.Equal(length, subTokens.Count);

        foreach (var token in subTokens)
        {
            Assert.Equal(expectedType, token.NonSqlType);
        }
    }

    [Fact]
    public void WhitespaceCombo()
    {
        var lexer = new GenericLexer();
        var tokens = lexer.Tokenize("\n \t\r    \r\n\t  \n");

        var first = Assert.Single(tokens) as NonSqlContainer;
        Assert.NotNull(first);

        var subTokens = first.SubTokens;
        Assert.Equal(13, subTokens.Count);
        Assert.Equal(13, first.Length);

        // Newline and Carriage Return + Newline are Equivalent
        var otherTokens = lexer.Tokenize("\n \t\r\n    \n\t  \r\n");

        var other = otherTokens[0] as NonSqlContainer;
        Assert.NotNull(other);

        Assert.Equal(first, other);
        Assert.Equal(first.GetHashCode(), other.GetHashCode());
        Assert.Equal(first.Length, other.Length);

        Assert.True(first.Equals(other));
        Assert.True(first.Equals((object)other));

        // Replace one \r\n with \t. Lengths should be the same, everything else different
        otherTokens = lexer.Tokenize("\n \t\t    \n\t  \r\n");
        other = otherTokens[0] as NonSqlContainer;
        Assert.NotNull(other);

        Assert.NotEqual(first, other);
        Assert.NotEqual(first.GetHashCode(), other.GetHashCode());
        Assert.Equal(first.Length, other.Length);
        Assert.False(first.Equals(other));
        Assert.False(first.Equals((object)other));

        // Now compare to null (mostly to get this off the coverage report)
        other = null;
        Assert.NotEqual(first, other);
        Assert.False(first.Equals(other));
        Assert.False(first.Equals((object?)other));
    }
}
