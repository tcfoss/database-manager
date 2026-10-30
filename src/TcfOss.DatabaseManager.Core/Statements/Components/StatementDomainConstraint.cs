using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementDomainConstraint(Identifier? Name = null) : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    protected void WriteName(SqlTextWriter writer)
    {
        if (Name != null)
        {
            writer.WriteSql($"CONSTRAINT {Name} ");
        }
    }

    protected void FormatName(SqlTextWriter writer, FormatManager manager)
    {
        if (Name != null)
        {
            writer.Write("CONSTRAINT ");
            Name.FormatSql(writer, manager);
            writer.Write(" ");
        }
    }

    public record Nullability(bool NotNull, Identifier? Name = null) : StatementDomainConstraint(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            WriteName(writer);
            writer.Write(NotNull ? "NOT NULL" : "NULL");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatName(writer, manager);
            writer.Write(NotNull ? "NOT NULL" : "NULL");
        }
    }

    public record Check(Expression Expression, Identifier? Name = null) : StatementDomainConstraint(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            WriteName(writer);
            writer.WriteSql($"CHECK ({Expression})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatName(writer, manager);
            writer.Write("CHECK (");
            Expression.FormatSql(writer, manager);
            writer.Write(")");
        }
    }
}
