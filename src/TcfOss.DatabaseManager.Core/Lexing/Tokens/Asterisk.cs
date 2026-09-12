using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Asterisk() : SymbolToken(StringSymbols.Asterisk), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.Asterisk;
}
