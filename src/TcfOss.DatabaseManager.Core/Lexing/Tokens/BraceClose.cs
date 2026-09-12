using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class BraceClose() : SymbolToken(StringSymbols.CurlyBraceClose), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.CurlyBraceClose;
};
