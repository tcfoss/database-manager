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
    protected virtual bool SupportBacktickQuotedIdentifiers => true;
    protected virtual bool SupportDollarQuotedStrings => true;
    protected virtual bool SupportEscapeStringLiterals => true;
    protected virtual bool SupportUnicodeLiterals => true;
    protected virtual bool SupportBitStringLiterals => true;
    protected virtual bool HexStringsAreBitStrings => false;
    protected virtual bool SupportPositionalParameters => true;
    protected virtual bool SupportNumericDigitSeparators => true;
    protected virtual bool SupportNonDecimalNumericLiterals => true;
    protected virtual bool HexNumbersAreNumericLiterals => false;
    protected virtual bool SupportDotDotToken => false;
    protected virtual bool RejectNumericTrailingJunk => false;
    protected virtual bool SupportPostgresOperatorTokens => true;
    protected virtual bool SupportExclamationAsNot => true;
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
            CharSymbols.Backtick when SupportBacktickQuotedIdentifiers => TokenizeQuotedIdentifier(ref state),
            CharSymbols.Backtick when SupportPostgresOperatorTokens => TokenizeSingleCharacterOperator(ref state, new OperatorToken("`") { Location = state.Location }),

            'N' or 'n' => TokenizeNationalStringLiteral(ref state),
            'B' or 'b' when SupportBitStringLiterals && state.PeekNth(1) == CharSymbols.SingleQuote => TokenizeBitStringLiteral(ref state, false),
            'X' or 'x' when HexStringsAreBitStrings && state.PeekNth(1) == CharSymbols.SingleQuote => TokenizeBitStringLiteral(ref state, true),
            'X' or 'x' => TokenizeHex(ref state),
            'E' or 'e' when SupportEscapeStringLiterals && state.PeekNth(1) == CharSymbols.SingleQuote => TokenizeEscapeStringLiteral(ref state),
            'U' or 'u' when SupportUnicodeLiterals && state.PeekNth(1) == '&' && state.PeekNth(2) is CharSymbols.SingleQuote or CharSymbols.DoubleQuote => TokenizeUnicodeLiteral(ref state, ref runState),

            CharSymbols.SingleQuote => TokenizeStringLiteral(ref state),

            CharSymbols.Dot when SupportDotDotToken && state.PeekNth(1) == CharSymbols.Dot => TokenizeDotDot(ref state),
            >= '0' and <= '9' or CharSymbols.Dot => TokenizeNumber(ref state),

            CharSymbols.ParenOpen => TokenizeSingleCharacter(ref state, new ParenOpen() { Location = state.Location }),
            CharSymbols.ParenClose => TokenizeSingleCharacter(ref state, new ParenClose() { Location = state.Location }),
            CharSymbols.CurlyBraceOpen => TokenizeSingleCharacter(ref state, new BraceOpen() { Location = state.Location }),
            CharSymbols.CurlyBraceClose => TokenizeSingleCharacter(ref state, new BraceClose() { Location = state.Location }),

            CharSymbols.Comma => TokenizeSingleCharacter(ref state, new Comma() { Location = state.Location }),
            CharSymbols.Minus => TokenizeMinus(ref state),
            CharSymbols.Plus => TokenizeSingleCharacterOperator(ref state, new Plus() { Location = state.Location }),
            CharSymbols.Slash => TokenizeSlash(ref state),
            CharSymbols.Asterisk => TokenizeSingleCharacterOperator(ref state, new Asterisk() { Location = state.Location }),
            CharSymbols.Percent => TokenizeSingleCharacterOperator(ref state, new Modulo() { Location = state.Location }),
            CharSymbols.Dollar => TokenizeDollar(ref state),

            CharSymbols.GreaterThan => TokenizeGreaterThan(ref state),
            CharSymbols.LessThan => TokenizeLessThan(ref state),

            CharSymbols.Equal => TokenizeEqual(ref state),

            CharSymbols.Semicolon => TokenizeSingleCharacter(ref state, new Semicolon() { Location = state.Location }),
            CharSymbols.Colon => TokenizeColon(ref state),
            CharSymbols.Pipe => TokenizePipe(ref state),
            CharSymbols.Ampersand => TokenizeSingleCharacterOperator(ref state, new Ampersand() { Location = state.Location }),
            CharSymbols.Caret => TokenizeSingleCharacterOperator(ref state, new Caret() { Location = state.Location }),
            CharSymbols.Tilde => TokenizeTilde(ref state),

            CharSymbols.Backslash => TokenizeSingleCharacter(ref state, new Backslash() { Location = state.Location }),

            CharSymbols.At => TokenizeAt(ref state),

            CharSymbols.ExclamationMark => TokenizeExclamation(ref state),
            CharSymbols.QuestionMark when SupportPostgresOperatorTokens => TokenizeSingleCharacterOperator(ref state, new OperatorToken("?") { Location = state.Location }),
            CharSymbols.Num when SupportPostgresOperatorTokens && (!IsIdentifierStart(character) || !IsIdentifierPart(state.PeekNth(1))) => TokenizeSingleCharacterOperator(ref state, new OperatorToken("#") { Location = state.Location }),

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

    private Token TokenizeSingleCharacterOperator(ref LexerState state, Token token)
    {
        state.Next();
        return TokenizeOperator(ref state, token);
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

    private static Word TokenizeQuotedIdentifier(ref LexerState state)
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
        return new Word(word, quoteStyle) { Location = initialLocation };
    }


    private Word TokenizeIdentifierOrKeyword(ref LexerState state)
    {
        return TokenizeIdentifierOrKeyword(ref state, SigilKind.None);
    }

    private Word TokenizeIdentifierOrKeyword(ref LexerState state, SigilKind sigil)
    {
        Location initialLocation = state.Location;

        state.Next();
        string word = ExtractWordTrustNoNewLines(ref state);

        return new Word(word, QuoteStyle.None, sigil) { Location = initialLocation };
    }

    private Token TokenizeEqual(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        if (state.Peek() == CharSymbols.GreaterThan)
        {
            state.Next();
            return TokenizeOperator(ref state, new FatArrow() { Location = initialLocation });
        }
        return TokenizeOperator(ref state, new Equal() { Location = initialLocation });
    }

    private Token TokenizeAt(ref LexerState state)
    {
        Location initialLocation = state.Location;

        state.Next();
        char next = state.Peek();
        return next switch
        {
            _ when IsIdentifierStart(CharSymbols.At) && IsIdentifierPart(next) => TokenizeIdentifierOrKeyword(ref state, SigilKind.Variable),
            _ => TokenizeOperator(ref state, IsIdentifierStart(CharSymbols.At)
                ? new At() { Location = initialLocation }
                : new OperatorToken("@") { Location = initialLocation }),
        };
    }

    private Token TokenizeExclamation(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();

        if (state.Peek() == CharSymbols.Equal)
        {
            state.Next();
            return TokenizeOperator(ref state, new NotEqual() { Location = initialLocation });
        }
        if (state.Peek() == CharSymbols.Tilde)
        {
            state.Next();
            if (state.Peek() == CharSymbols.Asterisk)
            {
                state.Next();
                return TokenizeOperator(ref state, new NotTildeAsterisk() { Location = initialLocation });
            }
            return TokenizeOperator(ref state, new NotTilde() { Location = initialLocation });
        }
        return TokenizeOperator(ref state, SupportExclamationAsNot
            ? new Exclamation() { Location = initialLocation }
            : new OperatorToken("!") { Location = initialLocation });
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

    private Token TokenizePipe(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        if (state.Peek() == CharSymbols.Pipe)
        {
            state.Next();
            return TokenizeOperator(ref state, new DoublePipe() { Location = initialLocation });
        }
        return TokenizeOperator(ref state, new Pipe() { Location = initialLocation });
    }

    private Token TokenizeTilde(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        if (state.Peek() == CharSymbols.Asterisk)
        {
            state.Next();
            return TokenizeOperator(ref state, new TildeAsterisk() { Location = initialLocation });
        }
        return TokenizeOperator(ref state, new Tilde() { Location = initialLocation });
    }

    private Token TokenizeGreaterThan(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();

        if (state.Peek() == CharSymbols.Equal)
        {
            state.Next();
            return TokenizeOperator(ref state, new GreaterThanOrEqual() { Location = initialLocation });
        }

        return TokenizeOperator(ref state, new GreaterThan() { Location = initialLocation });
    }

    private Token TokenizeLessThan(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();

        char next = state.Peek();
        if (next == CharSymbols.Equal)
        {
            state.Next();
            return TokenizeOperator(ref state, new LessThanOrEqual() { Location = initialLocation });
        }
        else if (next == CharSymbols.GreaterThan)
        {
            state.Next();
            return TokenizeOperator(ref state, new NotEqual() { Location = initialLocation });
        }

        return TokenizeOperator(ref state, new LessThan() { Location = initialLocation });
    }

    private Token TokenizeDollar(ref LexerState state)
    {
        if (SupportPositionalParameters && char.IsAsciiDigit(state.PeekNth(1)))
        {
            Location initialLocation = state.Location;
            state.Next();
            int start = state.Position;
            state.AdvanceThroughDigits();
            if (IsStrictIdentifierStart(state.Peek()) || !int.TryParse(state.SpanFrom(start), out _))
            {
                throw new LexException.ExpectedSymbolSequence(initialLocation);
            }
            return new PositionalParameterToken(new string(state.SpanFrom(start))) { Location = initialLocation };
        }

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

    private Token TokenizeNumber(ref LexerState state)
    {
        Location initialLocation = state.Location;
        int startPosition = state.Position;

        if (SupportNonDecimalNumericLiterals && TryExtractRadix(ref state, HexNumbersAreNumericLiterals, out int radix))
        {
            return TokenizeRadixNumber(ref state, radix, initialLocation, startPosition);
        }

        if (state.Peek() == CharSymbols.Zero && state.PeekNth(1) is 'x' or 'X')
        {
            state.Next(); // Consume the '0'
            state.Next(); // Consume the 'x' or 'X'
            ReadOnlySpan<char> hex = state.TakeWhileHexDigit();
            return new HexStringLiteral(new string(hex)) { Location = initialLocation };
        }

        AdvanceThroughNumericDigits(ref state);

        if (state.Peek() == CharSymbols.Dot
            // If `..` is encountered and supported, do not split into two adjacent numeric literals
            && !(SupportDotDotToken && state.PeekNth(1) == CharSymbols.Dot))
        {
            state.Next();
        }

        AdvanceThroughNumericDigits(ref state);

        if (state.SpanFrom(startPosition) is [CharSymbols.Dot])
        {
            return new Dot() { Location = initialLocation };
        }

        if (state.Peek() is 'e' or 'E')
        {
            TokenizeExponent(ref state);
        }

        if (RejectNumericTrailingJunk && IsStrictIdentifierStart(state.Peek()))
        {
            throw new LexException.ExpectedSymbolSequence(initialLocation);
        }
        bool isLong = state.Peek() is 'l' or 'L';
        string text = new(state.SpanFrom(startPosition));
        if (isLong)
        {
            state.Next();
        }
        return new NumericLiteral(text, isLong) { Location = initialLocation };
    }

    private static bool TryExtractRadix(ref LexerState state, bool hexAsNumber, out int radix)
    {
        if (state.Peek() != CharSymbols.Zero)
        {
            radix = 10;
            return false;
        }

        radix = state.PeekNth(1) switch
        {
            'x' or 'X' when hexAsNumber => 16,
            'b' or 'B' => 2,
            'o' or 'O' => 8,
            _ => 10,
        };

        if (radix != 10)
        {
            state.Next(); // Consume the '0'
            state.Next(); // Consume the radix prefix ('x', 'b', or 'o')
            return true;
        }
        return false;
    }

    private NumericLiteral TokenizeRadixNumber(ref LexerState state, int radix, Location initialLocation, int startPosition)
    {
        if (SupportNumericDigitSeparators && state.Peek() == CharSymbols.Underscore)
        {
            state.Next();
        }
        if (!IsNumericDigit(state.Peek(), radix))
        {
            throw new LexException.ExpectedSymbolSequence(initialLocation);
        }
        AdvanceThroughNumericDigits(ref state, radix);
        if ((RejectNumericTrailingJunk && IsStrictIdentifierStart(state.Peek()))
            || char.IsAsciiDigit(state.Peek()))
        {
            throw new LexException.ExpectedSymbolSequence(initialLocation);
        }
        return new NumericLiteral(new string(state.SpanFrom(startPosition))) { Location = initialLocation };
    }

    private void AdvanceThroughNumericDigits(ref LexerState state, int radix = 10)
    {
        while (true)
        {
            int start = state.Position;
            if (radix == 10)
            {
                state.AdvanceThroughDigits();
            }
            else
            {
                while (IsNumericDigit(state.Peek(), radix))
                {
                    state.Next();
                }
            }
            if (state.Position == start || !SupportNumericDigitSeparators || state.Peek() != CharSymbols.Underscore
                || !IsNumericDigit(state.PeekNth(1), radix))
            {
                return;
            }
            state.Next();
        }
    }

    private static bool IsNumericDigit(char character, int radix)
    {
        return radix switch
        {
            16 => char.IsAsciiHexDigit(character),
            8 => character is >= '0' and <= '7',
            2 => character is '0' or '1',
            _ => char.IsAsciiDigit(character)
        };
    }

    private void TokenizeExponent(ref LexerState state)
    {
        int offset = 1; // We know the current character is 'e' or 'E'

        if (state.PeekNth(offset) is CharSymbols.Plus or CharSymbols.Minus)
        {
            offset++;
        }

        if (!char.IsAsciiDigit(state.PeekNth(offset)))
        {
            // 'e' or 'E' is not followed by a valid exponent, so it's not part of the number.
            return;
        }

        state.Next(); // Consume the 'e' or 'E'
        if (offset > 1)
        {
            state.Next(); // Consume the '+' or '-'
        }

        AdvanceThroughNumericDigits(ref state);
    }

    private static DotDot TokenizeDotDot(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();
        state.Next();
        return new DotDot { Location = initialLocation };
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

    private static BitStringLiteral TokenizeBitStringLiteral(ref LexerState state, bool isHexadecimal)
    {
        Location initialLocation = state.Location;
        state.Next();
        return new BitStringLiteral(ExtractString(ref state, false), isHexadecimal) { Location = initialLocation };
    }

    private Token TokenizeUnicodeLiteral(ref LexerState state, ref TRunState runState)
    {
        Location initialLocation = state.Location;
        state.Next();
        state.Next();
        char quote = state.Peek();
        string raw = ExtractString(ref state, false, quote);
        char? escape = ParseUnicodeEscapeClause(ref state, ref runState);
        int length = state.Position - initialLocation.Position;
        if (quote == CharSymbols.SingleQuote)
        {
            return new UnicodeStringLiteral(raw) { EscapeCharacter = escape, SourceLength = length, Location = initialLocation };
        }
        try
        {
            string name = PgStringDecoders.DecodeUnicodeString(raw, escape ?? '\\', quote);
            if (name.Length == 0)
            {
                throw new FormatException();
            }
            return new Word(name.Replace("\"", "\"\"", StringComparison.Ordinal), QuoteStyle.Ansi)
            {
                SourceLength = length,
                Location = initialLocation
            };
        }
        catch (FormatException)
        {
            throw new LexException.ExpectedSymbolSequence(initialLocation);
        }
    }

    private char? ParseUnicodeEscapeClause(ref LexerState state, ref TRunState runState)
    {
        LexerState candidate = state;
        TRunState candidateRunState = runState;
        SkipUnicodeTrivia(ref candidate, ref candidateRunState);
        if (candidate.Peek() is not ('U' or 'u'))
        {
            return null;
        }
        Word keyword = TokenizeIdentifierOrKeyword(ref candidate);
        if (keyword.Keyword != Keyword.UESCAPE)
        {
            return null;
        }
        SkipUnicodeTrivia(ref candidate, ref candidateRunState);
        if (candidate.Peek() is 'U' or 'u')
        {
            throw new LexException.ExpectedSymbolSequence(candidate.Location);
        }
        Location literalLocation = candidate.Location;
        Token literal = candidate.Peek() == CharSymbols.SingleQuote
            ? new StringLiteral(ExtractString(ref candidate, false)) { Location = literalLocation }
            : NextToken(ref candidate, ref candidateRunState);
        try
        {
            string text = literal switch
            {
                StringLiteral textLiteral => textLiteral.Value.Replace("''", "'", StringComparison.Ordinal),
                EscapeStringLiteral textLiteral => new Value.EscapeString(textLiteral.Value).DecodedValue,
                DollarQuotedStringLiteral textLiteral => textLiteral.Value,
                _ => throw new FormatException()
            };
            if (text.Length != 1)
            {
                throw new FormatException();
            }
            PgStringDecoders.DecodeUnicodeString("", text[0]);
            state = candidate;
            runState = candidateRunState;
            return text[0];
        }
        catch (FormatException)
        {
            throw new LexException.ExpectedSymbolSequence(literal.Location);
        }
    }

    private void SkipUnicodeTrivia(ref LexerState state, ref TRunState runState)
    {
        while (state.Peek() is CharSymbols.Space or CharSymbols.Tab or CharSymbols.NewLine or CharSymbols.CarriageReturn
            || (state.Peek() == CharSymbols.Minus && state.PeekNth(1) == CharSymbols.Minus)
            || (state.Peek() == CharSymbols.Slash && state.PeekNth(1) == CharSymbols.Asterisk))
        {
            NextToken(ref state, ref runState);
        }
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

    private Token TokenizeSlash(ref LexerState state)
    {
        Location startLocation = state.Location;
        state.Next();

        return state.Peek() switch
        {
            CharSymbols.Asterisk => TokenizeBlockComment(ref state, startLocation),
            _ => TokenizeOperator(ref state, new Slash() { Location = startLocation })
        };
    }

    private Token TokenizeMinus(ref LexerState state)
    {
        Location startLocation = state.Location;
        state.Next();

        return state.Peek() switch
        {
            CharSymbols.Minus => TokenizeLineComment(ref state, startLocation),
            _ => TokenizeOperator(ref state, new Minus() { Location = startLocation })
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

    private Token TokenizeOperator(ref LexerState state, Token prefix)
    {
        if (!SupportPostgresOperatorTokens)
        {
            return prefix;
        }
        Location location = prefix.Location;
        int prefixLength = state.Position - location.Position;
        int length = prefixLength;
        bool hasNonStandardCharacter = false;
        foreach (char character in state.SpanFrom(location.Position))
        {
            hasNonStandardCharacter |= character is '~' or '!' or '@' or '#' or '%' or '^' or '&' or '|' or '`' or '?';
        }
        while (IsOperatorCharacter(state.PeekNth(length - prefixLength))
            && (!SupportBacktickQuotedIdentifiers || state.PeekNth(length - prefixLength) != CharSymbols.Backtick)
            && !IsCommentStart(state, length - prefixLength))
        {
            hasNonStandardCharacter |= state.PeekNth(length - prefixLength) is '~' or '!' or '@' or '#' or '%' or '^' or '&' or '|' or '`' or '?';
            length++;
        }
        if (!hasNonStandardCharacter)
        {
            while (length > prefixLength && state.PeekNth(length - prefixLength - 1) is '+' or '-')
            {
                length--;
            }
        }
        if (length > 63)
        {
            throw new LexException.ExpectedSymbolSequence(location);
        }
        if (length == prefixLength)
        {
            return prefix;
        }
        for (int index = prefixLength; index < length; index++)
        {
            state.Next();
        }
        string symbol = new(state.SpanFrom(location.Position));
        return new OperatorToken(symbol) { Location = location };
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

    protected static bool IsStrictIdentifierStart(char character)
    {
        return char.IsLetter(character) || character is CharSymbols.Underscore;
    }

    private static bool IsOperatorCharacter(char character)
    {
        return character is '+' or '-' or '*' or '/' or '<' or '>' or '=' or '~' or '!' or '@' or '#' or '%' or '^' or '&' or '|' or '`' or '?';
    }

    private static bool IsCommentStart(LexerState state, int offset)
    {
        return (state.PeekNth(offset), state.PeekNth(offset + 1)) is ('-', '-') or ('/', '*');
    }
}
