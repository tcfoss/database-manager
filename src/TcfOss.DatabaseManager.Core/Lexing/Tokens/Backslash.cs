using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Backslash() : SymbolToken(StringSymbols.Backslash), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Backslash;
}
