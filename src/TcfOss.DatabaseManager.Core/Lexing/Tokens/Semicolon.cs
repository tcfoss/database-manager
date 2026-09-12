using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Semicolon() : SymbolToken(StringSymbols.Semicolon), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Semicolon;
}

