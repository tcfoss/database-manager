using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementRoutineParameter(DataTypeReference DataType) : IWriteSql
{
    public abstract Identifier? Name { get; }

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Directed(Identifier ParameterName, DataTypeReference DataType, RoutineParameterDirection? Direction)
        : StatementRoutineParameter(DataType)
    {
        public override Identifier Name => ParameterName;
        public ParameterDefault? Default { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WritePrefix(writer, Name, DataType, Direction);
            Default?.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatPrefix(writer, manager, Name, DataType, Direction);
            Default?.FormatSql(writer, manager);
        }
    }

    public record Undirected(Identifier ParameterName, DataTypeReference DataType) : StatementRoutineParameter(DataType)
    {
        public override Identifier Name => ParameterName;
        public Value? DefaultValue { get; init; }
        public MsRoutineParameterOptions? Options { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} {DataType}");
            Options?.WriteBeforeDefault(writer);
            if (DefaultValue != null)
            {
                writer.WriteSql($" = {DefaultValue}");
            }
            Options?.WriteAfterDefault(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ParameterName.FormatSql(writer, manager);
            writer.Write(" ");
            DataType.FormatSql(writer, manager);
            Options?.WriteBeforeDefault(writer);
            if (DefaultValue != null)
            {
                writer.WriteSql($" = {DefaultValue}");
            }
            Options?.WriteAfterDefault(writer);
        }
    }

    public record General(Identifier? ParameterName, DataTypeReference DataType, RoutineParameterDirection? Direction,
        ParameterDefault? Default, MsRoutineParameterOptions? Options = null) : StatementRoutineParameter(DataType)
    {
        public override Identifier? Name => ParameterName;

        public override void ToSql(SqlTextWriter writer)
        {
            WritePrefix(writer, Name, DataType, Direction);
            Options?.WriteBeforeDefault(writer);
            Default?.ToSql(writer);
            Options?.WriteAfterDefault(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatPrefix(writer, manager, Name, DataType, Direction);
            Options?.WriteBeforeDefault(writer);
            Default?.FormatSql(writer, manager);
            Options?.WriteAfterDefault(writer);
        }
    }

    private static void WritePrefix(SqlTextWriter writer, Identifier? name, DataTypeReference dataType,
        RoutineParameterDirection? direction)
    {
        if (direction != null)
        {
            writer.Write($"{direction} ");
        }
        if (name != null)
        {
            writer.WriteSql($"{name} ");
        }
        dataType.ToSql(writer);
    }

    private static void FormatPrefix(SqlTextWriter writer, FormatManager manager, Identifier? name,
        DataTypeReference dataType, RoutineParameterDirection? direction)
    {
        if (direction != null)
        {
            writer.Write($"{direction} ");
        }
        if (name != null)
        {
            name.FormatSql(writer, manager);
            writer.Write(" ");
        }
        dataType.FormatSql(writer, manager);
    }
}
