using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing;

namespace TcfOss.DatabaseManager.Core.Extensions;

public static class StringExtensions
{
    public static string? EscapeQuotedString(this string? value, char quote)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        char prev = char.MinValue;
        char curr;

        var state = new LexerState(value);
        List<char> word = [];


        while ((curr = state.Peek()) != CharSymbols.EndOfFile)
        {
            if (curr == quote)
            {
                if (prev == CharSymbols.Backslash)
                {
                    word.Add(curr);
                    state.Next();
                    continue;
                }

                state.Next();
                word.Add(curr);
                word.Add(curr);

                if (state.Peek() == quote)
                {
                    state.Next();
                }
            }
            else
            {
                word.Add(curr);
                state.Next();
            }
            prev = curr;
        }

        return new string([.. word]);
    }

    public static string? EscapeSingleQuotedString(this string? value)
    {
        return EscapeQuotedString(value, CharSymbols.SingleQuote);
    }
}
