using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class At() : SymbolToken(StringSymbols.At), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.At;
}
