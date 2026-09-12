namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class NumericLiteral(string value, bool isLong = false) : Token
{
    public string Value { get; } = value;
    public bool IsLong { get; } = isLong;

    public override string ToString()
    {
        char? longIndicator = IsLong ? 'L' : null;
        return $"{Value}{longIndicator}";
    }

    private bool Equals(NumericLiteral? other)
    {
        return other != null && Value == other.Value && IsLong == other.IsLong;
    }

    public override bool Equals(object? obj) => Equals(obj as NumericLiteral);

    public override bool Equals(Token? other) => Equals(other as NumericLiteral);

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, IsLong);
    }
}
