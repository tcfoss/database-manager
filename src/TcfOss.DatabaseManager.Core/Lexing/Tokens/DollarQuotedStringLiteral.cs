namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class DollarQuotedStringLiteral(string tag, string value) : Token
{
    public string Tag { get; } = tag;
    public string Value { get; } = value;

    public string Delimiter => $"${Tag}$";

    public override int Length => Delimiter.Length * 2 + Value.Length;

    public override string ToString()
    {
        return $"{Delimiter}{Value}{Delimiter}";
    }

    private bool Equals(DollarQuotedStringLiteral? other)
    {
        return other != null && Tag == other.Tag && Value == other.Value;
    }

    public override bool Equals(Token? other) => Equals(other as DollarQuotedStringLiteral);
    public override bool Equals(object? obj) => Equals(obj as DollarQuotedStringLiteral);
    public override int GetHashCode() => HashCode.Combine(Tag, Value);
}
