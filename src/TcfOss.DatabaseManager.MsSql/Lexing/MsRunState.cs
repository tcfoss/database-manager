namespace TcfOss.DatabaseManager.MsSql.Lexing;

public struct MsRunState
{
    /// <summary>
    /// True when the lexer position is at the start of a logical line — i.e.,
    /// the start of input, or no non-newline tokens have been emitted since
    /// the last newline. Used to detect T-SQL GO batch separators.
    /// </summary>
    public bool AtLineStart { get; set; } = true;

    public MsRunState()
    {
    }
}
