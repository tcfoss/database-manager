using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementRoutineParameter(Identifier Name, DataTypeReference DataType) : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Directed(Identifier Name, DataTypeReference DataType, RoutineParameterDirection? Direction)
        : StatementRoutineParameter(Name, DataType)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            if (Direction != null)
            {
                writer.Write($"{Direction} ");
            }
            writer.WriteSql($"{Name} {DataType}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            if (Direction != null)
            {
                writer.Write($"{Direction} ");
            }
            Name.FormatSql(writer, manager);
            writer.Write(" ");
            DataType.FormatSql(writer, manager);
        }
    }

    public record Undirected(Identifier Name, DataTypeReference DataType) : StatementRoutineParameter(Name, DataType)
    {
        public Value? DefaultValue { get; init; }
        public OutputKeyword? Output { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} {DataType}");
            if (DefaultValue != null)
            {
                writer.WriteSql($" = {DefaultValue}");
            }
            if (Output != null)
            {
                writer.Write($" {Output}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
            writer.Write(" ");
            DataType.FormatSql(writer, manager);
            if (DefaultValue != null)
            {
                writer.WriteSql($" = {DefaultValue}");
            }
            if (Output != null)
            {
                writer.Write($" {Output}");
            }
        }
    }
}
