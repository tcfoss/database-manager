using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Resources;
using Ksc = TcfOss.DatabaseManager.Core.Parsing.KeywordSearchCondition;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

public static class ParserStateTests
{
    private sealed record UnrecognizedKeywordSearchCondition : Ksc;

    private static Token[] TokenSet1 => [
        new Word("SELECT") { Location = new Lexing.Location() },
        new Word("a") { Location = new Lexing.Location() },
        new Plus() { Location = new Lexing.Location() },
        new Word("b") { Location = new Lexing.Location() },
    ];

    [Fact]
    public static void Peek_Gives_First()
    {
        var s = new ParserState(TokenSet1);

        Assert.Equal(new Word("SELECT") { Location = new Lexing.Location() }, s.Peek());
    }

    [Fact]
    public static void Next_Gives_First()
    {
        var s = new ParserState(TokenSet1);

        Assert.Equal(new Word("SELECT") { Location = new Lexing.Location() }, s.Next());
    }

    [Fact]
    public static void Peek_After_Next_Gives_Second_Non_Whitespace()
    {
        var s = new ParserState(TokenSet1);
        s.Next();

        Assert.Equal(new Word("a") { Location = new Lexing.Location() }, s.Peek());
    }

    [Fact]
    public static void Next_After_Next_Gives_Second_Non_Whitespace()
    {
        var s = new ParserState(TokenSet1);
        s.Next();

        Assert.Equal(new Word("a") { Location = new Lexing.Location() }, s.Next());
    }

