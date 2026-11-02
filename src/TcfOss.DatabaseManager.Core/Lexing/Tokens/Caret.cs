using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Caret() : SymbolToken(StringSymbols.Caret), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Caret;
}
