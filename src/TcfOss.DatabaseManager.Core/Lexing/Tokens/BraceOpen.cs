using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class BraceOpen() : SymbolToken(StringSymbols.CurlyBraceOpen), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.CurlyBraceOpen;
}