    [Fact]
    public static void PeekNth_Eof()
    {
        var s = new ParserState(TokenSet1);

        Assert.True(s.PeekNth(100) is Eof);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public static void Trailing_Trivia_Is_Only_Exposed_On_Eof(int tokenCount)
    {
        Token[] expected = TokenSet1[..tokenCount];
        var whitespace = new NonSqlList { Start = 8, End = 9 };
        whitespace.Add(new Core.Statements.Components.NonSql.Newlines(1));
        var trivia = new NonSqlContainer(whitespace) { Location = new Lexing.Location() { Line = 1, Column = 9, Position = 8 } };
        Token[] tokens = [.. expected, trivia];
        var s = new ParserState(tokens, sourceId: 7);

        Assert.Equal(7, s.SourceId);
        Assert.True(s.PeekN(0).IsEmpty);
        Assert.True(s.PeekN(tokenCount).SequenceEqual(expected));
        Assert.True(s.PeekN(tokenCount + 1).SequenceEqual(expected));
        var eof = Assert.IsType<Eof>(s.PeekNth(tokenCount));
        Assert.Equal(trivia.Location, eof.Location);
        Assert.Equal(trivia.SubTokens, eof.PreNonSql);
        Assert.Same(eof, s.PeekNth(tokenCount + 1));
        Assert.Same(eof, s.PeekNth(-1));

        foreach (Token token in expected)
        {
            Assert.Same(token, s.Peek());
            Assert.Same(token, s.Next());
        }

        Assert.Same(eof, s.Peek());
        Assert.Same(eof, s.Next());
        Assert.Same(eof, s.Next());
        Assert.True(s.PeekN(1).IsEmpty);
        Assert.Same(trivia, tokens[^1]);

        s.Rewind();
        if (tokenCount > 0)
        {
            Assert.Same(expected[^1], s.Next());
        }
        Assert.Same(eof, s.Peek());
    }

    [Fact]
    public static void PeekKeyword_Matches_True()
    {
        var s = new ParserState(TokenSet1);
        Assert.True(s.PeekKeyword(Keyword.SELECT));
    }

    [Fact]
    public static void PeekKeyword_NonMatches_False()
    {
        var s = new ParserState(TokenSet1);
        Assert.False(s.PeekKeyword(Keyword.UPDATE));
    }

    [Fact]
    public static void PeekKeyword_NonKeyword_False()
    {
        var s = new ParserState([new Semicolon() { Location = new Lexing.Location() }]);
        Assert.False(s.PeekKeyword(Keyword.CREATE));
    }

    [Fact]
    public static void PeekN_Match()
    {
        var s = new ParserState(TokenSet1);
        var expected = new Token[]
        {
            new Word("SELECT") { Location = new Lexing.Location() },
            new Word("a") { Location = new Lexing.Location() },
            new Plus() { Location = new Lexing.Location() }
        };

        Assert.True(s.PeekN(3).SequenceEqual(expected));
    }

    [Fact]
    public static void PeekN_NonMatch()
    {
        var s = new ParserState(TokenSet1);
        var expected = new Token[]
        {
            new Word("SELECT") { Location = new Lexing.Location() },
            new Word("b") { Location = new Lexing.Location() },
            new Plus() { Location = new Lexing.Location() },
        };

        Assert.False(s.PeekN(3).SequenceEqual(expected));
    }

    [Fact]
    public static void PeekN_Returns_View_Without_Advancing()
    {
        Token[] tokens = TokenSet1;
        var s = new ParserState(tokens);
        s.Next();

        ReadOnlySpan<Token> peeked = s.PeekN(2);

        Assert.True(peeked == tokens.AsSpan(1, 2));
        Assert.Same(tokens[1], s.Peek());
        s.Next();
        Assert.Same(tokens[1], peeked[0]);
        Assert.True(s.PeekN(int.MaxValue).SequenceEqual(tokens.AsSpan(2)));
    }

    [Fact]
    public static void Rewind_Reverses_Next_1()
    {
        var s = new ParserState(TokenSet1);

        var expected = s.Peek();

        s.Next();
        s.Rewind();

        var actual = s.Peek();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void Rewind_Reverses_Next_3()
    {
        var s = new ParserState(TokenSet1);

        var expected = s.Peek();

        s.Next();
        s.Next();
        s.Next();
        s.Rewind();
        s.Rewind();
        s.Rewind();

        var actual = s.Peek();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void Rewind_At_Start_No_Effect()
    {
        var s = new ParserState(TokenSet1);

        var expected = s.Peek();
        s.Rewind();
        s.Rewind();
        var actual = s.Peek();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void RewindToSaved()
    {
        var s = new ParserState(TokenSet1);

        s.Next();

        s.SavePosition();

        var expected = s.Peek();

        s.Next();
        s.Next();

        s.RewindToSaved();

        var actual = s.Peek();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void RewindToSaved_NoSave_NoEffect()
    {
        var s = new ParserState(TokenSet1);

        s.Next();
        s.Next();

        var expected = s.Peek();

        s.RewindToSaved();

        var actual = s.Peek();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ExpectKeyword_Match_Advances_Pointer()
    {
        var s = new ParserState(TokenSet1);

        s.ExpectKeyword(Keyword.SELECT);

        Assert.Equal(new Word("a") { Location = new Lexing.Location() }, s.Peek());
    }

    [Fact]
    public static void ExpectKeyword_NonMatch_Throws()
    {
        var s = new ParserState(TokenSet1);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => s.ExpectKeyword(Keyword.UPDATE));
        var expectedMessage = string.Format(ErrorMessages.Err_Parse_ExpectedButFound, "UPDATE", "SELECT");
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public static void ExpectKeyword_Empty_Throws()
    {
        var s = new ParserState([]);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => s.ExpectKeyword(Keyword.SELECT));
        var expectedMessage = string.Format(ErrorMessages.Err_Parse_ExpectedButFound, "SELECT", "end of input".Italic());
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public static void ExpectToken_Match_Advances_Pointer()
    {
        var s = new ParserState(TokenSet1);
        s.Next();
        s.Next();

        s.ExpectToken<Plus>();

        Assert.Equal(new Word("b") { Location = new Lexing.Location() }, s.Peek());
    }

    [Fact]
    public static void ExpectToken_NonMatch_Throws()
    {
        var s = new ParserState(TokenSet1);
        s.Next();
        s.Next();

        var exception = Assert.Throws<ParseException.ExpectedButFound>(s.ExpectToken<Minus>);
        var expectedMessage = string.Format(ErrorMessages.Err_Parse_ExpectedButFound, "-", "+");
        Assert.Equal(expectedMessage, exception.Message);
    }

    private static Token[] TokenSet2 => [
        new Word("CREATE") { Location = new Lexing.Location() },
        new Word("OR") { Location = new Lexing.Location() },
        new Word("REPLACE") { Location = new Lexing.Location() },
        new Word("TABLE") { Location = new Lexing.Location() },
        new Word("my_table") { Location = new Lexing.Location() },
        new Word("IF") { Location = new Lexing.Location() },
        new Word("NOT") { Location = new Lexing.Location() },
        new Word("EXISTS") { Location = new Lexing.Location() },
        new ParenOpen() { Location = new Lexing.Location() },
        new Word("my_column") { Location = new Lexing.Location() },
        new Word("INT") { Location = new Lexing.Location() },
        new Word("NOT") { Location = new Lexing.Location() },
        new Word("NULL") { Location = new Lexing.Location() },
        new ParenClose() { Location = new Lexing.Location() },
        new Semicolon() { Location = new Lexing.Location() }
    ];

    [Fact]
    public static void ParseKeyword_Success()
    {
        var s = new ParserState(TokenSet2);
        Assert.True(s.ParseKeyword(Keyword.CREATE));
    }

    [Fact]
    public static void ParseKeyword_Failure()
    {
        var s = new ParserState(TokenSet2);
        Assert.False(s.ParseKeyword(Keyword.UPDATE));
    }

    [Fact]
    public static void ParseKeyword_Eof()
    {
        var s = new ParserState(TokenSet2);
        Token next;
        while ((next = s.Next()) is not Eof)
        {
            // Advance pointer.
        }

        Assert.True(next is Eof);
        Assert.False(s.ParseKeyword(Keyword.CREATE));
    }

    [Fact]
    public static void ParseKeywordsAll_Success()
    {
        var s = new ParserState(TokenSet2);
        Assert.True(s.ParseKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE, Keyword.TABLE));
    }

    [Fact]
    public static void ParseKeywordsAll_Failure()
    {
        var s = new ParserState(TokenSet2);
        Assert.False(s.ParseKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE, Keyword.VIEW));
    }

    [Fact]
    public static void ParseKeywordsAll_Eof()
    {
        var s = new ParserState(TokenSet2);
        Token next;
        while ((next = s.Next()) is not Eof)
        {
            // Advance pointer.
        }

        Assert.True(next is Eof);
        Assert.False(s.ParseKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE, Keyword.TABLE));
    }

    [Fact]
    public static void ParseKeywordsAll_Eof_Minus_One()
    {
        var s = new ParserState(TokenSet2);

        while (s.Next() is not Eof)
        {
            // Advance pointer.
        }
        s.Rewind();

        Assert.True(s.Peek() is Semicolon);

        Assert.False(s.ParseKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE, Keyword.TABLE));
    }

    [Fact]
    public static void ParseKeywordsAny_First()
    {
        var s = new ParserState(TokenSet2);
        Assert.Equal(Keyword.CREATE, s.ParseKeywordsAny(Keyword.CREATE, Keyword.ALTER, Keyword.DROP));
    }

    [Fact]
    public static void ParseKeywordsAny_Later()
    {
        var s = new ParserState(TokenSet2);
        Assert.Equal(Keyword.CREATE, s.ParseKeywordsAny(Keyword.DROP, Keyword.ALTER, Keyword.CREATE));
    }

    [Fact]
    public static void ParseKeywordsAny_NoMatch()
    {
        var s = new ParserState(TokenSet2);
        Assert.Equal(Keyword.undefined, s.ParseKeywordsAny(Keyword.DROP, Keyword.ALTER, Keyword.EXECUTE));
    }

    [Fact]
    public static void ParseKeywordsAny_Eof()
    {
        var s = new ParserState(TokenSet2);
        Token next;
        while ((next = s.Next()) is not Eof)
        {
            // Advance.
        }

        Assert.True(next is Eof);
        Assert.Equal(Keyword.undefined, s.ParseKeywordsAny(Keyword.CREATE, Keyword.ALTER, Keyword.DROP));
    }

    [Fact]
    public static void ParseKeywordSequence()
    {
        var s = new ParserState(TokenSet2);
        var actual = s.ParseKeywordSequence(Ksc.OneOf(Keyword.ALTER, Keyword.CREATE), Ksc.Optionals(Keyword.OR, Keyword.REPLACE), Keyword.TABLE);
        var expected = new List<Keyword>()
        {
            Keyword.CREATE,
            Keyword.OR,
            Keyword.REPLACE,
            Keyword.TABLE
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseKeywordSequence_With_Optional_Fake()
    {
        var s = new ParserState(TokenSet2);
        var actual = s.ParseKeywordSequence(Ksc.OneOf(Keyword.ALTER, Keyword.CREATE), Ksc.Optionals(Keyword.OR, Keyword.REPLACE), Ksc.Optional(Keyword.TEMPORARY), Keyword.TABLE);
        var expected = new List<Keyword>()
        {
            Keyword.CREATE,
            Keyword.OR,
            Keyword.REPLACE,
            Keyword.TABLE
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseKeywordSequence_With_Optional_Seq_Fake()
    {
        var s = new ParserState(TokenSet2);
        var actual = s.ParseKeywordSequence(Ksc.OneOf(Keyword.ALTER, Keyword.CREATE), Ksc.Optionals(Keyword.OR, Keyword.REPLACE, Keyword.NOT), Keyword.TABLE);
        Assert.Empty(actual);
    }

    [Fact]
    public static void ParseKeywordSequence_With_Optional_Seq_Real_Fake()
    {
        var s = new ParserState(TokenSet2);
        var actual = s.ParseKeywordSequence(Ksc.Required(Keyword.CREATE), Ksc.Optionals(Keyword.OR, Keyword.REPLACE), Ksc.Optionals(Keyword.OR, Keyword.REPLACE), Keyword.TABLE);
        var expected = new List<Keyword>()
        {
            Keyword.CREATE,
            Keyword.OR,
            Keyword.REPLACE,
            Keyword.TABLE
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseKeywordSequence_Failure()
    {
        var s = new ParserState(TokenSet2);
        var actual = s.ParseKeywordSequence(Ksc.OneOf(Keyword.ALTER, Keyword.CREATE), Ksc.Optionals(Keyword.OR, Keyword.REPLACE), Keyword.VIEW);
        Assert.Empty(actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ParseKeywordSequence_Partial_Failure_Restores_Position(bool useOneOf)
    {
        var s = new ParserState(TokenSet2);
        s.Next();
        Token expected = s.Peek();
        Ksc required = useOneOf ? Ksc.OneOf(Keyword.VIEW, Keyword.INDEX) : Ksc.Required(Keyword.VIEW);

        List<Keyword> actual = s.ParseKeywordSequence(Ksc.Optionals(Keyword.OR, Keyword.REPLACE), required);

        Assert.Empty(actual);
        Assert.Same(expected, s.Peek());
    }

    [Fact]
    public static void ParseKeywordSequence_Optional_First_Match()
    {
        var s = new ParserState(TokenSet2);

        List<Keyword> actual = s.ParseKeywordSequence(Ksc.Optional(Keyword.TEMPORARY), Ksc.Optional(Keyword.CREATE));

        Assert.Equal([Keyword.CREATE], actual);
        Assert.True(s.PeekKeyword(Keyword.OR));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ParseKeywordSequence_Empty_Optional_Sequence_Preserves_Position(bool afterRequiredKeyword)
    {
        Token[] tokens =
        [
            new Word("CREATE") { Location = new Lexing.Location() },
            new Word("TABLE") { Location = new Lexing.Location() }
        ];
        var state = new ParserState(tokens);

        List<Keyword> found = afterRequiredKeyword
            ? state.ParseKeywordSequence(Ksc.Required(Keyword.CREATE), Ksc.Optionals())
            : state.ParseKeywordSequence(Ksc.Optionals());

        if (afterRequiredKeyword)
        {
            Assert.Equal(Keyword.CREATE, Assert.Single(found));
        }
        else
        {
            Assert.Empty(found);
        }
        Assert.Same(tokens[afterRequiredKeyword ? 1 : 0], state.Peek());
    }

    [Fact]
    public static void ParseKeywordSequence_Unrecognized_Condition_Does_Not_Consume_Tokens()
    {
        Token[] tokens = TokenSet1;
        var state = new ParserState(tokens);

        List<Keyword> found = state.ParseKeywordSequence(new UnrecognizedKeywordSearchCondition());

        Assert.Empty(found);
        Assert.Same(tokens[0], state.Peek());
        Assert.Equal(Keyword.SELECT, Assert.Single(state.ParseKeywordSequence(Ksc.Required(Keyword.SELECT))));
        Assert.Same(tokens[1], state.Peek());
    }

    [Fact]
    public static void ParseKeywordSequence_No_Matches_Returns_Independent_Empty_Lists()
    {
        var s = new ParserState(TokenSet2);
        Token expected = s.Peek();

        List<Keyword> first = s.ParseKeywordSequence(Ksc.Optional(Keyword.TEMPORARY));
        List<Keyword> second = s.ParseKeywordSequence();
        first.Add(Keyword.SELECT);

        Assert.Empty(second);
        Assert.Same(expected, s.Peek());
    }

    [Fact]
    public static void ParseKeywordSequence_Eof()
    {
        var s = new ParserState(TokenSet2);
        Token next;
        while ((next = s.Next()) is not Eof)
        {
            // Advance pointer.
        }
        Assert.True(next is Eof);

        var actual = s.ParseKeywordSequence(Ksc.OneOf(Keyword.ALTER, Keyword.CREATE), Ksc.Optionals(Keyword.OR, Keyword.REPLACE), Keyword.VIEW);
        Assert.Empty(actual);
    }

    [Fact]
    public static void PeekKeywordsAll_Match_Match()
    {
        var s = new ParserState(TokenSet2);
        Assert.True(s.PeekKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE, Keyword.TABLE));
    }

    [Fact]
    public static void PeekKeywordsAll_Match_NonMatch()
    {
        var s = new ParserState(TokenSet2);
        Assert.False(s.PeekKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE, Keyword.DATABASE));
    }

    [Fact]
    public static void PeekKeywordsAllEqual_NonKeyword_NonMatch()
    {
        var s = new ParserState([new Word("SELECT") { Location = new Lexing.Location() }, new Asterisk() { Location = new Lexing.Location() }, new Word("FROM") { Location = new Lexing.Location() }, new Word("my_table") { Location = new Lexing.Location() }]);
        Assert.False(s.PeekKeywordsAll(Keyword.SELECT, Keyword.DISTINCT));
    }

    [Fact]
    public static void PeekKeywordsAll_Match_Eof()
    {
        var s = new ParserState(TokenSet2);

        while (s.Next() is not Eof)
        {
            // Advance pointer.
        }

        Assert.False(s.PeekKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE, Keyword.DATABASE));
    }

    [Fact]
    public static void PeekKeywordsAllEqual_Handles_Empty_And_Short_Input_Without_Advancing()
    {
        Token[] tokens = [new Word("CREATE") { Location = new Lexing.Location() }];
        var s = new ParserState(tokens);

        Assert.True(s.PeekKeywordsAll());
        Assert.True(s.PeekKeywordsAll(Keyword.CREATE));
        Assert.False(s.PeekKeywordsAll(Keyword.CREATE, Keyword.TABLE));
        Assert.Same(tokens[0], s.Peek());

        s.Next();
        Assert.True(s.PeekKeywordsAll());
        Assert.False(s.PeekKeywordsAll(Keyword.CREATE));
    }

    [Fact]
    public static void ExpectKeywordsAll_Match_Advance_Pointer()
    {
        var s = new ParserState(TokenSet2);

        s.ExpectKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE);

        Assert.True(s.Peek() is Word { Keyword: Keyword.TABLE });
    }

    [Fact]
    public static void ExpectKeywordsAll_Match_Throws()
    {
        var s = new ParserState(TokenSet2);

        Assert.Throws<ParseException.ExpectedButFound>(() =>
        {
            s.ExpectKeywordsAll(Keyword.CREATE, Keyword.OR, Keyword.REPLACE, Keyword.PROCEDURE);
        });
    }

    private static Token[] TokenSet3 => [
        new ParenOpen() { Location = new Lexing.Location() },
        new Word("a") { Location = new Lexing.Location() },
        new Comma() { Location = new Lexing.Location() },
        new Word("b") { Location = new Lexing.Location() },
        new Comma() { Location = new Lexing.Location() },
        new Word("c") { Location = new Lexing.Location() },
        new ParenClose() { Location = new Lexing.Location() },
        new Semicolon() { Location = new Lexing.Location() }
    ];

    [Fact]
    public static void ParseParenthesized()
    {
        var s = new ParserState(TokenSet3);

        Assert.True(s.PeekIs<ParenOpen>());
        string actual = s.ParseParenthesized(x =>
        {
            string actual = "";
            while (!x.PeekIs<ParenClose>())
            {
                actual += x.Next().ToString();
            }
            return actual;
        });
        var expected = "a,b,c";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseParenthesizedCommaSeparated()
    {
        var s = new ParserState(TokenSet3);

        var expected = new SqlValueList<string>() { "a", "b", "c" };

        var actual = s.ParseParenthesizedCommaSeparated(x => x.Next().ToString() ?? "", false);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseParenthesizedCommaSeparated_Empty_Allowed()
    {
        var s = new ParserState([new ParenOpen() { Location = new Lexing.Location() }, new ParenClose() { Location = new Lexing.Location() }]);

        var actual = s.ParseParenthesizedCommaSeparated(x => x.Next().ToString() ?? "", true);

        Assert.NotNull(actual);
        Assert.Empty(actual);
    }

    [Fact]
    public static void ParseParenthesizedCommaSeparated_Empty_Forbidden()
    {
        var s = new ParserState([new ParenOpen() { Location = new Lexing.Location() }, new ParenClose() { Location = new Lexing.Location() }]);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() =>
        {
            _ = s.ParseParenthesizedCommaSeparated(x => x.Next().ToString() ?? "", false);
        });

        var expectedMessage = string.Format(ErrorMessages.Err_Parse_ExpectedButFound, "comma_separated_list".Italic(), ")");
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public static void ParseParenthesizedCommaSeparated_Missing_Forbidden()
    {
        var s = new ParserState([new Semicolon() { Location = new Lexing.Location() }]);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() =>
        {
            _ = s.ParseParenthesizedCommaSeparated(x => x.Next().ToString() ?? "", true);
        });

        var expectedMessage = string.Format(ErrorMessages.Err_Parse_ExpectedButFound, "parenthesized_list".Italic(), ";");
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public static void ParseParenthesizedCommaSeparatedOptional()
    {
        var s = new ParserState(TokenSet3);

        var expected = new SqlValueList<string>() { "a", "b", "c" };

        var actual = s.ParseParenthesizedCommaSeparatedOptional(x => x.Next().ToString() ?? "", false);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseParenthesizedCommaSeparatedOptional_Missing_Allowed()
    {
        var s = new ParserState([new Semicolon() { Location = new Lexing.Location() }]);

        var actual = s.ParseParenthesizedCommaSeparatedOptional(x => x.Next().ToString() ?? "", true);

        Assert.Null(actual);
    }


    [Fact]
    public static void ParseParenthesizedCommaSeparatedOptional_Empty_Allowed()
    {
        var s = new ParserState([new ParenOpen() { Location = new Lexing.Location() }, new ParenClose() { Location = new Lexing.Location() }]);

        var actual = s.ParseParenthesizedCommaSeparatedOptional(x => x.Next().ToString() ?? "", true);

        Assert.NotNull(actual);
        Assert.Empty(actual);
    }

    [Fact]
    public static void ParseParenthesizedCommaSeparatedOptional_Empty_Forbidden()
    {
        var s = new ParserState([
            new ParenOpen() { Location = new Lexing.Location() },
            new ParenClose() { Location = new Lexing.Location() }
        ]);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() =>
        {
            _ = s.ParseParenthesizedCommaSeparatedOptional(x => x.Next().ToString() ?? "", false);
        });

        var expectedMessage = string.Format(ErrorMessages.Err_Parse_ExpectedButFound, "comma_separated_list".Italic(), ")");
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public static void ParseInit_Condition_True()
    {
        var s = new ParserState(TokenSet3);
        var actual = s.ParseInit(s.Next() is ParenOpen, _ => "PARSED", "NOT PARSED");
        var expected = "PARSED";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void ParseInit_Condition_False()
    {
        var s = new ParserState(TokenSet3);
        var actual = s.ParseInit(s.Next() is ParenClose, _ => "PARSED", "NOT PARSED");
        var expected = "NOT PARSED";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void TryParse_Success()
    {
        var state = new ParserState(TokenSet3);
        state.Next();

        var success = state.TryParse(s =>
        {
            if (s.Next() is Word w)
            {
                return w.Value;
            }

            return null;
        }, out string? actual);

        Assert.True(success);
        Assert.Equal("a", actual);
        Assert.True(state.Peek() is Comma);
    }

    [Fact]
    public static void TryParse_Fail_Exception()
    {
        var s = new ParserState(TokenSet3);
        s.Next();

        var success = s.TryParse(state =>
        {
            if (state.Next() is Word)
            {
                throw new ParseException.ExpectedButFound("", "");
            }
            return "NO";
        }, out string? actual);

        Assert.False(success);
        Assert.Null(actual);
        Assert.True(s.Peek() is Word { Value: "a" });
    }

    [Fact]
    public static void TryParse_Fail_Null()
    {
        var s = new ParserState(TokenSet3);
        s.Next();

        var success = s.TryParse(state =>
        {
            if (state.Next() is Word)
            {
                return null;
            }
            return "NO";
        }, out string? actual);

        Assert.False(success);
        Assert.Null(actual);
        Assert.True(s.Peek() is Word { Value: "a" });
    }

    private static SecurityContext ExpectSecurityException(ParserState state)
    {
        return (state.Peek() as Word) switch
        {
            { Keyword: Keyword.DEFINER } => SecurityContext.Definer,
            { Keyword: Keyword.INVOKER } => SecurityContext.Invoker,
            _ => throw state.ExpectedException<SecurityContext>()
        };
    }

    [Fact]
    public static void ExpectedException_EnumLike()
    {
        var s = new ParserState(TokenSet1);

        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() =>
        {
            _ = ExpectSecurityException(s);
        });
        var expectedMessage = string.Format(ErrorMessages.Err_Parse_ExpectedOneOfButFound, string.Join(" | ", "DEFINER", "INVOKER"), "SELECT");
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public static void EmptyTokenSet()
    {
        var s = new ParserState([]);
        var peeked = s.Peek();

        Assert.True(peeked is Eof);
        var expectedLocation = new Lexing.Location(0, 0, 0);
        Assert.Equal(expectedLocation, peeked.Location);
    }

    [Theory]
    [InlineData(ParseContext.None)]
    [InlineData(ParseContext.DomainCheck)]
    [InlineData(ParseContext.DefaultExpression)]
    [InlineData(ParseContext.StringLiteralAsStatement)]
    [InlineData(ParseContext.BeginAtomic)]
    [InlineData(ParseContext.NarrowSqlOnly)]
    [InlineData(ParseContext.BeginAtomic | ParseContext.NarrowSqlOnly)]
    [InlineData(ParseContext.DomainCheck | ParseContext.DefaultExpression | ParseContext.StringLiteralAsStatement | ParseContext.BeginAtomic)]
    public static void EnterContext_RestoresNestedEnablingAndDisabling(ParseContext context)
    {
        var state = new ParserState([]);
        Assert.Equal(ParseContext.None, state.Context);

        using (state.EnterContext(context))
        {
            Assert.Equal(context, state.Context);
            using (state.EnterContext(context))
            {
                Assert.Equal(context, state.Context);
                using (state.EnterContext(disable: context))
                {
                    Assert.Equal(ParseContext.None, state.Context);
                }
                Assert.Equal(context, state.Context);
            }
            Assert.Equal(context, state.Context);
        }

        Assert.Equal(ParseContext.None, state.Context);
    }

    [Fact]
    public static void EnterContext_UpdatesOnlyRequestedFlags()
    {
        var state = new ParserState([]);
        const ParseContext outer = ParseContext.DomainCheck | ParseContext.DefaultExpression | ParseContext.BeginAtomic;
        using (state.EnterContext(outer))
        {
            using (state.EnterContext(ParseContext.StringLiteralAsStatement, disable: ParseContext.DefaultExpression | ParseContext.BeginAtomic))
            {
                Assert.Equal(ParseContext.DomainCheck | ParseContext.StringLiteralAsStatement, state.Context);
                using (state.EnterContext())
                {
                    Assert.Equal(ParseContext.DomainCheck | ParseContext.StringLiteralAsStatement, state.Context);
                }
                Assert.Equal(ParseContext.DomainCheck | ParseContext.StringLiteralAsStatement, state.Context);
            }
            Assert.Equal(outer, state.Context);
        }

        Assert.Equal(ParseContext.None, state.Context);
    }

    [Theory]
    [InlineData(ParseContext.None, ParseContext.BeginAtomic | ParseContext.StringLiteralAsStatement)]
    [InlineData(ParseContext.DomainCheck | ParseContext.DefaultExpression, ParseContext.DomainCheck | ParseContext.BeginAtomic | ParseContext.StringLiteralAsStatement)]
    public static void EnterContext_RestoresPreviousContextOnException(ParseContext initial, ParseContext inner)
    {
        var state = new ParserState([]);
        using (state.EnterContext(initial))
        {
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                using (state.EnterContext(ParseContext.BeginAtomic | ParseContext.StringLiteralAsStatement, disable: ParseContext.DefaultExpression))
                {
                    Assert.Equal(inner, state.Context);
                    throw new InvalidOperationException();
                }
            }));
            Assert.Equal(initial, state.Context);
        }

        Assert.Equal(ParseContext.None, state.Context);
    }

    [Fact]
    public static void EnterStatementScope_RestoresNestedLabelsAndClearsUnlabeledChildren()
    {
        var state = new ParserState([], sourceId: 17);
        var outerToken = new Word("outer_label") { Location = new Lexing.Location() };
        var innerToken = new Word("inner_label") { Location = new Lexing.Location() };

        Assert.Null(state.StatementLabel);
        using (state.EnterStatementScope(outerToken))
        {
            Assert.Equal(outerToken.ToIdentifier(17), state.StatementLabel?.Label);
            using (state.EnterStatementScope(innerToken))
            {
                Assert.Equal(innerToken.ToIdentifier(17), state.StatementLabel?.Label);
                using (state.EnterStatementScope(null))
                {
                    Assert.Null(state.StatementLabel);
                }
                Assert.Equal(innerToken.ToIdentifier(17), state.StatementLabel?.Label);
            }
            Assert.Equal(outerToken.ToIdentifier(17), state.StatementLabel?.Label);
        }
        Assert.Null(state.StatementLabel);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void StatementScope_RestoresEnclosingLabelAfterParseException(bool hasInnerLabel)
    {
        Token[] tokens = hasInnerLabel
            ? [new Word("inner_label") { Location = new Lexing.Location() }, new Colon { Location = new Lexing.Location() }, new Word("SELECT") { Location = new Lexing.Location() }]
            : [new Word("SELECT") { Location = new Lexing.Location() }];
        var state = new ParserState(tokens);
        var outerToken = new Word("outer_label") { Location = new Lexing.Location() };

        using (state.EnterStatementScope(outerToken))
        {
            Assert.ThrowsAny<ParseException>(() => new Parser().ParseStatement(state));
            Assert.Equal(outerToken.ToIdentifier(state.SourceId), state.StatementLabel?.Label);
        }
        Assert.Null(state.StatementLabel);
    }
}
