using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Slash() : SymbolToken(StringSymbols.Slash), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Slash;
}

