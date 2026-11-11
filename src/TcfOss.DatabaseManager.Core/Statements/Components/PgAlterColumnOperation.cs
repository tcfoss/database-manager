using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgAlterColumnOperation : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record ChangeType(DataTypeReference DataType) : PgAlterColumnOperation
    {
        public bool UseSetData { get; init; }
        public ObjectName? Collation { get; init; }
        public Expression? Using { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(UseSetData ? "SET DATA TYPE " : "TYPE ");
            DataType.ToSql(writer);
            if (Collation != null)
            {
                writer.WriteSql($" COLLATE {Collation}");
            }
            if (Using != null)
            {
                writer.WriteSql($" USING {Using}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(UseSetData ? "SET DATA TYPE " : "TYPE ");
            DataType.FormatSql(writer, manager);
            if (Collation != null)
            {
                writer.Write(" COLLATE ");
                Collation.FormatSql(writer, manager);
            }
            if (Using != null)
            {
                writer.Write(" USING ");
                Using.FormatSql(writer, manager);
            }
        }
    }

    public record Nullability(bool NotNull) : PgAlterColumnOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(NotNull ? "SET NOT NULL" : "DROP NOT NULL");
        }
    }

    public record SetExpression(Expression Expression) : PgAlterColumnOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET EXPRESSION AS ({Expression})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SET EXPRESSION AS (");
            Expression.FormatSql(writer, manager);
            writer.Write(")");
        }
    }

    public record DropExpression(bool IfExists) : PgAlterColumnOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(IfExists ? "DROP EXPRESSION IF EXISTS" : "DROP EXPRESSION");
        }
    }

    public record Configuration(PgColumnConfigurationOperation Operation) : PgAlterColumnOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Operation.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Operation.FormatSql(writer, manager);
        }
    }
}
