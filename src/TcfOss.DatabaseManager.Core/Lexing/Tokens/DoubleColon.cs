using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class DoubleColon() : SymbolToken(StringSymbols.DoubleColon), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.DoubleColon;
}
