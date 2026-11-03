using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class NotTildeAsterisk() : SymbolToken(StringSymbols.NotTildeAsterisk), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.NotTildeAsterisk;
}
