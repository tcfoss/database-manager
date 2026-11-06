using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgStatementRoutineOption : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract string UniqueName { get; }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record Language(Identifier Name) : PgStatementRoutineOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"LANGUAGE {Name}");
        }

        public override string UniqueName => "LANGUAGE";
    }

    public record Security(SecurityContext Context) : PgStatementRoutineOption
    {
        public bool External { get; init; }
        public override void ToSql(SqlTextWriter writer)
        {
            if (External)
            {
                writer.Write("EXTERNAL ");
            }
            writer.WriteSql($"SECURITY {Context}");
        }

        public override string UniqueName => "SECURITY";
    }

    public record Parallel(PgRoutineParallel Mode) : PgStatementRoutineOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"PARALLEL {Mode}");
        }

        public override string UniqueName => "PARALLEL";
    }

    public record Cost(Value.Number Value) : PgStatementRoutineOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"COST {Value}");
        }

        public override string UniqueName => "COST";
    }

    public record Rows(Value.Number Value) : PgStatementRoutineOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ROWS {Value}");
        }

        public override string UniqueName => "ROWS";
    }

    public record Volatility(PgRoutineVolatility Mode) : PgStatementRoutineOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Mode}");
        }

        public override string UniqueName => "VOLATILITY";
    }

    public record NullHandling(PgRoutineNullHandling Mode) : PgStatementRoutineOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Mode}");
        }

        public override string UniqueName => "STRICT";
    }

    public record RoutineSetting(PgRoutineSetting Setting) : PgStatementRoutineOption
    {
        public bool UseEqualSign { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            if (UseEqualSign && Setting is PgRoutineSetting.ToValues values)
            {
                writer.WriteSql($"SET {values.Name} = {values.Values.ToSqlDelimited()}");
            }
            else
            {
                Setting.ToSql(writer);
            }
        }

        public override string UniqueName => $"SET:{string.Join('.', Setting.Name.Values.Select(value => value.Name))}";
    }
}
