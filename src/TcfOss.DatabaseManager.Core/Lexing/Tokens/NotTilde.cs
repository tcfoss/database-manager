using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class NotTilde() : SymbolToken(StringSymbols.NotTilde), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.NotTilde;
}
