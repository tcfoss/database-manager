using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Walrus() : SymbolToken(StringSymbols.Assignment), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Assignment;
}

