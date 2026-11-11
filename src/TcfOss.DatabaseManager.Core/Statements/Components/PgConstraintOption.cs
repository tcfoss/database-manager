using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgConstraintOption : IWriteSql
{
    public abstract string UniqueName { get; }
    public abstract void ToSql(SqlTextWriter writer);

    public record Deferrable(bool Value) : PgConstraintOption
    {
        public override string UniqueName => "DEFERRABLE";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Value ? "DEFERRABLE" : "NOT DEFERRABLE");
        }
    }

    public record InitiallyDeferred(bool Value) : PgConstraintOption
    {
        public override string UniqueName => "INITIALLY";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Value ? "INITIALLY DEFERRED" : "INITIALLY IMMEDIATE");
        }
    }

    public record NotValid : PgConstraintOption
    {
        public override string UniqueName => "NOT VALID";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("NOT VALID");
        }
    }

    public record NoInherit : PgConstraintOption
    {
        public override string UniqueName => "NO INHERIT";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("NO INHERIT");
        }
    }
}
