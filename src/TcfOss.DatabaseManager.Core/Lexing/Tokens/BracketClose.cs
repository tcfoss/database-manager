using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class BracketClose() : SymbolToken(StringSymbols.SquareBracketClose), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.SquareBracketClose;
}
