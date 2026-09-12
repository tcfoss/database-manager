using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class NotEqual() : SymbolToken(StringSymbols.NotEqual), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.NotEqual;
}

