using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Pipe() : SymbolToken(StringSymbols.Pipe), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Pipe;
}
