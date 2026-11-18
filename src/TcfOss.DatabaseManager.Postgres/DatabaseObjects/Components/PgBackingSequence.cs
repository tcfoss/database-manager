using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgBackingSequence(ObjectIdentifier Name, DataType DataType, PgIdentityOptions Options)
{
    public bool NameExplicit { get; init; }
    public bool Unlogged { get; init; }

    public virtual bool Equals(PgBackingSequence? other)
    {
        return other is not null && EqualityContract == other.EqualityContract
            && Name == other.Name && DataType == other.DataType
            && Options == other.Options && Unlogged == other.Unlogged;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(EqualityContract, Name, DataType, Options, Unlogged);
    }
}
