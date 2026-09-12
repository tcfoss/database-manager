using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Minus() : SymbolToken(StringSymbols.Minus), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Minus;
}

