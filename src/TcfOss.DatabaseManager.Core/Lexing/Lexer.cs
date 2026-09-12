using System.Buffers;
using System.Text;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.Core.Lexing;

public class Lexer<TRunState> : ILexer
    where TRunState : struct
{
    private const int StackAllocationThreshold = 256;

    public virtual List<Token> Tokenize(string sql) => Tokenize(sql.AsSpan());

    public virtual List<Token> Tokenize(ReadOnlySpan<char> sql)
    {
        List<Token> tokens = [];
        ValueList<NonSql> nonSql = [];
        var state = new LexerState(sql);
        TRunState runState = CreateRunState();

        Token curr;
        while ((curr = NextToken(ref state, ref runState)) is not Eof)
        {
            if (curr is NonSql ns)
            {
                nonSql.Add(ns);
                continue;
            }

            if (nonSql.Count > 0)
            {
                curr.PreNonSql = nonSql;
                nonSql = [];
            }
            PostProcessToken(curr, tokens, ref runState);
        }

        if (nonSql.Count > 0)
        {
            tokens.Add(new NonSqlContainer(nonSql) { Location = nonSql[0].Location });
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

    protected virtual Token NextToken(ref LexerState state, ref TRunState runState)
    {
        char character = state.Peek();

        return character switch
        {
            CharSymbols.Space => TokenizeSingleCharacter(ref state, new NonSql(NonSqlType.Space) { Location = state.Location }),
            CharSymbols.Tab => TokenizeSingleCharacter(ref state, new NonSql(NonSqlType.Tab) { Location = state.Location }),
            CharSymbols.NewLine => TokenizeSingleCharacter(ref state, new NonSql(NonSqlType.Newline) { Location = state.Location }),
            CharSymbols.CarriageReturn => TokenizeCarriageReturn(ref state),

            CharSymbols.SquareBracketOpen => TokenizeQuotedIdentifier(ref state),
            CharSymbols.DoubleQuote => TokenizeQuotedIdentifier(ref state),
            CharSymbols.Backtick => TokenizeQuotedIdentifier(ref state),

            'N' or 'n' => TokenizeNationalStringLiteral(ref state),
            'X' or 'x' => TokenizeHex(ref state),

            CharSymbols.SingleQuote => TokenizeStringLiteral(ref state),

            _ when char.IsDigit(character) || character == CharSymbols.Dot => TokenizeNumber(ref state),

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

            CharSymbols.GreaterThan => TokenizeGreaterThan(ref state),
            CharSymbols.LessThan => TokenizeLessThan(ref state),

            CharSymbols.Equal => TokenizeEqual(ref state),

            CharSymbols.Semicolon => TokenizeSingleCharacter(ref state, new Semicolon() { Location = state.Location }),
            CharSymbols.Colon => TokenizeColon(ref state),

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

    private static NonSql TokenizeCarriageReturn(ref LexerState state)
    {
        Location location = state.Location;
        state.Next();
        if (state.Peek() == CharSymbols.NewLine)
        {
            state.Next();
        }
        return new NonSql(NonSqlType.Newline) { Location = location };
    }

    private static Label? MaybeTokenizeLabel(ref LexerState state, string word, Location initialLocation, QuoteStyle? quoteStyle = null)
    {
        if (state.Peek() == CharSymbols.Colon && state.PeekNth(1) is not (CharSymbols.Colon or CharSymbols.Equal))
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
                throw new InvalidOperationException($"Could not determine character to close quotation starting with {quoteStart}.");
        }

        char? lastChar = null;
        var chars = new StringBuilder();

        char curr;
        do
        {
            curr = state.Peek();
            state.Next();

            if (curr == quoteEnd && state.Peek() == quoteEnd)
            {
                chars.Append(curr);
                chars.Append(curr);
                state.Next();
            }
            else if (curr == quoteEnd)
            {
                lastChar = quoteEnd;
                break;
            }
            else
            {
                chars.Append(curr);
            }
        } while (curr != CharSymbols.EndOfFile);

        if (lastChar != quoteEnd)
        {
            throw new LexException.UnterminatedQuotedIdentifier(quoteEnd, initialLocation);
        }

        string word = chars.ToString();
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
        string word = new(ExtractWordTrustNoNewLines(ref state));

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
        return new Colon() { Location = initialLocation };
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

    private static Token TokenizeNumber(ref LexerState state)
    {
        Location initialLocation = state.Location;

        if (state.Peek() == CharSymbols.Zero && state.PeekNth(1) is 'x' or 'X')
        {
            state.Next(); // Consume the '0'
            state.Next(); // Consume the 'x' or 'X'
            ReadOnlySpan<char> hex = state.TakeWhile(char.IsAsciiHexDigit);
            return new HexStringLiteral(new string(hex)) { Location = initialLocation };
        }

        int startPosition = state.Position;

        state.TakeWhile(char.IsDigit);

        if (state.Peek() == CharSymbols.Dot)
        {
            state.Next();
        }

        state.TakeWhile(char.IsDigit);

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

        state.TakeWhile(char.IsDigit);
    }

    private StringLiteral TokenizeStringLiteral(ref LexerState state)
    {
        Location initialLocation = state.Location;

        string contents = ExtractString(ref state);

        return new StringLiteral(contents) { Location = initialLocation };
    }

    private Token TokenizeHex(ref LexerState state)
    {
        Location initialLocation = state.Location;
        state.Next();

        if (state.Peek() != CharSymbols.SingleQuote)
        {
            return new Word(new string(ExtractWordTrustNoNewLines(ref state))) { Location = state.Location };
        }

        string hex = ExtractString(ref state, backslashEscape: true);
        return new HexStringLiteral(hex) { Location = initialLocation };
    }

    private Token TokenizeNationalStringLiteral(ref LexerState state)
    {
        Location initialLocation = state.Location;

        state.Next();
        return state.Peek() switch
        {
            CharSymbols.SingleQuote => new NationalStringLiteral(new string(ExtractString(ref state))) { Location = initialLocation },
            _ => new Word(new string(ExtractWordTrustNoNewLines(ref state))) { Location = initialLocation }
        };
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
        return new NonSql(NonSqlType.InlineComment, comment) { Prefix = "--", Location = startLocation };
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
                        // state.Next();
                        return new NonSql(NonSqlType.BlockComment, new string(state.SpanFrom(startPosition, state.Position - 2))) { Location = startLocation };
                    }
                    break;
            }
            lastChar = curr;
        }
    }

    private static string ExtractLineComment(ref LexerState state)
    {
        int startPosition = state.Position;
        state.TakeWhile(c => c is not CharSymbols.NewLine and not CharSymbols.EndOfFile);

        bool isEof = state.Next() == CharSymbols.EndOfFile;

        string content = new(state.SpanFrom(startPosition));
        if (isEof)
        {
            return content + CharSymbols.EndOfFile;
        }
        return content;
    }

    private string ExtractString(ref LexerState state, char quoteChar = CharSymbols.SingleQuote, bool backslashEscape = true)
    {
        Location startLocation = state.Location;

        state.Next(); // Consume the opening quotation mark.

        // Try allocation-free search first.
        int startPosition = state.Position;
        while (true)
        {
            char curr = state.Peek();

            if (curr == CharSymbols.EndOfFile)
            {
                throw new LexException.UnterminatedString(startLocation);
            }

            if (curr == quoteChar)
            {
                if (state.PeekNth(1) == quoteChar)
                {
                    // Escaped quote. Have to do it the slower way.
                    break;
                }

                // String was closed without any escaping. Return it.
                string result = new(state.SpanFrom(startPosition));
                state.Next(); // Consume the closing quotation mark.
                return result;
            }

            if (backslashEscape && curr == CharSymbols.Backslash)
            {
                // String contains backslash escapes. Have to do it the slower way.
                break;
            }

            state.Next();
        }

        // Escaped strings---have to go the slower route.
        int estimatedCapacity = Math.Max(16, state.Position - startPosition + 32);
        char[]? rentedArray = null;
        char[] buffer;

        Span<char> stackBuffer = stackalloc char[StackAllocationThreshold];

        if (estimatedCapacity <= StackAllocationThreshold)
        {
            buffer = null!;
            state.SpanFrom(startPosition).CopyTo(stackBuffer);
        }
        else
        {
            rentedArray = ArrayPool<char>.Shared.Rent(estimatedCapacity);
            buffer = rentedArray;
            state.SpanFrom(startPosition).CopyTo(buffer);
        }

        int length = state.Position - startPosition;

        void Append(char c, Span<char> stackBuffer)
        {
            if (rentedArray == null)
            {
                if (length < StackAllocationThreshold)
                {
                    stackBuffer[length++] = c;
                    return;
                }

                // Overflow to heap.
                rentedArray = ArrayPool<char>.Shared.Rent(Math.Max(estimatedCapacity * 2, length + 64));
                buffer = rentedArray;
                stackBuffer[..length].CopyTo(buffer);
            }
            else if (length >= rentedArray.Length)
            {
                // Get a bigger buffer.
                char[] expanded = ArrayPool<char>.Shared.Rent(rentedArray.Length * 2);
                rentedArray.AsSpan(0, length).CopyTo(expanded);
                ArrayPool<char>.Shared.Return(rentedArray);
                rentedArray = expanded;
                buffer = rentedArray;
            }

            buffer[length++] = c;
        }

        try
        {
            bool escapeNext = false;

            while (true)
            {
                char curr = state.Peek();

                if (curr == CharSymbols.EndOfFile)
                {
                    throw new LexException.UnterminatedString(startLocation);
                }

                if (curr == quoteChar)
                {
                    if (escapeNext)
                    {
                        Append(curr, stackBuffer);
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
                        int pairedQuotes = numberQuotes / 2 * 2;

                        for (int i = 0; i < pairedQuotes; i++)
                        {
                            Append(quoteChar, stackBuffer);
                        }

                        if (numberQuotes - pairedQuotes == 1)
                        {
                            // Closing quote found. Return the string.
                            return BuildResult(stackBuffer);
                        }
                    }
                }
                else if (backslashEscape && curr == CharSymbols.Backslash)
                {
                    Append(curr, stackBuffer);
                    state.Next();
                    escapeNext = true;
                }
                else
                {
                    Append(curr, stackBuffer);
                    state.Next();
                    escapeNext = false;
                }
            }
        }
        catch
        {
            if (rentedArray != null)
            {
                ArrayPool<char>.Shared.Return(rentedArray);
            }
            throw;
        }

        string BuildResult(Span<char> stackBuffer)
        {
            if (rentedArray == null)
            {
                return new string(stackBuffer[..length]);
            }
            string s = new(buffer.AsSpan(0, length));
            ArrayPool<char>.Shared.Return(rentedArray);
            return s;
        }
    }

    private string ExtractWordTrustNoNewLines(ref LexerState state)
    {
        int start = state.Position - 1;
        state.TakeWhileTrustNoNewLines(IsIdentifierPart);
        return new string(state.SpanFrom(start, state.Position));
    }

    private static bool IsIdentifierStart(char character)
    {
        return char.IsLetter(character)
            || character is CharSymbols.Underscore or CharSymbols.Num or CharSymbols.At;
    }

    protected bool IsIdentifierPart(char character)
    {
        return char.IsLetter(character) || char.IsDigit(character) || character is CharSymbols.Underscore;
    }
}
