using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class TildeAsterisk() : SymbolToken(StringSymbols.TildeAsterisk), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.TildeAsterisk;
}
