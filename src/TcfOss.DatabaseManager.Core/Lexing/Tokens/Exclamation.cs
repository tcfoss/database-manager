using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Exclamation() : SymbolToken(StringSymbols.ExclamationMark), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.ExclamationMark;
}

