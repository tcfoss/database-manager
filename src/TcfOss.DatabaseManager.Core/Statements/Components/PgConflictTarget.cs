using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgConflictTarget : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record IndexInference(SqlValueList<PgConflictIndexElement> Elements, Expression? Where = null) : PgConflictTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"({Elements})");
            if (Where != null)
            {
                writer.WriteSql($" WHERE {Where}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("(");
            writer.FormatDelimited(Elements, manager);
            writer.Write(")");
            if (Where != null)
            {
                writer.Write(" WHERE ");
                Where.FormatSql(writer, manager);
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (PgConflictIndexElement element in Elements)
            {
                foreach (ItemRef item in element.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
            foreach (ItemRef item in Where?.GetReferencedItems(context) ?? [])
            {
                yield return item;
            }
        }
    }

    public record Constraint(Identifier Name) : PgConflictTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ON CONSTRAINT {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("ON CONSTRAINT ");
            Name.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return [];
        }
    }
}
