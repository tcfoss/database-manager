namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public class HexStringLiteral(string value) : Token
{
    public string Value { get; } = value;

    public override int Length => Value.Length + 3;

    public override string ToString()
    {
        return $"X'{Value}'";
    }

    private bool Equals(HexStringLiteral? other)
    {
        return other != null && Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as HexStringLiteral);

    public override bool Equals(Token? other) => Equals(other as HexStringLiteral);

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
