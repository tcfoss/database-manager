using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgColumn(ColumnIdentifier Name, DataType DataType)
{
    public ColumnOption.Nullability? Nullability { get; init; }
    public ColumnOption.ColumnDefault? Default { get; init; }
}
