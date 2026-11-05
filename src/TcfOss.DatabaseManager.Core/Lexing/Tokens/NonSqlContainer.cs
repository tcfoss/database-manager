namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public sealed class NonSqlContainer(NonSqlList subTokens) : Token
{
    public NonSqlList SubTokens { get; } = subTokens;

    public override int Length => SubTokens.Length;

    private bool Equals(NonSqlContainer? other)
    {
        return other != null && SubTokens.SequenceEqual(other.SubTokens);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as NonSqlContainer);
    }

    public override bool Equals(Token? other)
    {
        return Equals(other as NonSqlContainer);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (Statements.Components.NonSql item in SubTokens)
        {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }
}
