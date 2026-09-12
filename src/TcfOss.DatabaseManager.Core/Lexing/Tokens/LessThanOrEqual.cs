using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class LessThanOrEqual() : SymbolToken(StringSymbols.LessThanOrEqual), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.LessThanOrEqual;
}

