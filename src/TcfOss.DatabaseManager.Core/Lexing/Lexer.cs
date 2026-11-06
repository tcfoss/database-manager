using System.Buffers;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using NonSqlComponent = TcfOss.DatabaseManager.Core.Statements.Components.NonSql;

namespace TcfOss.DatabaseManager.Core.Lexing;

public class Lexer<TRunState> : ILexer
    where TRunState : struct
{
    private readonly Func<char, bool> _isIdentifierPart;
    protected virtual bool SupportBackslashQuoteEscape => true;
    protected virtual bool SupportBracketQuotedIdentifiers => true;
    protected virtual bool SupportDollarQuotedStrings => true;
    protected virtual bool SupportEscapeStringLiterals => true;
    protected virtual SearchValues<char> IdentifierAsciiCharacters => LexerState.s_standardIdentifierCharacters;

    public Lexer()
    {
        _isIdentifierPart = IsIdentifierPart;
    }

    public virtual List<Token> Tokenize(string sql)
    {
        return Tokenize(sql.AsSpan());
    }

    public virtual List<Token> Tokenize(ReadOnlySpan<char> sql)
    {
        List<Token> tokens = [];
        NonSqlList? nonSql = null;
        Location nonSqlLocation = default;
        var state = new LexerState(sql);
        TRunState runState = CreateRunState();

        Token curr;
        while ((curr = NextToken(ref state, ref runState)) is not Eof)
        {
            if (curr is NonSql ns)
            {
                if (nonSql == null)
                {
                    nonSql = new NonSqlList { Start = ns.Location.Position };
                    nonSqlLocation = ns.Location;
                }
                nonSql.Add(ns.Value);
                nonSql.End = ns.Location.Position + ns.Length;
                continue;
            }

            if (nonSql != null)
            {
                curr.PreNonSql = nonSql;
                nonSql = null;
            }
            PostProcessToken(curr, tokens, ref runState);
        }

        PostProcessStream(tokens, ref runState);

        if (nonSql != null)
        {
            tokens.Add(new NonSqlContainer(nonSql) { Location = nonSqlLocation });
        }

        return tokens;
    }

    private static TRunState CreateRunState()
    {
        return new TRunState();
    }

    protected virtual void PostProcessToken(Token token, List<Token> tokens, ref TRunState runState)
    {
        tokens.Add(token);
    }

    protected virtual void PostProcessStream(List<Token> tokens, ref TRunState runState)
    {
        // Do nothing by default.
    }

    protected virtual Token NextToken(ref LexerState state, ref TRunState runState)
    {
        char character = state.Peek();

        return character switch
        {
            CharSymbols.Space => TokenizeSpaces(ref state),
            CharSymbols.Tab => TokenizeTabs(ref state),
            CharSymbols.NewLine or CharSymbols.CarriageReturn => TokenizeNewlines(ref state),

            CharSymbols.SquareBracketOpen when SupportBracketQuotedIdentifiers => TokenizeQuotedIdentifier(ref state),
            CharSymbols.SquareBracketOpen => TokenizeSingleCharacter(ref state, new BracketOpen() { Location = state.Location }),
            CharSymbols.SquareBracketClose when !SupportBracketQuotedIdentifiers => TokenizeSingleCharacter(ref state, new BracketClose() { Location = state.Location }),
            CharSymbols.DoubleQuote => TokenizeQuotedIdentifier(ref state),
            CharSymbols.Backtick => TokenizeQuotedIdentifier(ref state),

            'N' or 'n' => TokenizeNationalStringLiteral(ref state),
            'X' or 'x' => TokenizeHex(ref state),
            'E' or 'e' when SupportEscapeStringLiterals && state.PeekNth(1) == CharSymbols.SingleQuote => TokenizeEscapeStringLiteral(ref state),

            CharSymbols.SingleQuote => TokenizeStringLiteral(ref state),

            >= '0' and <= '9' or CharSymbols.Dot => TokenizeNumber(ref state),

            CharSymbols.ParenOpen => TokenizeSingleCharacter(ref state, new ParenOpen() { Location = state.Location }),
            CharSymbols.ParenClose => TokenizeSingleCharacter(ref state, new ParenClose() { Location = state.Location }),
            CharSymbols.CurlyBraceOpen => TokenizeSingleCharacter(ref state, new BraceOpen() { Location = state.Location }),
            CharSymbols.CurlyBraceClose => TokenizeSingleCharacter(ref state, new BraceClose() { Location = state.Location }),

            CharSymbols.Comma => TokenizeSingleCharacter(ref state, new Comma() { Location = state.Location }),
            CharSymbols.Minus => TokenizeMinus(ref state),
            CharSymbols.Plus => TokenizeSingleCharacter(ref state, new Plus() { Location = state.Location }),
            CharSymbols.Slash => TokenizeSlash(ref state),
            CharSymbols.Asterisk => TokenizeSingleCharacter(ref state, new Asterisk() { Location = state.Location }),
            CharSymbols.Percent => TokenizeSingleCharacter(ref state, new Modulo() { Location = state.Location }),
            CharSymbols.Dollar => TokenizeDollar(ref state),

            CharSymbols.GreaterThan => TokenizeGreaterThan(ref state),
            CharSymbols.LessThan => TokenizeLessThan(ref state),

            CharSymbols.Equal => TokenizeEqual(ref state),

            CharSymbols.Semicolon => TokenizeSingleCharacter(ref state, new Semicolon() { Location = state.Location }),
            CharSymbols.Colon => TokenizeColon(ref state),
            CharSymbols.Pipe => TokenizePipe(ref state),
            CharSymbols.Ampersand => TokenizeSingleCharacter(ref state, new Ampersand() { Location = state.Location }),
            CharSymbols.Caret => TokenizeSingleCharacter(ref state, new Caret() { Location = state.Location }),
            CharSymbols.Tilde => TokenizeTilde(ref state),

            CharSymbols.Backslash => TokenizeSingleCharacter(ref state, new Backslash() { Location = state.Location }),

            CharSymbols.At => TokenizeAt(ref state),

            CharSymbols.ExclamationMark => TokenizeExclamation(ref state),

            CharSymbols.EndOfFile => new Eof() { Location = state.Location },

            _ when IsIdentifierStart(character) => TokenizeIdentifierOrKeyword(ref state),

            _ => throw new LexException.UnexpectedCharacter(character, state.Location)
        };

    }

    private static Token TokenizeSingleCharacter(ref LexerState state, Token token)
    {
        state.Next();
        return token;
    }

    private static NonSql TokenizeSpaces(ref LexerState state)
    {
        Location location = state.Location;
        int count = state.AdvanceWhileTrustNoNewLines(CharSymbols.Space);
        return new NonSql(WhitespaceCache.Spaces(count), count) { Location = location };
    }

    private static NonSql TokenizeTabs(ref LexerState state)
    {
        Location location = state.Location;
        int count = state.AdvanceWhileTrustNoNewLines(CharSymbols.Tab);
        return new NonSql(WhitespaceCache.Tabs(count), count) { Location = location };
    }

    private static NonSql TokenizeNewlines(ref LexerState state)
    {
        Location location = state.Location;
        int count = 0;
        while (true)
        {
            char curr = state.Peek();
            if (curr == CharSymbols.NewLine)
            {
                state.Next();
            }
            else if (curr == CharSymbols.CarriageReturn)
            {
                state.Next();
                if (state.Peek() == CharSymbols.NewLine)
                {
                    state.Next();
                }
            }
            else
            {
                break;
            }
            count++;
        }
        return new NonSql(WhitespaceCache.Newlines(count), state.Position - location.Position) { Location = location };
    }

    private static Label? MaybeTokenizeLabel(ref LexerState state, string word, Location initialLocation, QuoteStyle? quoteStyle = null)
    {
        if (state.Peek() == CharSymbols.Colon && state.PeekNth(1) is not (CharSymbols.Colon or CharSymbols.Equal or CharSymbols.EndOfFile))
        {
            state.Next();
            return new Label(new Identifier(word, quoteStyle ?? QuoteStyle.None)) { Location = initialLocation };
        }
        return null;
    }

    private static Token TokenizeQuotedIdentifier(ref LexerState state)
    {
        Location initialLocation = state.Location;
        char quoteStart = state.Next();

        char quoteEnd;
        QuoteStyle quoteStyle;

        switch (quoteStart)
        {
            case CharSymbols.DoubleQuote:
                quoteEnd = CharSymbols.DoubleQuote;
                quoteStyle = QuoteStyle.Ansi;
                break;
            case CharSymbols.SquareBracketOpen:
                quoteEnd = CharSymbols.SquareBracketClose;
                quoteStyle = QuoteStyle.Brackets;
                break;
            case CharSymbols.Backtick:
                quoteEnd = CharSymbols.Backtick;
                quoteStyle = QuoteStyle.Backticks;
                break;
            default:
                throw new DefensiveGuardException("identifier quote character", quoteStart.ToString());
        }

        int startPosition = state.Position;

        while (true)
        {
            state.AdvanceThroughNonQuote(quoteEnd);
            if (state.Peek() == CharSymbols.EndOfFile)
            {
                throw new LexException.UnterminatedQuotedIdentifier(quoteEnd, initialLocation);
            }

            state.Next();
            if (state.Peek() != quoteEnd)
            {
                break;
            }
            state.Next();
        }

        string word = new(state.SpanFrom(startPosition, state.Position - 1));
        Label? label = MaybeTokenizeLabel(ref state, word, initialLocation, quoteStyle);
        if (label != null)
        {
            return label;
        }

        return new Word(word, quoteStyle) { Location = initialLocation };
    }


    private Token TokenizeIdentifierOrKeyword(ref LexerState state)
    {
        return TokenizeIdentifierOrKeyword(ref state, SigilKind.None);
    }

    private Token TokenizeIdentifierOrKeyword(ref LexerState state, SigilKind sigil)
    {
        Location initialLocation = state.Location;

        state.Next();
        string word = ExtractWordTrustNoNewLines(ref state);

        if (sigil == SigilKind.None)
        {
            Label? label = MaybeTokenizeLabel(ref state, word, initialLocation);
            if (label != null)
            {
                return label;
            }
        }

        return new Word(word, QuoteStyle.None, sigil) { Location = initialLocation };
    }

    private static Token TokenizeEqual(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        if (state.Peek() == CharSymbols.GreaterThan)
        {
            state.Next();
            return new FatArrow() { Location = initialLocation };
        }
        return new Equal() { Location = initialLocation };
    }

    private Token TokenizeAt(ref LexerState state)
    {
        Location initialLocation = state.Location;

        state.Next();
        char next = state.Peek();
        return next switch
        {
            _ when IsIdentifierStart(CharSymbols.At) && IsIdentifierPart(next) => TokenizeIdentifierOrKeyword(ref state, SigilKind.Variable),
            _ => new At() { Location = initialLocation },
        };
    }

    private static Token TokenizeExclamation(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();

        if (state.Peek() == CharSymbols.Equal)
        {
            state.Next();
            return new NotEqual() { Location = initialLocation };
        }
        if (state.Peek() == CharSymbols.Tilde)
        {
            state.Next();
            if (state.Peek() == CharSymbols.Asterisk)
            {
                state.Next();
                return new NotTildeAsterisk() { Location = initialLocation };
            }
            return new NotTilde() { Location = initialLocation };
        }
        return new Exclamation() { Location = initialLocation };
    }

    private static Token TokenizeColon(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        if (state.Peek() == CharSymbols.Equal)
        {
            state.Next();
            return new Walrus() { Location = initialLocation };
        }
        if (state.Peek() == CharSymbols.Colon)
        {
            state.Next();
            return new DoubleColon() { Location = initialLocation };
        }
        return new Colon() { Location = initialLocation };
    }

    private static Token TokenizePipe(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        if (state.Peek() == CharSymbols.Pipe)
        {
            state.Next();
            return new DoublePipe() { Location = initialLocation };
        }
        return new Pipe() { Location = initialLocation };
    }

    private static Token TokenizeTilde(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        if (state.Peek() == CharSymbols.Asterisk)
        {
            state.Next();
            return new TildeAsterisk() { Location = initialLocation };
        }
        return new Tilde() { Location = initialLocation };
    }

    private static Token TokenizeGreaterThan(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();

        if (state.Peek() == CharSymbols.Equal)
        {
            state.Next();
            return new GreaterThanOrEqual() { Location = initialLocation };
        }

        return new GreaterThan() { Location = initialLocation };
    }

    private static Token TokenizeLessThan(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();

        char next = state.Peek();
        if (next == CharSymbols.Equal)
        {
            state.Next();
            return new LessThanOrEqual() { Location = initialLocation };
        }
        else if (next == CharSymbols.GreaterThan)
        {
            state.Next();
            return new NotEqual() { Location = initialLocation };
        }

        return new LessThan() { Location = initialLocation };
    }

    private Token TokenizeDollar(ref LexerState state)
    {
        if (SupportDollarQuotedStrings)
        {
            if (MaybeTokenizeDollarQuotedString(ref state) is { } token)
            {
                return token;
            }
        }

        if (IsIdentifierStart(CharSymbols.Dollar))
        {
            return TokenizeIdentifierOrKeyword(ref state);
        }

        throw new LexException.UnexpectedCharacter(CharSymbols.Dollar, state.Location);
    }

    private DollarQuotedStringLiteral? MaybeTokenizeDollarQuotedString(ref LexerState state)
    {
        Location initialLocation = state.Location;
        LexerState candidate = state.Clone();
        candidate.Next();
        int tagStart = candidate.Position;
        if (IsStrictIdentifierStart(candidate.Peek()))
        {
            candidate.Next();
            candidate.TakeWhileIdentifierPart(LexerState.s_standardIdentifierCharacters, _isIdentifierPart);
        }
        if (candidate.Peek() != CharSymbols.Dollar)
        {
            return null;
        }
        int tagEnd = candidate.Position;
        string tag = new(candidate.SpanFrom(tagStart, tagEnd));
        string delimiter = $"${tag}$";
        candidate = state.Clone();
        string value = ExtractStringCustomQuotes(ref candidate, delimiter);
        state = candidate;
        return new DollarQuotedStringLiteral(tag, value) { Location = initialLocation };
    }

    private static Token TokenizeNumber(ref LexerState state)
    {
        Location initialLocation = state.Location;

        if (state.Peek() == CharSymbols.Zero && state.PeekNth(1) is 'x' or 'X')
        {
            state.Next(); // Consume the '0'
            state.Next(); // Consume the 'x' or 'X'
            ReadOnlySpan<char> hex = state.TakeWhileHexDigit();
            return new HexStringLiteral(new string(hex)) { Location = initialLocation };
        }

        int startPosition = state.Position;

        state.AdvanceThroughDigits();

        if (state.Peek() == CharSymbols.Dot)
        {
            state.Next();
        }

        state.AdvanceThroughDigits();

        if (state.SpanFrom(startPosition) is [CharSymbols.Dot])
        {
            return new Dot() { Location = initialLocation };
        }

        if (state.Peek() is 'e' or 'E')
        {
            TokenizeExponent(ref state);
        }

        bool isLong = state.Peek() is 'l' or 'L';
        string text = new(state.SpanFrom(startPosition));
        if (isLong)
        {
            state.Next();
        }
        return new NumericLiteral(text, isLong) { Location = initialLocation };
    }

    private static void TokenizeExponent(ref LexerState state)
    {
        int offset = 1; // We know the current character is 'e' or 'E'

        if (state.PeekNth(offset) is CharSymbols.Plus or CharSymbols.Minus)
        {
            offset++;
        }

        if (!char.IsDigit(state.PeekNth(offset)))
        {
            // 'e' or 'E' is not followed by a valid exponent, so it's not part of the number.
            return;
        }

        state.Next(); // Consume the 'e' or 'E'
        if (offset > 1)
        {
            state.Next(); // Consume the '+' or '-'
        }

        state.AdvanceThroughDigits();
    }

    private StringLiteral TokenizeStringLiteral(ref LexerState state)
    {
        Location initialLocation = state.Location;

        string contents = ExtractString(ref state, SupportBackslashQuoteEscape);

        return new StringLiteral(contents) { Location = initialLocation };
    }

    private static EscapeStringLiteral TokenizeEscapeStringLiteral(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        return new EscapeStringLiteral(ExtractString(ref state, true, supportBackslashEscape: true)) { Location = initialLocation };
    }

    private Token TokenizeNationalStringLiteral(ref LexerState state)
    {
        Location initialLocation = state.Location;

        state.Next();
        return state.Peek() switch
        {
            CharSymbols.SingleQuote => new NationalStringLiteral(ExtractString(ref state, SupportBackslashQuoteEscape)) { Location = initialLocation },
            _ => new Word(ExtractWordTrustNoNewLines(ref state)) { Location = initialLocation }
        };
    }

    private Token TokenizeHex(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();

        if (state.Peek() != CharSymbols.SingleQuote)
        {
            return new Word(ExtractWordTrustNoNewLines(ref state)) { Location = initialLocation };
        }

        string hex = ExtractString(ref state, SupportBackslashQuoteEscape);
        return new HexStringLiteral(hex) { Location = initialLocation };
    }

    private static Token TokenizeSlash(ref LexerState state)
    {
        Location startLocation = state.Location;
        state.Next();

        return state.Peek() switch
        {
            CharSymbols.Asterisk => TokenizeBlockComment(ref state, startLocation),
            _ => new Slash() { Location = startLocation }
        };
    }

    private static Token TokenizeMinus(ref LexerState state)
    {
        Location startLocation = state.Location;
        state.Next();

        return state.Peek() switch
        {
            CharSymbols.Minus => TokenizeLineComment(ref state, startLocation),
            _ => new Minus() { Location = startLocation }
        };
    }

    private static NonSql TokenizeLineComment(ref LexerState state, Location startLocation)
    {
        state.Next(); // Consume the second '-'

        string comment = ExtractLineComment(ref state);
        return new NonSql(new NonSqlComponent.LineComment(comment, "--"), state.Position - startLocation.Position) { Location = startLocation };
    }

    private static NonSql TokenizeBlockComment(ref LexerState state, Location startLocation)
    {
        state.Next(); // Consume the '*'

        int startPosition = state.Position;

        char lastChar = CharSymbols.Space;
        int nested = 1;

        while (true)
        {
            char curr = state.Next();

            if (curr == CharSymbols.EndOfFile)
            {
                throw new LexException.UnterminatedBlockComment(state.Location);
            }

            switch (lastChar)
            {
                case CharSymbols.Slash when curr == CharSymbols.Asterisk:
                    nested++;
                    break;
                case CharSymbols.Asterisk when curr == CharSymbols.Slash:
                    nested--;
                    if (nested == 0)
                    {
                        string body = new(state.SpanFrom(startPosition, state.Position - 2));
                        return new NonSql(new NonSqlComponent.BlockComment(body), state.Position - startLocation.Position) { Location = startLocation };
                    }
                    break;
            }
            lastChar = curr;
        }
    }

    private static string ExtractLineComment(ref LexerState state)
    {
        int startPosition = state.Position;
        state.AdvanceToEndOfLine();

        if (state.IsAtEnd)
        {
            return new string(state.SpanFrom(startPosition)) + CharSymbols.EndOfFile;
        }

        // Consume the newline, or an embedded NUL, which stays in the body as-is.
        state.Next();
        return new string(state.SpanFrom(startPosition));
    }

    private static string ExtractString(ref LexerState state, bool supportBackslashQuoteEscape, char quoteChar = CharSymbols.SingleQuote, bool supportBackslashEscape = false)
    {
        Location startLocation = state.Location;

        state.Next(); // Consume the opening quotation mark.

        // Try allocation-free search first.
        int startPosition = state.Position;
        state.AdvanceThroughOrdinaryStringCharacters(quoteChar, supportBackslashQuoteEscape);
        char first = state.Peek();

        if (first == CharSymbols.EndOfFile)
        {
            throw new LexException.UnterminatedString(startLocation);
        }

        if (first == quoteChar && state.PeekNth(1) != quoteChar)
        {
            // String was closed without any escaping. Return it.
            string result = new(state.SpanFrom(startPosition));
            state.Next(); // Consume the closing quotation mark.
            return result;
        }

        // Escaped quote or backslash. Have to do it the slower way.
        bool escapeNext = false;
        while (true)
        {
            if (!escapeNext)
            {
                state.AdvanceThroughOrdinaryStringCharacters(quoteChar, supportBackslashQuoteEscape);
            }
            char curr = state.Peek();

            if (curr == CharSymbols.EndOfFile)
            {
                throw new LexException.UnterminatedString(startLocation);
            }

            if (curr == quoteChar)
            {
                if (escapeNext)
                {
                    state.Next();
                    escapeNext = false;
                }
                else
                {
                    int quoteStart = state.Position;
                    while (state.Peek() == quoteChar)
                    {
                        state.Next();
                    }

                    int numberQuotes = state.Position - quoteStart;
                    if (numberQuotes % 2 != 0)
                    {
                        return new string(state.SpanFrom(startPosition, state.Position - 1));
                    }
                }
            }
            else if (curr == CharSymbols.Backslash && (!supportBackslashEscape || !escapeNext)) // If backslash-escapes are off, state.AdvanceThroughOrdinaryStringCharacters will eat it
            {
                state.Next();
                escapeNext = true;
            }
            else
            {
                state.Next();
                escapeNext = false;
            }
        }
    }

    private static string ExtractStringCustomQuotes(ref LexerState state, string delimiter)
    {
        Location startLocation = state.Location;
        state.TakeIfSequence(delimiter);

        int bodyStart = state.Position;
        while (state.Peek() != CharSymbols.EndOfFile)
        {
            if (state.TakeIfSequence(delimiter))
            {
                return new string(state.SpanFrom(bodyStart, state.Position - delimiter.Length));
            }
            state.Next();
        }

        throw new LexException.UnterminatedString(startLocation);
    }

    private string ExtractWordTrustNoNewLines(ref LexerState state)
    {
        int start = state.Position - 1;
        state.TakeWhileIdentifierPart(IdentifierAsciiCharacters, _isIdentifierPart);
        return new string(state.SpanFrom(start, state.Position));
    }

    protected virtual bool IsIdentifierStart(char character)
    {
        return char.IsLetter(character)
            || character is CharSymbols.Underscore or CharSymbols.Num or CharSymbols.At;
    }

    protected virtual bool IsIdentifierPart(char character)
    {
        return char.IsLetter(character) || char.IsDigit(character) || character is CharSymbols.Underscore;
    }

    protected bool IsStrictIdentifierStart(char character)
    {
        return char.IsLetter(character) || character is CharSymbols.Underscore;
    }
}
