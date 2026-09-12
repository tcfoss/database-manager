namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class NationalStringLiteral(string value) : Token
{
    public string Value { get; } = value;

    public override string ToString()
    {
        return $"N'{Value}'";
    }

    private bool Equals(NationalStringLiteral? other)
    {
        return other != null && Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as NationalStringLiteral);

    public override bool Equals(Token? other) => Equals(other as NationalStringLiteral);

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
