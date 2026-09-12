namespace TcfOss.DatabaseManager.Core.Lexing.Tokens;

public abstract class Token : IEquatable<Token>
{
    public required Location Location { get; init; }
    public List<NonSql>? PreNonSql { get; set; }
    public virtual int Length => 1;

    public abstract bool Equals(Token? other);

    public abstract override int GetHashCode();

    public abstract override bool Equals(object? obj);
}
