namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class DotDot() : SymbolToken(".."), ITypeSymbol
{
    public static string TypeSymbol => "..";
}
