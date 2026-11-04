using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementRoutineParameter(DataTypeReference DataType) : IWriteSql
{
    public abstract Identifier? Name { get; }
    public abstract RoutineParameterDirection? Direction { get; }
    public abstract ParameterDefault? ParameterDefault { get; }

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Directed(Identifier ParameterName, DataTypeReference DataType, RoutineParameterDirection? Direction, ParameterDefault? Default = null)
        : StatementRoutineParameter(DataType)
    {
        public override Identifier Name => ParameterName;
        public override RoutineParameterDirection? Direction { get; } = Direction;
        public override ParameterDefault? ParameterDefault { get; } = Default;

        public override void ToSql(SqlTextWriter writer)
        {
            WritePrefix(writer, Name, DataType, Direction);
            ParameterDefault?.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatPrefix(writer, manager, Name, DataType, Direction);
            ParameterDefault?.FormatSql(writer, manager);
        }
    }

    public record Undirected(Identifier ParameterName, DataTypeReference DataType, ParameterDefault.ValueDefault? Default = null) : StatementRoutineParameter(DataType)
    {
        public override Identifier Name => ParameterName;
        public override RoutineParameterDirection? Direction => null;
        public override ParameterDefault.ValueDefault? ParameterDefault { get; } = Default;
        public MsRoutineParameterOptions? Options { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} {DataType}");
            Options?.WriteBeforeDefault(writer);
            if (ParameterDefault != null)
            {
                writer.WriteSql($" = {ParameterDefault.Value}");
            }
            Options?.WriteAfterDefault(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ParameterName.FormatSql(writer, manager);
            writer.Write(" ");
            DataType.FormatSql(writer, manager);
            Options?.WriteBeforeDefault(writer);
            if (ParameterDefault != null)
            {
                writer.WriteSql($" = {ParameterDefault.Value}");
            }
            Options?.WriteAfterDefault(writer);
        }
    }

    public record General(Identifier? ParameterName, DataTypeReference DataType, RoutineParameterDirection? Direction,
        ParameterDefault? Default, MsRoutineParameterOptions? Options = null) : StatementRoutineParameter(DataType)
    {
        public override Identifier? Name => ParameterName;
        public override RoutineParameterDirection? Direction { get; } = Direction;
        public override ParameterDefault? ParameterDefault { get; } = Default;

        public override void ToSql(SqlTextWriter writer)
        {
            WritePrefix(writer, Name, DataType, Direction);
            Options?.WriteBeforeDefault(writer);
            ParameterDefault?.ToSql(writer);
            Options?.WriteAfterDefault(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatPrefix(writer, manager, Name, DataType, Direction);
            Options?.WriteBeforeDefault(writer);
            ParameterDefault?.FormatSql(writer, manager);
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
