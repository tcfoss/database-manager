using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class GreaterThan() : SymbolToken(StringSymbols.GreaterThan), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.GreaterThan;
}

