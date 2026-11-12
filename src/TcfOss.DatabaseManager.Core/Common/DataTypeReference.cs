using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using PgPseudoTypeValue = TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes.PgPseudoType;

namespace TcfOss.DatabaseManager.Core.Common;

public abstract record DataTypeReference : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public static implicit operator DataTypeReference(DataType dataType)
    {
        return new BuiltIn(dataType);
    }

    public record BuiltIn(DataType DataType) : DataTypeReference
    {
        public override void ToSql(SqlTextWriter writer)
        {
            DataType.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            DataType.ToSql(writer);
        }
    }

    public record Named(ObjectName Name) : DataTypeReference
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Name.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
        }
    }

    public record PgPseudoType(PgPseudoTypeValue Type) : DataTypeReference
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Type}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }

    public record PgTypeOf(ObjectName Name) : DataTypeReference
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Name.ToSql(writer);
            writer.Write("%TYPE");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
            writer.Write("%TYPE");
        }
    }

    public record Array(DataTypeReference ElementType, SqlValueList<Array.Dimension> Dimensions) : DataTypeReference
    {
        public readonly record struct Dimension(uint? Length);

        public bool UseArrayKeyword { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            ElementType.ToSql(writer);
            WriteSuffix(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ElementType.FormatSql(writer, manager);
            WriteSuffix(writer);
        }

        private void WriteSuffix(SqlTextWriter writer)
        {
            if (UseArrayKeyword)
            {
                writer.Write(" ARRAY");
            }
            foreach (Dimension dimension in Dimensions)
            {
                writer.WriteSql($"[{dimension.Length}]");
            }
        }
    }
}
