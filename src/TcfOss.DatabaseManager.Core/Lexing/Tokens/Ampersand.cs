using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Ampersand() : SymbolToken(StringSymbols.Ampersand), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Ampersand;
}
