using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class FatArrow() : SymbolToken(StringSymbols.FatArrow), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.FatArrow;
}

