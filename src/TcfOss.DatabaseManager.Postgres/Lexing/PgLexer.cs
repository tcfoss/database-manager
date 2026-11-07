using System.Buffers;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing;

namespace TcfOss.DatabaseManager.Postgres.Lexing;

public class PgLexer : Lexer<RunState>
{
    private static readonly SearchValues<char> s_identifierCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_$");

    protected override bool SupportBackslashQuoteEscape => false;
    protected override bool SupportBracketQuotedIdentifiers => false;
    protected override bool SupportDollarQuotedStrings => true;
    protected override bool HexStringsAreBitStrings => true;
    protected override SearchValues<char> IdentifierAsciiCharacters => s_identifierCharacters;

    protected override bool IsIdentifierPart(char character)
    {
        return base.IsIdentifierPart(character) || character == CharSymbols.Dollar;
    }
}
