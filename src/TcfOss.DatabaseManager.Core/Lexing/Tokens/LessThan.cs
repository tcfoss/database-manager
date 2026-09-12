using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class LessThan() : SymbolToken(StringSymbols.LessThan), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.LessThan;
}

