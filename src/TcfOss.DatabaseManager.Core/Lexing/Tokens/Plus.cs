using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Plus() : SymbolToken(StringSymbols.Plus), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Plus;
}

