using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Common;

public abstract record DataTypeReference : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public static implicit operator DataTypeReference(DataType dataType) => new BuiltIn(dataType);

    public record BuiltIn(DataType DataType) : DataTypeReference
    {
        public override void ToSql(SqlTextWriter writer) => DataType.ToSql(writer);

        public override void FormatSql(SqlTextWriter writer, FormatManager manager) => DataType.ToSql(writer);
    }

    public record Named(ObjectName Name) : DataTypeReference
    {
        public override void ToSql(SqlTextWriter writer) => Name.ToSql(writer);

        public override void FormatSql(SqlTextWriter writer, FormatManager manager) => Name.FormatSql(writer, manager);
    }
}
