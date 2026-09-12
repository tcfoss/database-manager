using System.Diagnostics;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.Core;

/// <summary>
/// A list-like entity implementing value-equality.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class SqlValueList<T> : ValueList<T>, IEquatable<SqlValueList<T>>, IWriteSql
    where T : IEquatable<T>?
{
    [DebuggerStepThrough]
    public SqlValueList()
    { }

    [DebuggerStepThrough]
    public SqlValueList(IEnumerable<T> sequence) : base(sequence)
    { }

    [DebuggerStepThrough]
    public SqlValueList(int capacity) : base(capacity)
    { }

    public void ToSql(SqlTextWriter writer)
    {
        for (int i = 0; i < Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }
            T item = this[i];
            if (item is IWriteSql sql)
            {
                sql.ToSql(writer);
            }
            else
            {
                writer.Write(item?.ToString());
            }
        }
    }

    public bool Equals(SqlValueList<T>? other) => base.Equals(other);

    public static implicit operator T[](SqlValueList<T> list)
    {
        return [.. list];
    }

    public static implicit operator SqlValueList<T>(T[] array)
    {
        return [.. array];
    }

    public override bool Equals(object? obj) => Equals(obj as SqlValueList<T>);

    public override int GetHashCode() => base.GetHashCode();
}
