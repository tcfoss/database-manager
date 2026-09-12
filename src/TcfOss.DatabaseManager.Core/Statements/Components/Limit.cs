using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record Limit : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSql($"{manager.Indent}{this}");
    }

    public record ExpressionLimit(Expression Expression) : Limit
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"LIMIT {Expression}");
        }
    }

    public record MyCommaSeparated(Expression LimitExpression, Expression? OffsetExpression) : Limit
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("LIMIT ");
            if (OffsetExpression != null)
            {
                writer.WriteSql($"{OffsetExpression}, ");
            }
            writer.WriteSql($"{LimitExpression}");
        }
    }

    public record MyLimitOffset(Expression LimitExpression, Expression? OffsetExpression) : Limit
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"LIMIT {LimitExpression}");
            if (OffsetExpression != null)
            {
                writer.WriteSql($" OFFSET {OffsetExpression}");
            }
        }
    }

    // TODO: WITH TIES, PERCENT.
    public record OffsetFetch(Expression Offset, Expression? Fetch) : Limit
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"OFFSET {Offset} ROWS");
            if (Fetch != null)
            {
                writer.WriteSql($" FETCH NEXT {Fetch} ROWS ONLY");
            }
        }
    }
}
