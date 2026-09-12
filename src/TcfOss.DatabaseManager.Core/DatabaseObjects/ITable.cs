using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public interface ITable
    : IDatabaseObject, IEquatable<ITable>
{
    public SqlValueList<IColumn> Columns { get; }
}
