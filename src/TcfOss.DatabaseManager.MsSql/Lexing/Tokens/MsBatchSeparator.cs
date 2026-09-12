using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MsSql.Lexing.Tokens;

/// <summary>
/// T-SQL <c>GO</c> batch separator. Recognized by the lexer when <c>GO</c>
/// (case-insensitive) appears as the only non-whitespace content on a line,
/// optionally followed by a positive integer batch count.
/// </summary>
public class MsBatchSeparator(int count = 1) : Token
{
    public int Count { get; } = count;
    public override int Length => 2;

    private bool Equals(MsBatchSeparator? other)
    {
        return other != null && Count == other.Count;
    }

    public override bool Equals(object? obj) => Equals(obj as MsBatchSeparator);

    public override bool Equals(Token? other) => Equals(other as MsBatchSeparator);

    public override int GetHashCode() => HashCode.Combine(nameof(MsBatchSeparator), Count);
}
