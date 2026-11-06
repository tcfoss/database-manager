using System.Buffers;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.MySql.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MySql.Lexing;

public class MyLexer : Lexer<MyRunState>
{
    protected override bool SupportDollarQuotedStrings => false;
    protected override bool SupportEscapeStringLiterals => false;
    private static readonly SearchValues<char> s_identifierCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_$");

    protected override SearchValues<char> IdentifierAsciiCharacters => s_identifierCharacters;

    protected override bool IsIdentifierStart(char character)
    {
        return base.IsIdentifierStart(character) || character == CharSymbols.Dollar
            || character >= '\u0080' && !char.IsSurrogate(character);
    }

    protected override bool IsIdentifierPart(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character is CharSymbols.Underscore or CharSymbols.Dollar
            || character >= '\u0080' && !char.IsSurrogate(character);
    }

    private static bool IsSymbolCharacter(char c)
    {
        return c is ';' or '!' or '@' or '#' or '/' or '$' or '%' or '&' or '*';
    }

    protected override void PostProcessToken(Token token, List<Token> tokens, ref MyRunState runState)
    {
        if (runState.MaybeCharset != null)
        {
            if (token is StringLiteral sl)
            {
                NonSqlList? nonSql = runState.MaybeCharset.PreNonSql;
                if (token.PreNonSql != null)
                {
                    if (nonSql == null)
                    {
                        nonSql = token.PreNonSql;
                    }
                    else
                    {
                        nonSql.Append(token.PreNonSql);
                    }
                }

                token = new StringLiteral(sl.Value)
                {
                    CharSet = runState.MaybeCharset.Value,
                    Location = runState.MaybeCharset.Location,
                    PreNonSql = nonSql
                };
            }
            else
            {
                // It was not a charset. Add the underscore-identifier to the list.
                tokens.Add(runState.MaybeCharset);
            }
            runState.MaybeCharset = null;
        }

        if (token is SetDelimiter sd)
        {
            runState.TerminatorString = sd.Delimiter == ";" ? null : sd.Delimiter;
            runState.TerminatorStringStart = runState.TerminatorString?[0];
        }

        if (token is Word { QuoteStyle: QuoteStyle.None } w && w.Value.StartsWith('_'))
        {
            runState.MaybeCharset = w;
            // Don't add the token yet, until we know what comes next
            return;
        }

        tokens.Add(token);
    }

    protected override void PostProcessStream(List<Token> tokens, ref MyRunState runState)
    {
        if (runState.MaybeCharset != null)
        {
            // It was not a charset.
            tokens.Add(runState.MaybeCharset);
            runState.MaybeCharset = null;
        }
    }

    protected override Token NextToken(ref LexerState state, ref MyRunState runState)
    {
        state.IdentifierTerminator = runState.TerminatorString;
        char character = state.Peek();
        if (character == runState.TerminatorStringStart)
        {
            Token? terminatorToken = TokenizeTerminatorString(ref state, runState.TerminatorString!);
            if (terminatorToken != null)
            {
                return terminatorToken;
            }
        }

        if (character is 'd' or 'D')
        {
            SetDelimiter? delimiterToken = TokenizeMaybeDelimiter(ref state);
            if (delimiterToken != null)
            {
                return delimiterToken;
            }
        }

        return base.NextToken(ref state, ref runState);
    }

    private static MyTerminator? TokenizeTerminatorString(ref LexerState state, string terminatorString)
    {
        Location initialLocation = state.Location;
        bool isTerminator = state.TakeIfSequence(terminatorString);
        if (isTerminator)
        {
            return new MyTerminator(terminatorString) { Location = initialLocation };
        }
        return null;
    }

    private static SetDelimiter? TokenizeMaybeDelimiter(ref LexerState state)
    {
        Location initialLocation = state.Location;

        bool isSetDelimiter = state.TakeIfSequenceCaseInsensitive("DELIMITER");
        if (!isSetDelimiter)
        {
            return null;
        }

        while (state.Peek() is CharSymbols.Space or CharSymbols.Tab)
        {
            _ = state.Next();
        }

        ReadOnlySpan<char> symbols = state.TakeWhile(IsSymbolCharacter);
        if (symbols.Length == 0)
        {
            throw new LexException.ExpectedSymbolSequence(initialLocation);
        }

        return new SetDelimiter(new string(symbols)) { Location = initialLocation };
    }
}
