using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record FunctionArgumentClause() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    // TODO: IgnoreOrRespectNulls

    public record OrderBy(SqlValueList<Components.OrderBy> OrderByExpressions) : FunctionArgumentClause
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ORDER BY {OrderByExpressions.ToSqlDelimited()}");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (Components.OrderBy orderBy in OrderByExpressions)
            {
                foreach (ItemRef item in orderBy.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }

    public record Limit(Expression LimitExpression) : FunctionArgumentClause
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"LIMIT {LimitExpression}");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in LimitExpression.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    // TODO: OnOverflow

    public record Separator(Value Value) : FunctionArgumentClause
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SEPARATOR {Value}");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }

    // TODO : Having
}
