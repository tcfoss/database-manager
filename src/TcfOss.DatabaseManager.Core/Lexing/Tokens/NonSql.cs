using System.Diagnostics;

namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

/// <summary>
/// Non-SQL text that appears along-side SQL: comments and whitespace.
/// </summary>
/// <param name="nonSqlType"></param>
/// <param name="value"></param>
public sealed class NonSql(NonSqlType nonSqlType, string? value = null) : Token, IEquatable<NonSql>
{
    public NonSqlType NonSqlType { get; } = nonSqlType;
    public string? Value { get; } = value;
    public string? Prefix { get; init; }

    public override int Length
    {
        get
        {
            return NonSqlType switch
            {
                NonSqlType.Space => 1,
                NonSqlType.Tab => 1,
                NonSqlType.Newline => 1,
                NonSqlType.InlineComment => (Value?.Length ?? 0) + (Prefix?.Length ?? 0),
                NonSqlType.BlockComment => (Value?.Length ?? 0) + 4,
                _ => throw new UnreachableException()
            };
        }
    }

    private bool Equals(NonSql? other)
    {
        return other != null && NonSqlType == other.NonSqlType && Value == other.Value && Prefix == other.Prefix;
    }

    public override bool Equals(object? obj) => Equals(obj as NonSql);

    public override bool Equals(Token? other) => Equals(other as NonSql);

    public override int GetHashCode()
    {
        return HashCode.Combine(NonSqlType, Value, Prefix);
    }

    bool IEquatable<NonSql>.Equals(NonSql? other) => Equals(other);
}
