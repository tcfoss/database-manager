using NonSqlComponent = TcfOss.DatabaseManager.Core.Statements.Components.NonSql;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

/// <summary>
/// Non-SQL text that appears along-side SQL: a comment or a run of one kind of whitespace.
/// </summary>
/// <param name="value">The parsed non-SQL component.</param>
/// <param name="length">Number of source characters covered.</param>
public sealed class NonSql(NonSqlComponent value, int length) : Token, IEquatable<NonSql>
{
    public NonSqlComponent Value { get; } = value;

    public override int Length => length;

    private bool Equals(NonSql? other)
    {
        return other != null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as NonSql);
    }

    public override bool Equals(Token? other)
    {
        return Equals(other as NonSql);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    bool IEquatable<NonSql>.Equals(NonSql? other)
    {
        return Equals(other);
    }
}
