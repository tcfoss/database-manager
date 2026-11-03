using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class Label(Identifier identifier) : Token
{
    public Identifier Identifier { get; } = identifier;

    public override int Length => Identifier.Name.Length + (Identifier.QuoteStyle != QuoteStyle.None ? 2 : 0) + 1;

    private bool Equals(Label? other)
    {
        return other != null && Identifier == other.Identifier;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Label);
    }

    public override bool Equals(Token? other)
    {
        return Equals(other as Label);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Identifier);
    }
}
