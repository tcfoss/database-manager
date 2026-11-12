using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DataStructures.Enums;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ParserState
{
    private readonly Token[] _tokens;
    private readonly int _tokenCount;
    private readonly Eof _finalEof;
    private int _position;
    private int? _savedPosition;

    public int SourceId { get; }

    public ParseContext Context { get; private set; }
    public StatementLabel? StatementLabel { get; private set; }

    public ParserState(Token[] tokens, int sourceId = 0)
    {
        _tokens = tokens;
        _tokenCount = tokens.Length;
        SourceId = sourceId;
        if (tokens.Length == 0)
        {
            _finalEof = new Eof() { Location = new Location() { Column = 0, Line = 0, Position = 0 } };
        }
        else if (tokens[^1] is NonSqlContainer nsc)
        {
            _tokenCount = tokens.Length - 1;
            _finalEof = new Eof() { Location = nsc.Location, PreNonSql = nsc.SubTokens };
        }
        else if (tokens[^1] is Semicolon or Eof)
        {
            _finalEof = new Eof() { Location = _tokens[^1].Location };
        }
        else
        {
            Token lastTok = _tokens[^1];
            Location lastLoc = lastTok.Location;
            _finalEof = new Eof() { Location = new Location() { Column = lastLoc.Column + lastTok.Length, Line = lastLoc.Line, Position = lastLoc.Position + lastTok.Length } };
        }
    }

    public Token PeekNth(int n)
    {
        int target = _position + n;
        if ((uint)target >= (uint)_tokenCount)
        {
            return _finalEof;
        }
        return _tokens[target];
    }

    /// <summary>
    /// Return the next non-whitespace, non-comment token without advancing the pointer.
    /// </summary>
    /// <returns></returns>
    public Token Peek()
    {
        if ((uint)_position >= (uint)_tokenCount)
        {
            return _finalEof;
        }
        return _tokens[_position];
    }

    public ReadOnlySpan<Token> PeekN(int n)
    {
        return _tokens.AsSpan(_position, Math.Min(n, _tokenCount - _position));
    }

    public bool PeekIs<T>() where T : Token
    {
        return Peek() is T;
    }

    public bool PeekNthIs<T>(int n) where T : Token
    {
        return PeekNth(n) is T;
    }

    public bool PeekKeyword(Keyword keyword)
    {
        return (Peek() as Word)?.Keyword == keyword;
    }

    public bool PeekKeywordsAll(params ReadOnlySpan<Keyword> keywords)
    {
        ReadOnlySpan<Token> foundKeywords = PeekN(keywords.Length);
        if (keywords.Length != foundKeywords.Length)
        {
            return false;
        }

        for (int i = 0; i < keywords.Length; i++)
        {
            if (keywords[i] != (foundKeywords[i] as Word)?.Keyword)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Return the next non-whitespace, non-comment token and advance the pointer.
    /// </summary>
    /// <returns></returns>
    public Token Next()
    {
        if ((uint)_position >= (uint)_tokenCount)
        {
            return _finalEof;
        }
        return _tokens[_position++];
    }

    /// <summary>
    /// Move the pointer back to the last non-whitespace, non-comment token.
    /// </summary>
    public void Rewind()
    {
        if (_position > 0)
        {
            _position--;
        }
    }

    public void SavePosition()
    {
        _savedPosition = _position;
    }

    public void RewindToSaved()
    {
        if (_savedPosition.HasValue)
        {
            _position = _savedPosition.Value;
            _savedPosition = null;
        }
    }

    public void ClearSavedPosition()
    {
        _savedPosition = null;
    }

    /// <summary>
    /// Return whether the next token is the requested keyword and, if so,
    /// consume it.
    /// </summary>
    /// <param name="expected"></param>
    /// <returns></returns>
    public bool ParseKeyword(Keyword expected)
    {
        Token token = Peek();

        if (token is not Word word || word.Keyword != expected)
        {
            return false;
        }

        Next();
        return true;
    }

    /// <summary>
    /// Return whether the upcoming sequence of tokens matches the requested
    /// keywords and, if so, consume them.
    /// </summary>
    /// <param name="keywords"></param>
    /// <returns></returns>
    public bool ParseKeywordsAll(params ReadOnlySpan<Keyword> keywords)
    {
        int position = _position;

        foreach (Keyword kw in keywords)
        {
            if (!ParseKeyword(kw))
            {
                _position = position;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// If the next token is one of the requested keywords, return and
    /// consume it. Otherwise, return `Keyword.undefined`.
    /// </summary>
    /// <param name="keywords"></param>
    /// <returns></returns>
    public Keyword ParseKeywordsAny(params ReadOnlySpan<Keyword> keywords)
    {
        if (Peek() is not Word token)
        {
            return Keyword.undefined;
        }

        foreach (Keyword kw in keywords)
        {
            if (kw == token.Keyword)
            {
                Next();
                return kw;
            }
        }

        return Keyword.undefined;
    }

    public bool TryGetPseudoKeyword(out string text, out Keyword keyword, bool consume = false)
    {
        if (Peek() is Word { QuoteStyle: QuoteStyle.None, Sigil: SigilKind.None } token && PeekNth(1) is not Dot)
        {
            text = token.Value;
            keyword = token.Keyword;
            if (consume)
            {
                Next();
            }
            return true;
        }

        text = string.Empty;
        keyword = Keyword.undefined;
        return false;
    }

    /// <summary>
    /// Advance through upcoming tokens and check whether they match the
    /// requested keyword criteria. If they do, return the detected keywords.
    /// Otherwise, return an empty `Sequence`.
    /// </summary>
    /// <param name="keywords"></param>
    /// <returns></returns>
    public List<Keyword> ParseKeywordSequence(params ReadOnlySpan<KeywordSearchCondition> keywords)
    {
        List<Keyword>? keywordsFound = null;
        int position = _position;
        foreach (KeywordSearchCondition keywordCondition in keywords)
        {
            switch (keywordCondition)
            {
                case KeywordSearchCondition.RequiredKeyword required:
                {
                    Keyword keyword = required.Keyword;
                    bool found = ParseKeyword(keyword);
                    if (found)
                    {
                        (keywordsFound ??= []).Add(keyword);
                    }
                    else
                    {
                        _position = position;
                        keywordsFound?.Clear();
                        return keywordsFound ?? [];
                    }

                    break;
                }
                case KeywordSearchCondition.OneOfKeywords oneOf:
                {
                    Keyword[] currKeywords = oneOf.Keywords;
                    Keyword result = ParseKeywordsAny(currKeywords);
                    if (result != Keyword.undefined)
                    {
                        (keywordsFound ??= []).Add(result);
                    }
                    else
                    {
                        _position = position;
                        keywordsFound?.Clear();
                        return keywordsFound ?? [];
                    }

                    break;
                }
                case KeywordSearchCondition.OptionalKeyword optional:
                {
                    Keyword currKeyword = optional.Keyword;
                    bool found = ParseKeyword(currKeyword);
                    if (found)
                    {
                        (keywordsFound ??= []).Add(currKeyword);
                    }

                    break;
                }
                case KeywordSearchCondition.OptionalKeywordSequence seq:
                {
                    Keyword[] currKeywords = seq.Keywords;
                    bool found = ParseKeywordsAll(currKeywords);
                    if (found && currKeywords.Length > 0)
                    {
                        (keywordsFound ??= []).AddRange(currKeywords);
                    }

                    break;
                }
            }
        }

        return keywordsFound ?? [];
    }

    public bool ConsumeTokenIs<T>() where T : Token
    {
        if (!PeekIs<T>())
        {
            return false;
        }
        Next();
        return true;
    }

    public bool ConsumeTokenIsIf<T>(Func<T, bool> predicate) where T : Token
    {
        Token peeked = Peek();
        if (peeked is not T token || !predicate(token))
        {
            return false;
        }
        Next();
        return true;
    }

    public void ExpectToken<T>() where T : SymbolToken, ITypeSymbol
    {
        if (!ConsumeTokenIs<T>())
        {
            string symbol = T.TypeSymbol;
            throw ExpectedException(symbol);
        }
    }

    public void ExpectLeftParen()
    {
        ExpectToken<ParenOpen>();
    }

    public void ExpectRightParen()
    {
        ExpectToken<ParenClose>();
    }

    /// <summary>
    /// If the upcoming token is the requested keyword, advance the pointer. Otherwise,
    /// throw an exception.
    /// </summary>
    /// <param name="keyword"></param>
    /// <param name="advance">If false, do not actually advance.</param>
    public void ExpectKeyword(Keyword keyword, bool advance = true)
    {
        if (!ParseKeyword(keyword))
        {
            throw ExpectedException($"{keyword}");
        }
        if (!advance)
        {
            Rewind();
        }
    }

    public void ExpectKeywordsAll(params ReadOnlySpan<Keyword> keywords)
    {
        foreach (Keyword keyword in keywords)
        {
            ExpectKeyword(keyword);
        }
    }

    public bool TryParse<T>(Func<ParserState, T?> parse, [NotNullWhen(true)] out T? result) where T : class
    {
        int position = _position;

        try
        {
            result = parse(this);

            if (result != null)
            {
                return true;
            }

            _position = position;
            result = null;
        }
        catch (ParseException)
        {
            _position = position;
            result = null;
        }
        return result != null;
    }

    public T ParseStringEnum<T>() where T : class, IStringEnum<T>
    {
        T? result = MaybeParseStringEnum<T>() ?? throw ExpectedException<T>();
        return result;
    }

    public T? MaybeParseStringEnum<T>() where T : class, IStringEnum<T>
    {
        if (Peek() is Word { QuoteStyle: QuoteStyle.None, Sigil: SigilKind.None } word
            && T.TryParse(word.Value, out T? parsed))
        {
            Next();
            return parsed;
        }
        return null;
    }

    /// <summary>
    /// If the condition is true, return the result of the `initializer` function. Otherwise
    /// return `valueOnFailure`.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="condition"></param>
    /// <param name="initialize"></param>
    /// <param name="valueOnFailure"></param>
    /// <returns></returns>
    public T? ParseInit<T>(bool condition, Func<ParserState, T> initialize, T? valueOnFailure = default)
    {
        if (condition)
        {
            return initialize(this);
        }
        return valueOnFailure;
    }

    public SqlValueList<T> ParseCommaSeparated<T>(Func<ParserState, T> parseComponent)
        where T : IEquatable<T>
    {
        var components = new SqlValueList<T>();

        do
        {
            components.Add(parseComponent(this));
        } while (!IsCommaSeparatedEnd());

        return components;
    }

    private bool IsCommaSeparatedEnd()
    {
        if (!ConsumeTokenIs<Comma>())
        {
            return true;
        }
        return false;
    }

    public T ParseParenthesized<T>(Func<ParserState, T> innerParser)
    {
        ExpectLeftParen();
        T result = innerParser(this);
        ExpectRightParen();
        return result;
    }

    public SqlValueList<T> ParseParenthesizedCommaSeparated<T>(Func<ParserState, T> parseItem, bool allowEmpty)
        where T : IEquatable<T>
    {
        if (ConsumeTokenIs<ParenOpen>())
        {
            if (PeekIs<ParenClose>())
            {
                if (allowEmpty)
                {
                    Next();
                    return [];
                }
                throw ExpectedException("comma_separated_list".Italic());
            }

            SqlValueList<T> components = ParseCommaSeparated(parseItem);
            ExpectRightParen();
            return components;
        }

        throw ExpectedException("parenthesized_list".Italic());
    }

    public SqlValueList<T>? ParseParenthesizedCommaSeparatedOptional<T>(Func<ParserState, T> parseItem, bool allowEmpty)
        where T : IEquatable<T>
    {
        if (ConsumeTokenIs<ParenOpen>())
        {
            if (PeekIs<ParenClose>())
            {
                if (allowEmpty)
                {
                    Next();
                    return [];
                }
                throw ExpectedException("comma_separated_list".Italic());
            }

            SqlValueList<T> components = ParseCommaSeparated(parseItem);
            ExpectRightParen();
            return components;
        }

        return null;
    }

    public Scope EnterContext(ParseContext enable = ParseContext.None, ParseContext disable = ParseContext.None)
    {
        return new Scope(this, enable, disable);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly ParserState _state;
        private readonly ParseContext _previousContext;

        internal Scope(ParserState state, ParseContext enable, ParseContext disable)
        {
            _state = state;
            _previousContext = state.Context;
            state.Context = (state.Context | enable) & ~disable;
        }

        public void Dispose()
        {
            _state.Context = _previousContext;
        }
    }

    public StatementScope EnterStatementScope(Word? labelToken)
    {
        StatementLabel? statementLabel = labelToken is not null ? new StatementLabel(labelToken, SourceId) : null;
        return new StatementScope(this, statementLabel);
    }

    public readonly struct StatementScope : IDisposable
    {
        private readonly ParserState _state;
        private readonly StatementLabel? _previousStatementLabel;

        internal StatementScope(ParserState state, StatementLabel? statementLabel)
        {
            _state = state;
            _previousStatementLabel = state.StatementLabel;
            state.StatementLabel = statementLabel;
        }

        public void Dispose()
        {
            _state.StatementLabel = _previousStatementLabel;
        }
    }

    public ParseException ExpectedException(string expected)
    {
        Token found = Peek();
        return ExpectedException(expected, found);
    }

    public ParseException ExpectedCategoryException(string expectedCategory)
    {
        Token found = Peek();
        return ExpectedCategoryException(expectedCategory, found);
    }

    public ParseException ExpectedException<T>(Token? found = null)
        where T : IStringEnum<T>
    {
        found ??= Peek();
        return ParseException.ExpectedOneOfButFound.FromEnumLike<T>($"{found}", found.Location);
    }

    public ParseException ExpectedException(params string[] expected)
    {
        Token found = Peek();
        string foundString;
        if (found is Word w)
        {
            foundString = w.Value;
        }
        else
        {
            foundString = $"'{found}'";
        }

        return new ParseException.ExpectedOneOfButFound(expected, foundString, found.Location);
    }

    public static ParseException ExpectedException(string expected, Token found)
    {
        if (found is Eof)
        {
            return new ParseException.ExpectedButFound(expected, "end of input".Italic(), found.Location);
        }
        return new ParseException.ExpectedButFound(expected, $"{found}", found.Location);
    }

    public static ParseException ExpectedCategoryException(string expectedCategory, Token found)
    {
        if (found is Eof)
        {
            return new ParseException.ExpectedButFound(expectedCategory.Italic(), "end of input".Italic(), found.Location);
        }
        return new ParseException.ExpectedButFound(expectedCategory.Italic(), $"{found}", found.Location);
    }
}
