using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

namespace TcfOss.DatabaseManager.MsSql.DatabaseObjects.Components;

public abstract record MsColumn(ColumnIdentifier Name)
{
    public record Typed(ColumnIdentifier Name, DataType DataType)
        : MsColumn(Name)
    {
        public ColumnOption.Nullability? Nullability { get; init; }
        public ColumnOption.Identity? Identity { get; init; }
    }
}