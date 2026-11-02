using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Tilde() : SymbolToken(StringSymbols.Tilde), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Tilde;
}
