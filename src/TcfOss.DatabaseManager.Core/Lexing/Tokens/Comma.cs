using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Comma() : SymbolToken(StringSymbols.Comma), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Comma;
}

