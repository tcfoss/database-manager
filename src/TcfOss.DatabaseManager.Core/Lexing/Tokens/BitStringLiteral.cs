namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class BitStringLiteral(string value, bool isHexadecimal = false) : Token
{
    public string Value { get; } = value;
    public bool IsHexadecimal { get; } = isHexadecimal;

    public override int Length => Value.Length + 3;

    public override string ToString()
    {
        return $"{(IsHexadecimal ? 'X' : 'B')}'{Value}'";
    }

    private bool Equals(BitStringLiteral? other)
    {
        return other is not null && Value == other.Value && IsHexadecimal == other.IsHexadecimal;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as BitStringLiteral);
    }

    public override bool Equals(Token? other)
    {
        return Equals(other as BitStringLiteral);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, IsHexadecimal);
    }
}
