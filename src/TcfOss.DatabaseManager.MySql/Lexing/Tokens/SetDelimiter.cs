using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.MySql.Lexing.Tokens;

public class SetDelimiter(string delimiter) : Token
{
    public string Delimiter { get; } = delimiter;

    public override int Length => Delimiter.Length;

    private bool Equals(SetDelimiter? other)
    {
        return other != null && Delimiter == other.Delimiter;
    }

    public override bool Equals(Token? other) => Equals(other as SetDelimiter);
    public override bool Equals(object? obj) => Equals(obj as SetDelimiter);

    public override int GetHashCode() => Delimiter.GetHashCode();
}
