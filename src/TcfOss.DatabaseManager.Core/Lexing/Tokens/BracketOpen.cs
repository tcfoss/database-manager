using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class BracketOpen() : SymbolToken(StringSymbols.SquareBracketOpen), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.SquareBracketOpen;
}
