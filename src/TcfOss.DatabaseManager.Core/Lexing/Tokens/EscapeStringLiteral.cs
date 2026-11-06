namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class EscapeStringLiteral(string value) : Token
{
    public string Value { get; } = value;

    public override int Length => Value.Length + 3;

    public override string ToString()
    {
        return $"E'{Value}'";
    }

    public override bool Equals(object? obj)
    {
        return obj is EscapeStringLiteral other && Value == other.Value;
    }

    public override bool Equals(Token? other)
    {
        return Equals((object?)other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
