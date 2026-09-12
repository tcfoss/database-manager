using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public interface IColumn
    : IEquatable<IColumn>
{
    public ColumnIdentifier Name { get; }
    public DataType DataType { get; }
}
