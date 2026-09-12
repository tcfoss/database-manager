using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Equal() : SymbolToken(StringSymbols.Equal), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Equal;
}

