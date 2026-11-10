using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record Limit : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSql($"{manager.Indent}{this}");
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        IEnumerable<Expression?> expressions = this switch
        {
            ExpressionLimit limit => [limit.Expression],
            MyCommaSeparated limit => [limit.LimitExpression, limit.OffsetExpression],
            MyLimitOffset limit => [limit.LimitExpression, limit.OffsetExpression],
            OffsetFetch limit => [limit.Offset, limit.Fetch],
            PgLimit limit => [limit.Count, limit.Offset],
            PgOffset limit => [limit.Offset],
            PgFetch limit => [limit.Count, limit.Offset],
            _ => []
        };
        foreach (Expression? expression in expressions)
        {
            if (expression != null)
            {
                foreach (ItemRef item in expression.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
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

    public record PgLimit(Expression? Count, Expression? Offset = null) : Limit
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("LIMIT ");
            if (Count == null)
            {
                writer.Write("ALL");
            }
            else
            {
                Count.ToSql(writer);
            }
            if (Offset != null)
            {
                writer.WriteSql($" OFFSET {Offset}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSql($"{manager.Indent}LIMIT ");
            if (Count == null)
            {
                writer.Write("ALL");
            }
            else
            {
                Count.FormatSql(writer, manager);
            }
            if (Offset != null)
            {
                writer.Write(" OFFSET ");
                Offset.FormatSql(writer, manager);
            }
        }
    }

    public record PgOffset(Expression Offset) : Limit
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"OFFSET {Offset}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSql($"{manager.Indent}OFFSET ");
            Offset.FormatSql(writer, manager);
        }
    }

    public record PgFetch(Expression? Count, Expression? Offset = null) : Limit
    {
        public bool WithTies { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            if (Offset != null)
            {
                writer.WriteSql($"OFFSET {Offset} ");
            }
            writer.Write("FETCH FIRST ");
            if (Count != null)
            {
                writer.WriteSql($"{Count} ");
            }
            writer.Write(WithTies ? "ROWS WITH TIES" : "ROWS ONLY");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            if (Offset != null)
            {
                writer.Write("OFFSET ");
                Offset.FormatSql(writer, manager);
                writer.Write(" ");
            }
            writer.Write("FETCH FIRST ");
            if (Count != null)
            {
                Count.FormatSql(writer, manager);
                writer.Write(" ");
            }
            writer.Write(WithTies ? "ROWS WITH TIES" : "ROWS ONLY");
        }
    }
}
