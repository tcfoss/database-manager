using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class DoublePipe() : SymbolToken(StringSymbols.DoublePipe), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.DoublePipe;
}
