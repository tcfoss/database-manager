using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class NonSqlContainer(ValueList<NonSql> subTokens) : Token
{
    public ValueList<NonSql> SubTokens { get; } = subTokens;

    public override int Length => SubTokens.Sum(x => x.Length);

    private bool Equals(NonSqlContainer? other)
    {
        return other != null && SubTokens.SequenceEqual(other.SubTokens);
    }

    public override bool Equals(object? obj) => Equals(obj as NonSqlContainer);

    public override bool Equals(Token? other) => Equals(other as NonSqlContainer);

    public override int GetHashCode()
    {
        return HashCode.Combine(SubTokens);
    }
}
