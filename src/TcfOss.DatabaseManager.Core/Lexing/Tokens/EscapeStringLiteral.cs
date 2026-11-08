namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class EscapeStringLiteral(string value) : Token
{
    public string Value { get; } = value;

    public override int Length => Value.Length + 3;

    public override string ToString()
    {
        return $"E'{Value}'";
    }

    private bool Equals(EscapeStringLiteral? other)
    {
        return other is not null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as EscapeStringLiteral);
    }

    public override bool Equals(Token? other)
    {
        return Equals(other as EscapeStringLiteral);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
