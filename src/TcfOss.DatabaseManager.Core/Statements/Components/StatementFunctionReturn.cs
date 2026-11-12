using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementFunctionReturn : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public static implicit operator StatementFunctionReturn(DataTypeReference dataType)
    {
        return new DataTypeReturn(dataType);
    }

    public static implicit operator StatementFunctionReturn(DataType dataType)
    {
        return new DataTypeReturn(dataType);
    }

    public abstract record Element : StatementFunctionReturn;

    public record DataTypeReturn(DataTypeReference DataType) : Element
    {
        public override void ToSql(SqlTextWriter writer)
        {
            DataType.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            DataType.FormatSql(writer, manager);
        }
    }

    public record SetOf(Element ReturnType) : StatementFunctionReturn
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SETOF {ReturnType}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SETOF ");
            ReturnType.FormatSql(writer, manager);
        }
    }

    public record Table(SqlValueList<TableColumn> Columns) : StatementFunctionReturn
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"TABLE ({Columns})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("TABLE (");
            for (int index = 0; index < Columns.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(", ");
                }
                Columns[index].FormatSql(writer, manager);
            }
            writer.Write(")");
        }
    }

    public record TableColumn(Identifier Name, DataTypeReference DataType) : IWriteSql
    {
        public void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} {DataType}");
        }

        public void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
            writer.Write(" ");
            DataType.FormatSql(writer, manager);
        }
    }
}
