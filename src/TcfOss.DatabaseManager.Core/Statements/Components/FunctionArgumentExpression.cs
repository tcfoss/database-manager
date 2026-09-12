using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record FunctionArgumentExpression() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record FunctionExpression(Expression Expression) : FunctionArgumentExpression
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
            foreach (ItemRef item in Expression.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public record QualifiedWildcard(ObjectName Name) : FunctionArgumentExpression
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name}.*");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
            writer.Write(".*");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            ItemRef? item = context.CreateObjectRef(Name);
            if (item != null)
            {
                yield return item;
            }
        }
    }

    public record Wildcard() : FunctionArgumentExpression
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("*");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("*");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }
}
