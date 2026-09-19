using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.MySql.BuiltIn;

#pragma warning disable CA1711 // Suffix 'Attribute' is by design
public sealed record StringAttribute() : IWriteSql
{
    public string? CharacterSet { get; init; }
    public string? Collation { get; init; }
    public bool CharacterSetInferred { get; init; }
    public bool CollationInferred { get; init; }

    public StringAttribute(string? characterSet, string? collation)
        : this()
    {
        CharacterSet = characterSet;
        Collation = collation;
    }

    public bool EitherSet => CharacterSet != null || Collation != null;

    public void ToSql(SqlTextWriter writer)
    {
        if (CharacterSet != null && Collation != null)
        {
            writer.WriteSql($"CHARACTER SET {CharacterSet} COLLATE {Collation}");
        }
        else if (CharacterSet != null)
        {
            writer.WriteSql($"CHARACTER SET {CharacterSet}");
        }
        else if (Collation != null)
        {
            writer.WriteSql($"COLLATE {Collation}");
        }
    }

    public bool Equals(StringAttribute? other)
    {
        if (other == null)
        {
            return false;
        }

        return CharacterSet == other.CharacterSet && Collation == other.Collation;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CharacterSet, Collation);
    }
}
