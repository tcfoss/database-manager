namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class StringLiteral(string value) : Token
{
    public string Value { get; } = value;

    public string? CharSet { get; init; }

    public override int Length => Value.Length + 2 + (CharSet?.Length ?? 0);

    public override string ToString()
    {
        return $"{CharSet}'{Value}'";
    }

    private bool Equals(StringLiteral? other)
    {
        return other != null && Value == other.Value && CharSet == other.CharSet;
    }

    public override bool Equals(object? obj) => Equals(obj as StringLiteral);

    public override bool Equals(Token? other) => Equals(other as StringLiteral);

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
