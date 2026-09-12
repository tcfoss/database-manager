using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Dot() : SymbolToken(StringSymbols.Dot), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Dot;
}

