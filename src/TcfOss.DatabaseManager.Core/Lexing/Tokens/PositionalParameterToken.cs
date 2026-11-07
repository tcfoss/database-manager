namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class PositionalParameterToken(string value) : Token
{
    public string Value { get; } = value;

    public override int Length => Value.Length + 1;

    public override string ToString()
    {
        return "$" + Value;
    }

    private bool Equals(PositionalParameterToken? other)
    {
        return other is not null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PositionalParameterToken);
    }

    public override bool Equals(Token? other)
    {
        return Equals(other as PositionalParameterToken);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
