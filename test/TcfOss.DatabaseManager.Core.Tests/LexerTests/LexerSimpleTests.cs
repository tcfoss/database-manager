using System.Collections;
using System.Reflection;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using NonSqlComponent = TcfOss.DatabaseManager.Core.Statements.Components.NonSql;

namespace TcfOss.DatabaseManager.Core.Tests.LexerTests;

public class LexerSimpleTests : LexerTestBase<GenericLexer>
{
    [Theory]
    [InlineData("caf\u00e9")]
    [InlineData("\u00e9clair")]
    [InlineData("\u540d\u524d")]
    [InlineData("a\u0661b")]
    [InlineData("ascii_123")]
    [InlineData("Nname")]
    [InlineData("nname")]
    public void Unquoted_Identifier_Preserves_Unicode_And_Following_Location(string identifier)
    {
        List<Token> tokens = GetTokens(identifier + ";");

        var word = Assert.IsType<Word>(tokens[0]);
        Assert.Equal(identifier, word.Value);
        Assert.Equal(identifier.Length, word.Length);
        Assert.Equal(0, word.Location.Position);
        Assert.Equal(identifier.Length, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(identifier.Length + 1, tokens[1].Location.Column);
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData("Xname")]
    [InlineData("xname")]
    public void Hex_Prefixed_Identifier_Preserves_Content_And_Following_Position(string identifier)
    {
        List<Token> tokens = GetTokens(identifier + ";");

        Assert.Equal(identifier, Assert.IsType<Word>(tokens[0]).Value);
        Assert.Equal(identifier.Length, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

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
        var actual = GetTokens($"SELECT {given}");
        Compare(expected, actual);
    }

    [Fact]
    public void Select_Almost_Numerics()
    {
        var actual = GetTokens("SELECT 1ea, 1e-10a, 1e-10-10, 1e+a, 3.a");
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
            new Comma() { Location = new Location() },
            new NumericLiteral("1") { Location = new Location() },
            new Word("e") { Location = new Location() },
            new Plus() { Location = new Location() },
            new Word("a") { Location = new Location() },
            new Comma() { Location = new Location() },
            new NumericLiteral("3.") { Location = new Location() },
            new Word("a") { Location = new Location() }
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
        var actual = GetTokens(test);
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
        var actual = GetTokens(test);
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
        var actual = GetTokens(test);
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

    [Theory]
    [InlineData("N''", "")]
    [InlineData("n'caf\u00e9'", "caf\u00e9")]
    [InlineData("N'it''s'", "it''s")]
    [InlineData("n'a\\'b'", "a\\'b")]
    public void National_String_Preserves_Content_And_Following_Position(string sql, string expected)
    {
        List<Token> tokens = GetTokens(sql + ";");

        var literal = Assert.IsType<NationalStringLiteral>(tokens[0]);
        Assert.Equal(expected, literal.Value);
        Assert.Equal(0, literal.Location.Position);
        Assert.Equal(sql.Length, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public void Select_String_With_Quote_Escapes()
    {
        var test = "SELECT 'bl''ah'";
        var actual = GetTokens(test);
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
        var actual = GetTokens(test);
        var expected = new List<Token>()
        {
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("bl\\'ah") { Location = new Location() }
        };

        Compare(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("N")]
    [InlineData("n")]
    [InlineData("X")]
    [InlineData("x")]
    public void EscapedString_Preserves_Raw_Content_Type_And_Locations(string prefix)
    {
        string[] contents =
        [
            "",
            "plain",
            "caf\u00e9",
            "''",
            "''''",
            "''''''",
            "before''after",
            "trailing''",
            "\\n",
            "\\'",
            "before\\'after",
            new string('\\', 2) + "'tail",
            new string('\\', 3) + "'tail",
            new string('\\', 4) + "'tail",
            "\\'''",
            "line\nbreak",
            "line\r\nbreak",
            "line\rbreak",
            "before''\nafter\\'tail",
            new string('a', 224) + "''" + new string('b', 32),
            new string('a', 225) + "\\'" + new string('b', 32),
            new string('a', 300) + "''" + new string('b', 8192)
        ];
        foreach (string content in contents)
        {
            string sql = $"SELECT {prefix}'{content}';";
            List<Token> tokens = GetTokens(sql);
            string actual = prefix switch
            {
                "N" or "n" => Assert.IsType<NationalStringLiteral>(tokens[1]).Value,
                "X" or "x" => Assert.IsType<HexStringLiteral>(tokens[1]).Value,
                _ => Assert.IsType<StringLiteral>(tokens[1]).Value
            };

            Assert.Equal(content, actual);
            Assert.Equal(new Location(1, 8, 7), tokens[1].Location);
            int position = sql.Length - 1;
            int line = 1 + sql.Count(character => character == '\n');
            int column = position - sql.LastIndexOf('\n');
            Assert.Equal(new Location(line, column, position), Assert.IsType<Semicolon>(tokens[2]).Location);
            Assert.Equal(3, tokens.Count);
            Compare(tokens.Take(2).ToArray(), GetTokens(sql[..^1]));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("N")]
    [InlineData("n")]
    [InlineData("X")]
    [InlineData("x")]
    public void EscapedString_Malformed_Content_Throws_And_Lexer_Remains_Reusable(string prefix)
    {
        string[] literals =
        [
            "'",
            "'plain",
            "'''",
            "'''''",
            "'before''after",
            "'before\\",
            "'before\\'",
            "'before\\\\'",
            "'before\0after'",
            "'before''\0after'",
            "'before\\'\0after'",
            "'before''\nafter"
        ];
        foreach (string literal in literals)
        {
            Assert.Throws<LexException.UnterminatedString>(() => GetTokens("SELECT " + prefix + literal));
            List<Token> valid = GetTokens("'valid'");
            Assert.Equal("valid", Assert.IsType<StringLiteral>(Assert.Single(valid)).Value);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    public void String_Content_Boundaries_Preserve_Text_And_Following_Location(int ordinaryLength)
    {
        string ordinary = new('a', ordinaryLength);
        string[] contents =
        [
            ordinary,
            ordinary + "\u540d",
            ordinary + "\nrest",
            ordinary + "\r\nrest",
            ordinary + "''tail",
            ordinary + "\\'tail",
            ordinary + "\\\nrest",
            ordinary + "\\\\'tail",
            ordinary + "''\nrest",
            "''" + ordinary,
            "\\'" + ordinary
        ];
        foreach (string prefix in new[] { "", "N" })
        {
            foreach (string content in contents)
            {
                string sql = $"SELECT {prefix}'{content}';";
                List<Token> tokens = GetTokens(sql);
                string actual = prefix.Length == 0
                    ? Assert.IsType<StringLiteral>(tokens[1]).Value
                    : Assert.IsType<NationalStringLiteral>(tokens[1]).Value;

                Assert.Equal(content, actual);
                Assert.Equal(new Location(1, 8, 7), tokens[1].Location);
                int position = sql.Length - 1;
                int line = 1 + sql.Count(character => character == '\n');
                int column = position - sql.LastIndexOf('\n');
                Assert.Equal(new Location(line, column, position), Assert.IsType<Semicolon>(tokens[2]).Location);
                Assert.Equal(3, tokens.Count);
                Compare(tokens.Take(2).ToArray(), GetTokens(sql[..^1]));
            }
        }

        foreach (string tail in new[] { "", "\0tail'", "\\", "''", "\r\n", "\\'" })
        {
            Assert.Throws<LexException.UnterminatedString>(() => GetTokens("SELECT '" + ordinary + tail));
        }
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 254, false)]
    [InlineData(0, 255, false)]
    [InlineData(0, 256, false)]
    [InlineData(0, 8192, false)]
    [InlineData(224, 0, false)]
    [InlineData(224, 8192, true)]
    [InlineData(225, 8192, false)]
    [InlineData(300, 8192, false)]
    [InlineData(0, 8192, true)]
    [InlineData(300, 8192, true)]
    public void EscapedString_BufferBoundaries_PreserveContents(int prefixLength, int suffixLength, bool doubledQuote)
    {
        string contents = new string('a', prefixLength) + (doubledQuote ? "''" : "\\'") + new string('b', suffixLength);
        string sql = "SELECT '" + contents + "', 42";

        var actual = GetTokens(sql);

        Compare([
            new Word("SELECT") { Location = new Location() },
            new StringLiteral(contents) { Location = new Location() },
            new Comma() { Location = new Location() },
            new NumericLiteral("42") { Location = new Location() },
        ], actual);
        Assert.Equal(new Location(1, sql.Length - 1, sql.Length - 2), actual[^1].Location);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 8192)]
    [InlineData(300, 0)]
    [InlineData(300, 8192)]
    public void EscapedString_Unterminated_ThrowsAndAllowsSubsequentTokenization(int prefixLength, int suffixLength)
    {
        string sql = "SELECT '" + new string('a', prefixLength) + "''" + new string('b', suffixLength);

        Assert.Throws<LexException.UnterminatedString>(() => GetTokens(sql));

        Compare([
            new Word("SELECT") { Location = new Location() },
            new StringLiteral("valid") { Location = new Location() },
        ], GetTokens("SELECT 'valid'"));
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
        var actual = GetTokens(test);
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
    [InlineData("::", typeof(DoubleColon))]
    [InlineData("||", typeof(DoublePipe))]
    [InlineData("|", typeof(Pipe))]
    [InlineData("&", typeof(Ampersand))]
    [InlineData("^", typeof(Caret))]
    [InlineData("~", typeof(Tilde))]
    [InlineData("~*", typeof(TildeAsterisk))]
    [InlineData("!~", typeof(NotTilde))]
    [InlineData("!~*", typeof(NotTildeAsterisk))]
    public void SymbolOperatorsTokenize(string opVal, Type type)
    {
        var actual = GetTokens(opVal);
        Assert.IsType(type, Assert.Single(actual));
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
    [InlineData("\"\"", "", QuoteStyle.Ansi)]
    [InlineData("``", "", QuoteStyle.Backticks)]
    [InlineData("[]", "", QuoteStyle.Brackets)]
    public void Select_Quoted_Identifiers(string value, string unquoted, QuoteStyle quoteStyle)
    {
        var expected = new Token[]
        {
            new Word("SELECT") { Location = new Location(1, 1, 0) },
            new Word(unquoted, quoteStyle) { Location = new Location(1, 8, 7) },
        };
        var actual = GetTokens($"SELECT {value}");
        Compare(expected, actual);
    }

    [Theory]
    [InlineData("\"", "\"", QuoteStyle.Ansi)]
    [InlineData("`", "`", QuoteStyle.Backticks)]
    [InlineData("[", "]", QuoteStyle.Brackets)]
    public void Quoted_Identifiers_Preserve_Content_And_Following_Location(string open, string close, QuoteStyle quoteStyle)
    {
        string[] values =
        [
            "",
            "plain",
            $"escaped{close}{close}name",
            $"trailing{close}{close}",
            $"{close}{close}",
            "line\nbreak",
            "line\r\nbreak",
            "caf\u00e9",
            new('a', 16),
            new('a', 17),
            new('a', 64),
            new('a', 1024)
        ];
        foreach (string value in values)
        {
            string sql = $"{open}{value}{close};";
            List<Token> tokens = GetTokens(sql);

            var word = Assert.IsType<Word>(tokens[0]);
            Assert.Equal(value, word.Value);
            Assert.Equal(quoteStyle, word.QuoteStyle);
            Assert.Equal(value.Length + 2, word.Length);
            Assert.Equal(new Location(1, 1, 0), word.Location);
            int position = sql.Length - 1;
            int line = 1 + sql.Count(character => character == '\n');
            int column = position - sql.LastIndexOf('\n');
            Assert.Equal(new Location(line, column, position), Assert.IsType<Semicolon>(tokens[1]).Location);
            Assert.Equal(2, tokens.Count);
        }
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
        var actualFull = GetTokens($"{value}");
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
        var actual = Assert.Single(GetTokens(value)) as Word;
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
        var actualFull = GetTokens(value);

        var actual = Assert.Single(actualFull) as Word;

        Assert.NotNull(actual);
        Assert.Equal(Keyword.undefined, actual.Keyword);
    }

    [Fact]
    public void At_As_Just_At_When_String_Ensues()
    {
        var actual = GetTokens("@'dog'");
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
        var actual = GetTokens("@" + value);
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
        var actual = GetTokens("@" + value);
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
    [InlineData("", "--")]
    [InlineData("\n", "--")]
    [InlineData("my inline comment\r\n", "--")]
    public void Comment(string body, string? prefix)
    {
        string commentBody = prefix is null ? $"/*{body}*/" : $"{prefix}{body}";
        var terminator = prefix is not null && !body.EndsWith('\n') ? StringSymbols.EndOfFile : null;
        NonSqlComponent component = prefix is null
            ? new NonSqlComponent.BlockComment(body)
            : new NonSqlComponent.LineComment($"{body}{terminator}", prefix);
        var subTokens = new NonSqlList { Start = 0, End = commentBody.Length };
        subTokens.Add(component);

        var actual = new GenericLexer().Tokenize(commentBody);
        var expected = new List<Token>()
        {
            new NonSqlContainer(subTokens) { Location = new Location() }
        };

        Compare(expected, actual);
        Assert.Equal(commentBody.Length, actual[0].Length);
    }

    [Fact]
    public void LineComment_Embedded_Nul_Ends_Comment_And_Lexing_Continues()
    {
        List<Token> tokens = new GenericLexer().Tokenize("--a\0b");

        var word = Assert.IsType<Word>(Assert.Single(tokens));
        Assert.Equal("b", word.Value);
        Assert.Equal(new Location(1, 5, 4), word.Location);
        Assert.NotNull(word.PreNonSql);
        Assert.Equal(new NonSqlComponent.LineComment("a\0"), Assert.Single(word.PreNonSql));
        Assert.Equal(4, word.PreNonSql.Length);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("text", 4)]
    [InlineData("\n", 0)]
    [InlineData("text\nmore", 4)]
    [InlineData("text\r\nmore", 5)]
    [InlineData("\0more", 0)]
    [InlineData("text\0more\n", 4)]
    [InlineData("text\nmore\0", 4)]
    public void AdvanceToEndOfLine_Preserves_Stop_Position_And_Location(string text, int stopPosition)
    {
        string input = "--" + text;
        var state = new LexerState(input);
        var expectedState = new LexerState(input);
        state.Next();
        state.Next();
        expectedState.Next();
        expectedState.Next();

        state.AdvanceToEndOfLine();
        expectedState.TakeWhile(character => character is not CharSymbols.NewLine and not CharSymbols.EndOfFile);

        Assert.Equal(2 + stopPosition, state.Position);
        Assert.Equal(expectedState.Location, state.Location);
        Assert.Equal(expectedState.Peek(), state.Peek());
        Assert.Equal(expectedState.Next(), state.Next());
        Assert.Equal(expectedState.Location, state.Location);
    }

    [Fact]
    public void Stray_Closing_Bracket_Throws_And_Lexer_Remains_Reusable()
    {
        var lexer = new GenericLexer();

        Assert.Throws<LexException.UnexpectedCharacter>(() => lexer.Tokenize("]"));

        Word word = Assert.IsType<Word>(Assert.Single(lexer.Tokenize("[valid]")));
        Assert.Equal("valid", word.Value);
        Assert.Equal(QuoteStyle.Brackets, word.QuoteStyle);
    }

    [Theory]
    [InlineData("SELECT \"unclosed_ansi", "\"")]
    [InlineData("SELECT `unclosed_mysql", "`")]
    [InlineData("SELECT [unclosed_mssql", "]")]
    [InlineData("SELECT \"", "\"")]
    [InlineData("SELECT `", "`")]
    [InlineData("SELECT [", "]")]
    [InlineData("SELECT \"escaped\"\"", "\"")]
    [InlineData("SELECT `escaped``", "`")]
    [InlineData("SELECT [escaped]]", "]")]
    [InlineData("SELECT \"before\0after\"", "\"")]
    [InlineData("SELECT `before\0after`", "`")]
    [InlineData("SELECT [before\0after]", "]")]
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

    [Theory]
    [InlineData("f:")]
    [InlineData("f: ")]
    [InlineData("f :next")]
    public void LabelSyntax_ProducesSeparateWordAndColon(string sql)
    {
        var tokens = new GenericLexer().Tokenize(sql);
        Assert.Equal("f", Assert.IsType<Word>(tokens[0]).Value);
        Assert.IsType<Colon>(tokens[1]);
    }

    [Theory]
    [InlineData("f:=2", typeof(Walrus))]
    [InlineData("f::2", typeof(DoubleColon))]
    public void ColonOperators_RemainDistinct(string sql, Type operatorType)
    {
        var tokens = new GenericLexer().Tokenize(sql);
        Assert.IsType<Word>(tokens[0]);
        Assert.Equal(operatorType, tokens[1].GetType());
    }

    [Theory]
    [InlineData("\"", "\"", QuoteStyle.Ansi)]
    [InlineData("`", "`", QuoteStyle.Backticks)]
    [InlineData("[", "]", QuoteStyle.Brackets)]
    public void Quoted_Label_Preserves_Escaped_Name(string open, string close, QuoteStyle quoteStyle)
    {
        string name = $"label{close}{close}name";
        string quoted = $"{open}{name}{close}";
        List<Token> tokens = new GenericLexer().Tokenize(quoted + ": ;");

        var word = Assert.IsType<Word>(tokens[0]);
        Assert.Equal(name, word.Value);
        Assert.Equal(quoteStyle, word.QuoteStyle);
        Assert.Equal(quoted.Length, Assert.IsType<Colon>(tokens[1]).Location.Position);
        Assert.Equal(quoted.Length + 2, Assert.IsType<Semicolon>(tokens[2]).Location.Position);
        Assert.Equal(3, tokens.Count);
    }

    [Theory]
    [InlineData("\n", 1, typeof(NonSqlComponent.Newlines), 1)]
    [InlineData("\r", 1, typeof(NonSqlComponent.Newlines), 1)]
    [InlineData("\r\n", 1, typeof(NonSqlComponent.Newlines), 1)]
    [InlineData("\n\r", 1, typeof(NonSqlComponent.Newlines), 2)]
    [InlineData("\r\r\n\n", 1, typeof(NonSqlComponent.Newlines), 3)]
    [InlineData("\t", 1, typeof(NonSqlComponent.Tabs), 1)]
    [InlineData("\t", 3, typeof(NonSqlComponent.Tabs), 3)]
    [InlineData(" ", 4, typeof(NonSqlComponent.Spaces), 4)]
    [InlineData(" ", 40, typeof(NonSqlComponent.Spaces), 40)]
    [InlineData("\t", 40, typeof(NonSqlComponent.Tabs), 40)]
    [InlineData("\n", 40, typeof(NonSqlComponent.Newlines), 40)]
    public void Whitespace(string unit, int repeat, Type expectedType, int expectedNumber)
    {
        string sql = string.Concat(Enumerable.Repeat(unit, repeat));
        var lexer = new GenericLexer();
        List<Token> tokens = lexer.Tokenize(sql);

        var first = Assert.IsType<NonSqlContainer>(Assert.Single(tokens));
        var actual = Assert.IsType<NonSqlComponent.Whitespace>(Assert.Single(first.SubTokens), exactMatch: false);

        Assert.Equal(sql.Length, first.Length);
        Assert.Equal(expectedType, actual.GetType());
        Assert.Equal(expectedNumber, actual.Number);
    }

    [Fact]
    public void Whitespace_Runs_Advance_Location()
    {
        var lexer = new GenericLexer();
        List<Token> tokens = lexer.Tokenize("  \t\r\n\n  a");

        var word = Assert.IsType<Word>(Assert.Single(tokens));
        Assert.Equal(new Location(3, 3, 8), word.Location);
        Assert.NotNull(word.PreNonSql);
        Assert.Equal(0, word.PreNonSql.Start);
        Assert.Equal(8, word.PreNonSql.End);
        Assert.Equal<NonSqlComponent>(
            [new NonSqlComponent.Spaces(2), new NonSqlComponent.Tabs(1), new NonSqlComponent.Newlines(2), new NonSqlComponent.Spaces(2)],
            word.PreNonSql);
    }

    [Fact]
    public void WhitespaceCombo()
    {
        var lexer = new GenericLexer();
        var tokens = lexer.Tokenize("\n \t\r    \r\n\t  \n");

        var first = Assert.Single(tokens) as NonSqlContainer;
        Assert.NotNull(first);

        var subTokens = first.SubTokens;
        Assert.Equal(9, subTokens.Count);
        Assert.Equal(14, first.Length);

        // Newline and Carriage Return + Newline are Equivalent
        var otherTokens = lexer.Tokenize("\n \t\r\n    \n\t  \r\n");

        var other = otherTokens[0] as NonSqlContainer;
        Assert.NotNull(other);

        Assert.Equal(first, other);
        Assert.Equal(first.GetHashCode(), other.GetHashCode());
        Assert.Equal(15, other.Length);

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

    [Fact]
    public void EofSymbolType()
    {
        Assert.Equal(StringSymbols.EndOfFile, Eof.TypeSymbol);
    }

    [Theory]
    [InlineData(null, 4)]
    [InlineData("utf8", 8)]
    public void StringLiteralLengths(string? charSet, int expectedLength)
    {
        var token = new StringLiteral("hi")
        {
            CharSet = charSet,
            Location = new Location()
        };

        Assert.Equal(expectedLength, token.Length);
    }

    [Fact]
    public void NonSqlList_Append_Merges_Boundary_Whitespace()
    {
        var list = new NonSqlList { Start = 0, End = 2 };
        list.Add(new NonSqlComponent.BlockComment(""));
        list.Add(new NonSqlComponent.Spaces(1));
        var other = new NonSqlList { Start = 5, End = 8 };
        other.Add(new NonSqlComponent.Spaces(2));
        other.Add(new NonSqlComponent.Tabs(1));

        list.Append(other);

        Assert.Equal<NonSqlComponent>(
            [new NonSqlComponent.BlockComment(""), new NonSqlComponent.Spaces(3), new NonSqlComponent.Tabs(1)],
            list);
        Assert.Equal(0, list.Start);
        Assert.Equal(8, list.End);
    }

    [Fact]
    public void NonSqlList_Append_Keeps_Different_Whitespace_Separate()
    {
        var list = new NonSqlList { Start = 0, End = 1 };
        list.Add(new NonSqlComponent.Spaces(1));
        var other = new NonSqlList { Start = 1, End = 2 };
        other.Add(new NonSqlComponent.Tabs(1));

        list.Append(other);

        Assert.Equal<NonSqlComponent>([new NonSqlComponent.Spaces(1), new NonSqlComponent.Tabs(1)], list);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void NonSqlList_Append_Comment_At_Boundary_Does_Not_Merge(bool commentLast, bool commentFirst)
    {
        NonSqlComponent last = commentLast ? new NonSqlComponent.BlockComment("c") : new NonSqlComponent.Spaces(1);
        NonSqlComponent first = commentFirst ? new NonSqlComponent.BlockComment("c") : new NonSqlComponent.Spaces(1);
        var list = new NonSqlList { Start = 0, End = 1 };
        list.Add(last);
        var other = new NonSqlList { Start = 1, End = 2 };
        other.Add(first);

        list.Append(other);

        Assert.Equal<NonSqlComponent>([last, first], list);
        Assert.Equal(2, list.End);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NonSqlList_Append_With_Empty_List(bool thisEmpty)
    {
        var list = new NonSqlList { Start = 0, End = 0 };
        var other = new NonSqlList { Start = 0, End = 1 };
        (thisEmpty ? other : list).Add(new NonSqlComponent.Spaces(1));

        list.Append(other);

        Assert.Equal<NonSqlComponent>([new NonSqlComponent.Spaces(1)], list);
        Assert.Equal(1, list.End);
    }

    [Fact]
    public void NonSqlList_NonGeneric_Enumeration_Yields_Items()
    {
        var list = new NonSqlList { Start = 0, End = 1 };
        list.Add(new NonSqlComponent.Spaces(1));

        IEnumerator enumerator = ((IEnumerable)list).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(new NonSqlComponent.Spaces(1), enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Theory]
    [InlineData("x")]
    [InlineData("xyz")]
    [InlineData("Xyz")]
    public void Word_Starting_With_X_Has_Start_Location(string word)
    {
        List<Token> tokens = GetTokens($"  {word}");

        var actual = Assert.IsType<Word>(Assert.Single(tokens));
        Assert.Equal(word, actual.Value);
        Assert.Equal(new Location(1, 3, 2), actual.Location);
    }

    // === Round out LexerState coverage ===

    [Fact]
    public void LexerState_AtEnd_ReturnsEof()
    {
        var state = new LexerState("hello");

        Assert.Equal(CharSymbols.EndOfFile, state.PeekNth(10));

        while (state.Next() != CharSymbols.EndOfFile)
        {
            // Advance.
        }

        Assert.Equal(CharSymbols.EndOfFile, state.PeekNth(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("\n")]
    [InlineData("a\n")]
    [InlineData("a\nb")]
    [InlineData("\0x")]
    public void LexerState_Next_And_Clone_Preserve_Eof_Locations(string input)
    {
        var state = new LexerState(input) { IdentifierTerminator = "$!$" };
        int line = 1;
        int column = 1;
        for (int index = 0; index < input.Length; index++)
        {
            LexerState clone = state.Clone();
            Assert.Equal(state.Location, clone.Location);
            Assert.Equal(state.IdentifierTerminator, clone.IdentifierTerminator);
            Assert.Equal(input[index], clone.Next());
            Assert.Equal(index, state.Position);
            Assert.Equal(input[index], state.PeekNth(0));
            Assert.Equal(input[index], state.Next());
            if (index + 1 < input.Length)
            {
                if (input[index] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
            Assert.Equal(new Location(line, column, index + 1), state.Location);
        }

        for (int count = 0; count < 3; count++)
        {
            Assert.Equal(CharSymbols.EndOfFile, state.Peek());
            Assert.Equal(CharSymbols.EndOfFile, state.PeekNth(0));
            Assert.Equal(CharSymbols.EndOfFile, state.PeekNth(1));
            LexerState clone = state.Clone();
            Assert.Equal(state.Location, clone.Location);
            Assert.Equal(state.IdentifierTerminator, clone.IdentifierTerminator);
            Assert.Equal(CharSymbols.EndOfFile, clone.Next());
            Assert.Equal(input.Length + count, state.Position);
            Assert.Equal(CharSymbols.EndOfFile, state.Next());
            Assert.Equal(new Location(line, column, input.Length + count + 1), state.Location);
        }
    }

    [Fact]
    public void LexerState_Bulk_Advance_To_End_Preserves_Eof_And_Clone()
    {
        var state = new LexerState("123");
        Assert.Equal(3, state.AdvanceThroughDigits());
        Assert.Equal(new Location(1, 4, 3), state.Location);
        LexerState clone = state.Clone();
        Assert.Equal(CharSymbols.EndOfFile, clone.Peek());
        Assert.Equal(CharSymbols.EndOfFile, clone.Next());
        Assert.Equal(new Location(1, 4, 4), clone.Location);
        Assert.Equal(new Location(1, 4, 3), state.Location);
        Assert.Equal(CharSymbols.EndOfFile, state.PeekNth(0));
        Assert.Equal(CharSymbols.EndOfFile, state.Next());
        Assert.Equal(clone.Location, state.Location);
    }

    [Fact]
    public void LexerState_TakeIfSequence_FalseIfBeyondSpan()
    {
        var state = new LexerState("hello");
        Assert.False(state.TakeIfSequence("hello world"));
    }

    [Fact]
    public void LexerState_TakeIfSequence_HandlesNewLines()
    {
        var givenText = """
        Hello
        World
        """;

        var state = new LexerState(givenText);
        Assert.True(state.TakeIfSequence("Hello\nW"));
        var location = state.Location;
        Assert.Equal(2, location.Line);
        Assert.Equal(2, location.Column);
        Assert.Equal(7, location.Position);
    }

    [Fact]
    public void LexerState_TakeWhile_HandlesNewline()
    {
        var givenText = """
        Hello
        World
        """;

        var state = new LexerState(givenText);
        string result = new(state.TakeWhile(c => c != 'r'));

        var expected = "Hello\nWo";
        Assert.Equal(expected, result);

        var location = state.Location;
        Assert.Equal(2, location.Line);
        Assert.Equal(3, location.Column);
        Assert.Equal(8, location.Position);
    }

    [Theory]
    [InlineData("")]
    [InlineData("`")]
    [InlineData("a`")]
    [InlineData("abc")]
    [InlineData("a\nb`c")]
    [InlineData("a\n`")]
    [InlineData("a\n")]
    [InlineData("\n\nab\r\ncd\n`x")]
    [InlineData("ab\0c`")]
    [InlineData("a\\b`")]
    public void LexerState_AdvanceThroughNonQuote_Matches_Repeated_Next(string input)
    {
        AssertAdvanceMatchesRepeatedNext(
            input,
            (ref state) => state.AdvanceThroughNonQuote('`'),
            c => c is '`' or CharSymbols.EndOfFile);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("abc", true)]
    [InlineData("a\nb'c", true)]
    [InlineData("a\\b'", true)]
    [InlineData("a\\b'", false)]
    [InlineData("\n\nab\r\ncd\n\\x", true)]
    [InlineData("\n\nab\r\ncd\n\\x", false)]
    [InlineData("ab\0c'", true)]
    public void LexerState_AdvanceThroughOrdinaryStringCharacters_Matches_Repeated_Next(string input, bool backslashEscapes)
    {
        AssertAdvanceMatchesRepeatedNext(
            input,
            (ref state) => state.AdvanceThroughOrdinaryStringCharacters('\'', backslashEscapes),
            c => c is '\'' or CharSymbols.EndOfFile || (backslashEscapes && c == '\\'));
    }

    [Fact]
    public void LexerState_AdvanceThroughNonQuote_Past_End_Is_NoOp()
    {
        var state = new LexerState("a");
        state.Next();
        state.Next();
        Location before = state.Location;

        state.AdvanceThroughNonQuote('`');

        Assert.Equal(before, state.Location);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NumericFlags_DoubleDotTokenizationIsIndependentOfTrailingJunk(bool dotDot, bool rejectJunk)
    {
        var lexer = new NumericFlagsLexer { DotDotToken = dotDot, RejectTrailingJunk = rejectJunk };
        List<Token> tokens = lexer.Tokenize("1..2");

        if (dotDot)
        {
            Assert.Equal("1", Assert.IsType<NumericLiteral>(tokens[0]).Value);
            Assert.IsType<DotDot>(tokens[1]);
            Assert.Equal("2", Assert.IsType<NumericLiteral>(tokens[2]).Value);
            Assert.Equal(3, tokens.Count);
        }
        else
        {
            Assert.Equal("1.", Assert.IsType<NumericLiteral>(tokens[0]).Value);
            Assert.Equal(".2", Assert.IsType<NumericLiteral>(tokens[1]).Value);
            Assert.Equal(2, tokens.Count);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NumericFlags_DigitSeparatorsAreIndependentOfRadixPrefixes(bool separators, bool nonDecimal)
    {
        var lexer = new NumericFlagsLexer { DigitSeparators = separators, NonDecimalNumbers = nonDecimal };
        List<Token> tokens = lexer.Tokenize("1_000");

        if (separators)
        {
            Assert.Equal("1_000", Assert.IsType<NumericLiteral>(Assert.Single(tokens)).Value);
        }
        else
        {
            Assert.Equal("1", Assert.IsType<NumericLiteral>(tokens[0]).Value);
            Assert.Equal("_000", Assert.IsType<Word>(tokens[1]).Value);
            Assert.Equal(2, tokens.Count);
        }
    }

    [Theory]
    [InlineData("0b10")]
    [InlineData("0o7")]
    public void NumericFlags_RadixNumbersDoNotRequireSeparators(string sql)
    {
        var lexer = new NumericFlagsLexer { DigitSeparators = false };

        Assert.Equal(sql, Assert.IsType<NumericLiteral>(Assert.Single(lexer.Tokenize(sql))).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NumericFlags_HexInterpretationIsSelectableWithRadixSupport(bool numericHex)
    {
        var lexer = new NumericFlagsLexer { NumericHexNumbers = numericHex };
        Token token = Assert.Single(lexer.Tokenize("0xFF"));

        if (numericHex)
        {
            Assert.Equal("0xFF", Assert.IsType<NumericLiteral>(token).Value);
        }
        else
        {
            Assert.Equal("FF", Assert.IsType<HexStringLiteral>(token).Value);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NumericFlags_TrailingJunkRejectionIsIndependentOfDoubleDots(bool dotDot, bool rejectJunk)
    {
        var lexer = new NumericFlagsLexer { DotDotToken = dotDot, RejectTrailingJunk = rejectJunk };

        if (rejectJunk)
        {
            Assert.Throws<LexException.ExpectedSymbolSequence>(() => lexer.Tokenize("123name"));
        }
        else
        {
            List<Token> tokens = lexer.Tokenize("123name");
            Assert.Equal("123", Assert.IsType<NumericLiteral>(tokens[0]).Value);
            Assert.Equal("name", Assert.IsType<Word>(tokens[1]).Value);
            Assert.Equal(2, tokens.Count);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NumericFlags_PrefixUnderscoreRequiresDigitSeparatorSupport(bool separators)
    {
        var lexer = new NumericFlagsLexer { DigitSeparators = separators };

        if (separators)
        {
            Assert.Equal("0b_10", Assert.IsType<NumericLiteral>(Assert.Single(lexer.Tokenize("0b_10"))).Value);
        }
        else
        {
            Assert.Throws<LexException.ExpectedSymbolSequence>(() => lexer.Tokenize("0b_10"));
        }
    }

    [Theory]
    [InlineData("0b10name", "0b10", false)]
    [InlineData("0o7name", "0o7", false)]
    [InlineData("0xFFname", "0xFF", false)]
    [InlineData("0b10name", "0b10", true)]
    [InlineData("0o7name", "0o7", true)]
    [InlineData("0xFFname", "0xFF", true)]
    public void NumericFlags_RadixTrailingIdentifiersObeyJunkRejection(string sql, string number, bool rejectJunk)
    {
        var lexer = new NumericFlagsLexer { NumericHexNumbers = true, RejectTrailingJunk = rejectJunk };

        if (rejectJunk)
        {
            Assert.Throws<LexException.ExpectedSymbolSequence>(() => lexer.Tokenize(sql));
        }
        else
        {
            List<Token> tokens = lexer.Tokenize(sql);
            Assert.Equal(number, Assert.IsType<NumericLiteral>(tokens[0]).Value);
            Assert.Equal("name", Assert.IsType<Word>(tokens[1]).Value);
            Assert.Equal(number.Length, tokens[1].Location.Position);
            Assert.Equal(2, tokens.Count);
        }
    }

    [Theory]
    [InlineData("0b102", false)]
    [InlineData("0o78", false)]
    [InlineData("0b102", true)]
    [InlineData("0o78", true)]
    public void NumericFlags_InvalidRadixDigitsAlwaysFail(string sql, bool rejectJunk)
    {
        var lexer = new NumericFlagsLexer { RejectTrailingJunk = rejectJunk };

        Assert.Throws<LexException.ExpectedSymbolSequence>(() => lexer.Tokenize(sql));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("?")]
    [InlineData("->>")]
    [InlineData("+?")]
    [InlineData("/@@")]
    [InlineData("<=?")]
    [InlineData("!=?")]
    [InlineData("||?")]
    [InlineData("!~*?")]
    public void KitchenSink_CustomOperators_Preserve_Symbol_And_Source(string symbol)
    {
        List<Token> tokens = GetTokens("\n " + symbol + ";");

        var token = Assert.IsType<OperatorToken>(tokens[0]);
        Assert.Equal(symbol, token.Value);
        Assert.Equal(symbol.Length, token.Length);
        Assert.Equal(new Location(2, 2, 2), token.Location);
        Assert.Equal(symbol.Length + 2, Assert.IsType<Semicolon>(tokens[1]).Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    [Theory]
    [InlineData("+")]
    [InlineData("@")]
    [InlineData("#")]
    [InlineData("+?")]
    public void KitchenSink_Operators_StopBefore_BacktickQuotedIdentifiers(string symbol)
    {
        List<Token> tokens = GetTokens(symbol + "`dog`");

        Assert.Equal(symbol, tokens[0].ToString());
        var word = Assert.IsType<Word>(tokens[1]);
        Assert.Equal("dog", word.Value);
        Assert.Equal(QuoteStyle.Backticks, word.QuoteStyle);
        Assert.Equal(symbol.Length, word.Location.Position);
        Assert.Equal(2, tokens.Count);
    }

    private sealed class NumericFlagsLexer : Lexer<RunState>
    {
        public bool DigitSeparators { get; init; } = true;
        public bool NonDecimalNumbers { get; init; } = true;
        public bool NumericHexNumbers { get; init; }
        public bool DotDotToken { get; init; }
        public bool RejectTrailingJunk { get; init; }

        protected override bool SupportNumericDigitSeparators => DigitSeparators;
        protected override bool SupportNonDecimalNumericLiterals => NonDecimalNumbers;
        protected override bool HexNumbersAreNumericLiterals => NumericHexNumbers;
        protected override bool SupportDotDotToken => DotDotToken;
        protected override bool RejectNumericTrailingJunk => RejectTrailingJunk;
    }

    private delegate void LexerStateAction(ref LexerState state);

    private static void AssertAdvanceMatchesRepeatedNext(string input, LexerStateAction advance, Func<char, bool> isStop)
    {
        for (int start = 0; start <= input.Length; start++)
        {
            var expected = new LexerState(input);
            var actual = new LexerState(input);
            for (int i = 0; i < start; i++)
            {
                expected.Next();
                actual.Next();
            }
            while (!isStop(expected.Peek()))
            {
                expected.Next();
            }

            advance(ref actual);

            Assert.Equal(expected.Location, actual.Location);
            Assert.Equal(expected.Peek(), actual.Peek());
        }
    }
}
