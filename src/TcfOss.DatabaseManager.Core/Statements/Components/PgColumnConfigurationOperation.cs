using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgColumnConfigurationOperation : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record SetStatistics(int Target) : PgColumnConfigurationOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET STATISTICS {Target}");
        }
    }

    public record SetDefaultStatistics : PgColumnConfigurationOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SET STATISTICS DEFAULT");
        }
    }

    public record SetOptions(SqlValueList<PgStorageParameter> Parameters) : PgColumnConfigurationOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET ({Parameters})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SET (");
            writer.FormatDelimited(Parameters, manager);
            writer.Write(")");
        }
    }

    public record ResetOptions(SqlValueList<Identifier> Parameters) : PgColumnConfigurationOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"RESET ({Parameters})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("RESET (");
            writer.FormatDelimited(Parameters, manager);
            writer.Write(")");
        }
    }

    public record SetStorage(PgColumnStorageMode Mode) : PgColumnConfigurationOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET STORAGE {Mode}");
        }
    }

    public record SetCompression(Identifier Method) : PgColumnConfigurationOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET COMPRESSION {Method}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SET COMPRESSION ");
            Method.FormatSql(writer, manager);
        }
    }

    public record SetDefaultCompression : PgColumnConfigurationOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SET COMPRESSION DEFAULT");
        }
    }
}
