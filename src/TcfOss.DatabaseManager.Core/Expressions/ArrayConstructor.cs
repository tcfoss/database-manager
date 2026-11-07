using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Expressions;

public abstract record ArrayConstructor : Expression
{
    public sealed record Elements(SqlValueList<Expression> Values) : ArrayConstructor
    {
        public bool UseArrayKeyword { get; init; } = true;

        public override void ToSql(SqlTextWriter writer)
        {
            if (UseArrayKeyword)
            {
                writer.Write("ARRAY");
            }
            writer.WriteSql($"[{Values}]");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(UseArrayKeyword ? "ARRAY[" : "[");
            for (int index = 0; index < Values.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(", ");
                }
                Values[index].FormatSql(writer, manager);
            }
            writer.Write("]");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (Expression value in Values)
            {
                foreach (ItemRef item in value.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }

    public sealed record Subquery(Select Query) : ArrayConstructor
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ARRAY({Query})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("ARRAY(");
            Query.FormatSql(writer, manager);
            writer.Write(")");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Query.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
