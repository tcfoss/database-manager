using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record RoutineParameter(Identifier Name, DataType DataType) : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    /// <summary>
    /// MySQL/MariaDB-style parameter: optional leading direction keyword
    /// (IN/OUT/INOUT), no default value.
    /// </summary>
    public record Directed(Identifier Name, DataType DataType, RoutineParameterDirection? Direction) : RoutineParameter(Name, DataType)
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
            writer.WriteSql($" {DataType}");
        }
    }

    /// <summary>
    /// T-SQL-style parameter: <c>@name TYPE [= default] [OUT|OUTPUT]</c>.
    /// No direction keyword; default value and OUTPUT marker are independent
    /// and both optional.
    /// </summary>
    public record Undirected(Identifier Name, DataType DataType) : RoutineParameter(Name, DataType)
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
            writer.WriteSql($" {DataType}");
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
