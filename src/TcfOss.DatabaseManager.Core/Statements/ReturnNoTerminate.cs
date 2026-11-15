using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record ReturnNoTerminate : Statement
{
    public record NextExpr(Expression? Expression) : ReturnNoTerminate
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("RETURN NEXT");
            if (Expression is not null)
            {
                writer.WriteSql($" {Expression}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("RETURN NEXT");
            if (Expression is not null)
            {
                writer.Write(" ");
                Expression.FormatSql(writer, manager);
            }
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            if (Expression is null)
            {
                yield break;
            }
            foreach (ItemRef item in Expression.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public record Query(Statement Statement) : ReturnNoTerminate
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"RETURN QUERY {Statement}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            manager.WriteBlockPart(writer, "RETURN QUERY");
            Statement.FormatSql(writer, manager);
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Statement.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
