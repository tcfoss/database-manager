using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Lexing;

// ReSharper disable UnusedMemberInSuper.Global
public interface ILexer
{
    List<Token> Tokenize(ReadOnlySpan<char> sql);
    List<Token> Tokenize(string sql);
}
