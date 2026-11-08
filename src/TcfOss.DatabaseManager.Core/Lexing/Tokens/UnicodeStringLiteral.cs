namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class UnicodeStringLiteral(string value) : Token
{
    public string Value { get; } = value;
    public char? EscapeCharacter { get; init; }
    public int? SourceLength { get; init; }

    public override int Length => SourceLength ?? Value.Length + 4;

    public override string ToString()
    {
        return EscapeCharacter is { } escape ? $"U&'{Value}' UESCAPE '{escape}'" : $"U&'{Value}'";
    }

    private bool Equals(UnicodeStringLiteral? other)
    {
        return other is not null && Value == other.Value && EscapeCharacter == other.EscapeCharacter;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as UnicodeStringLiteral);
    }

    public override bool Equals(Token? other)
    {
        return Equals(other as UnicodeStringLiteral);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, EscapeCharacter);
    }
}
