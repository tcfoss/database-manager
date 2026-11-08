using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions.Components;

public abstract record ArraySubscriptDimension : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public sealed record Index(Expression Expression) : ArraySubscriptDimension
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Expression.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Expression.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Expression.GetReferencedItems(context);
        }
    }

    public sealed record Slice(Expression? LowerBound, Expression? UpperBound) : ArraySubscriptDimension
    {
        public override void ToSql(SqlTextWriter writer)
        {
            LowerBound?.ToSql(writer);
            writer.Write(":");
            UpperBound?.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            LowerBound?.FormatSql(writer, manager);
            writer.Write(":");
            UpperBound?.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            if (LowerBound != null)
            {
                foreach (ItemRef item in LowerBound.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
            if (UpperBound != null)
            {
                foreach (ItemRef item in UpperBound.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }
}
