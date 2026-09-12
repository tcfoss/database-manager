using System.Diagnostics;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.Common;

/// <summary>
/// Represents a handle to a database object, consisting of catalog, schema, and name. This is
/// used as a key in dictionaries. It should be instantiated in the correct case (or *a* correct
/// case) for the SQL dialect.
/// </summary>
/// <param name="Catalog"></param>
/// <param name="Schema"></param>
/// <param name="Name"></param>
[DebuggerDisplay("{Catalog}.{Schema}.{Name}")]
public readonly record struct ObjectHandle(string Catalog, string Schema, string Name)
    : IComparable<ObjectHandle>
{
    public override string ToString()
    {
        return $"{Catalog}.{Schema}.{Name}";
    }

    public static ObjectHandle Create(ObjectIdentifier identifier, NameHandling nameHandling)
    {
        return new ObjectHandle(
            Handle.GetNamePart(identifier.Schema.Catalog, nameHandling),
            Handle.GetNamePart(identifier.Schema, nameHandling),
            Handle.GetNamePart(identifier, nameHandling));
    }

    public static ObjectHandle Create(SqlValueList<Identifier> name, SchemaIdentifier schema, NameHandling nameHandling)
    {
        if (name.Count == 3)
        {
            return new ObjectHandle(
                Handle.GetNamePart(name[0], nameHandling),
                Handle.GetNamePart(name[1], nameHandling),
                Handle.GetNamePart(name[2], nameHandling));
        }
        if (name.Count == 2)
        {
            return new ObjectHandle(
                Handle.GetNamePart(schema.Catalog, nameHandling),
                Handle.GetNamePart(name[0], nameHandling),
                Handle.GetNamePart(name[1], nameHandling));
        }
        if (name.Count == 1)
        {
            return new ObjectHandle(
                Handle.GetNamePart(schema.Catalog, nameHandling),
                Handle.GetNamePart(schema, nameHandling),
                Handle.GetNamePart(name[0], nameHandling));
        }
        throw new IdentifierMismatchException.IdentifierLengthException("object", 3, name.Count, name.Select(n => n.Name));
    }

    public static ObjectHandle Create(ObjectName name, SchemaIdentifier schema, NameHandling nameHandling)
    {
        return Create(name.Values, schema, nameHandling);
    }

    public int CompareTo(ObjectHandle other)
    {
        int catalogComp = string.Compare(Catalog, other.Catalog, StringComparison.Ordinal);
        if (catalogComp != 0)
        {
            return catalogComp;
        }
        int schemaComp = string.Compare(Schema, other.Schema, StringComparison.Ordinal);
        if (schemaComp != 0)
        {
            return schemaComp;
        }
        return string.Compare(Name, other.Name, StringComparison.Ordinal);
    }

    public static bool operator <(ObjectHandle left, ObjectHandle right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(ObjectHandle left, ObjectHandle right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(ObjectHandle left, ObjectHandle right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(ObjectHandle left, ObjectHandle right)
    {
        return left.CompareTo(right) >= 0;
    }
}
