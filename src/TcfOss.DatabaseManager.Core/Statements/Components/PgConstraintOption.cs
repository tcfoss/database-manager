using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgConstraintOption : IWriteSql, IHaveDuplicateCheckKey
{
    public abstract string DuplicateCheckKey { get; }
    public abstract void ToSql(SqlTextWriter writer);

    public record Deferrable(bool Value) : PgConstraintOption
    {
        public override string DuplicateCheckKey => "DEFERRABLE";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Value ? "DEFERRABLE" : "NOT DEFERRABLE");
        }
    }

    public record InitiallyDeferred(bool Value) : PgConstraintOption
    {
        public override string DuplicateCheckKey => "INITIALLY";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Value ? "INITIALLY DEFERRED" : "INITIALLY IMMEDIATE");
        }
    }

    public record NotValid : PgConstraintOption
    {
        public override string DuplicateCheckKey => "NOT VALID";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("NOT VALID");
        }
    }

    public record NoInherit : PgConstraintOption
    {
        public override string DuplicateCheckKey => "NO INHERIT";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("NO INHERIT");
        }
    }
}
