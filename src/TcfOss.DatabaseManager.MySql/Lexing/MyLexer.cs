using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.MySql.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MySql.Lexing;

public class MyLexer : Lexer<MyRunState>
{
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
                List<NonSql>? nonSql = runState.MaybeCharset.PreNonSql;
                if (token.PreNonSql != null)
                {
                    nonSql ??= [];
                    nonSql.AddRange(token.PreNonSql);
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

        if (token is Word w && w.Value.StartsWith('_'))
        {
            runState.MaybeCharset = w;
            // Don't add the token yet, until we know what comes next
            return;
        }

        tokens.Add(token);
    }

    protected override Token NextToken(ref LexerState state, ref MyRunState runState)
    {
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
