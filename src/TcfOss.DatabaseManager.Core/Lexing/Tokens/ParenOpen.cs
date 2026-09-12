using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class ParenOpen() : SymbolToken(StringSymbols.ParenOpen), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.ParenOpen;
}

