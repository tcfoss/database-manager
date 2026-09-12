using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class ParenClose() : SymbolToken(StringSymbols.ParenClose), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.ParenClose;
}

