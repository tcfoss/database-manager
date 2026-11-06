using System.Buffers;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.MsSql.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MsSql.Lexing;

public class MsLexer : Lexer<MsRunState>
{
    private static readonly SearchValues<char> s_identifierCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_@$#");

    protected override bool SupportBackslashQuoteEscape => false;
    protected override bool SupportDollarQuotedStrings => false;
    protected override bool SupportEscapeStringLiterals => false;
    protected override SearchValues<char> IdentifierAsciiCharacters => s_identifierCharacters;

    protected override bool IsIdentifierPart(char character)
    {
        return base.IsIdentifierPart(character) || character is CharSymbols.At or CharSymbols.Dollar or CharSymbols.Num;
    }

    protected override Token NextToken(ref LexerState state, ref MsRunState runState)
    {
        char character = state.Peek();

        // Recognize T-SQL GO batch separator when it appears as the first
        // non-whitespace word on a line.
        if (character is 'G' or 'g' && runState.AtLineStart)
        {
            Token? batch = TryTokenizeBatchSeparator(ref state);
            if (batch != null)
            {
                runState.AtLineStart = false;
                return batch;
            }
        }

        Token token = base.NextToken(ref state, ref runState);

        if (token is NonSql ns)
        {
            if (ns.Value is Core.Statements.Components.NonSql.Newlines)
            {
                runState.AtLineStart = true;
            }
        }
        else if (token is not Eof)
        {
            runState.AtLineStart = false;
        }

        return token;
    }

    private MsBatchSeparator? TryTokenizeBatchSeparator(ref LexerState state)
    {
        // Need exactly the two characters "GO" not followed by an identifier character.
        char second = state.PeekNth(1);
        if (second != 'O' && second != 'o')
        {
            return null;
        }
        char third = state.PeekNth(2);
        if (IsIdentifierPart(third))
        {
            return null;
        }

        Location initialLocation = state.Location;
        state.Next(); // G
        state.Next(); // O

        // Skip horizontal whitespace, then optionally consume a positive integer batch count.
        LexerState afterGo = state.Clone();
        while (afterGo.Peek() is CharSymbols.Space or CharSymbols.Tab)
        {
            afterGo.Next();
        }

        int count = 1;
        if (char.IsDigit(afterGo.Peek()))
        {
            ReadOnlySpan<char> digits = afterGo.TakeWhileDigit();
            if (int.TryParse(new string(digits), out int parsed) && parsed > 0)
            {
                count = parsed;
                state = afterGo;
            }
        }

        return new MsBatchSeparator(count) { Location = initialLocation };
    }
}
