using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class GreaterThanOrEqual() : SymbolToken(StringSymbols.GreaterThanOrEqual), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.GreaterThanOrEqual;
}

