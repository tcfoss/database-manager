using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Colon() : SymbolToken(StringSymbols.Colon), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Colon;
}

