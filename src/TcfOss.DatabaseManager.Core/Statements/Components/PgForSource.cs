using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgForSource : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record Range(Expression LowerBound, Expression UpperBound) : PgForSource
    {
        public bool Reverse { get; init; }
        public Expression? Step { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            if (Reverse)
            {
                writer.Write("REVERSE ");
            }
            writer.WriteSql($"{LowerBound} .. {UpperBound}");
            if (Step != null)
            {
                writer.WriteSql($" BY {Step}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            if (Reverse)
            {
                writer.Write("REVERSE ");
            }
            LowerBound.FormatSql(writer, manager);
            writer.Write(" .. ");
            UpperBound.FormatSql(writer, manager);
            if (Step != null)
            {
                writer.Write(" BY ");
                Step.FormatSql(writer, manager);
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in LowerBound.GetReferencedItems(context))
            {
                yield return item;
            }
            foreach (ItemRef item in UpperBound.GetReferencedItems(context))
            {
                yield return item;
            }
            if (Step != null)
            {
                foreach (ItemRef item in Step.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }

    public record Query(Statement Statement) : PgForSource
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Statement.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Statement.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Statement.GetReferencedItems(context);
        }
    }

    public record Execute(Expression Command) : PgForSource
    {
        public SqlValueList<Expression>? Using { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"EXECUTE {Command}");
            if (Using != null)
            {
                writer.WriteSql($" USING {Using}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("EXECUTE ");
            Command.FormatSql(writer, manager);
            if (Using != null)
            {
                writer.Write(" USING ");
                writer.FormatDelimited(Using, manager);
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Command.GetReferencedItems(context))
            {
                yield return item;
            }
            if (Using != null)
            {
                foreach (Expression expression in Using)
                {
                    foreach (ItemRef item in expression.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
