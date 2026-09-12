using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class Eof() : SymbolToken(StringSymbols.EndOfFile), ITypeSymbol
{
    public static string TypeSymbol => StringSymbols.EndOfFile;
}

