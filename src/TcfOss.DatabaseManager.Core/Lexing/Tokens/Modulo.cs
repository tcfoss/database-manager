using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Modulo() : SymbolToken(StringSymbols.Percent), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Percent;
}

