using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MySql.Lexing;

public struct MyRunState
{
    public string? TerminatorString { get; set; }
    public char? TerminatorStringStart { get; set; }
    public Word? MaybeCharset { get; set; }
}
