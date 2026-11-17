using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record PgExecute(
    Expression Command,
    SqlValueList<ObjectName>? Into,
    bool Strict,
    SqlValueList<Expression>? Using) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"EXECUTE {Command}");
        if (Into != null)
        {
            writer.Write(" INTO ");
            if (Strict)
            {
                writer.Write("STRICT ");
            }
            writer.WriteDelimited(Into);
        }
        if (Using != null)
        {
            writer.WriteSql($" USING {Using}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("EXECUTE ");
        Command.FormatSql(writer, manager);
        if (Into != null)
        {
            writer.Write(" INTO ");
            if (Strict)
            {
                writer.Write("STRICT ");
            }
            for (int i = 0; i < Into.Count; i++)
            {
                if (i > 0)
                {
                    writer.Write(", ");
                }
                Into[i].FormatSql(writer, manager);
            }
        }
        if (Using != null)
        {
            writer.Write(" USING ");
            writer.FormatDelimited(Using, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
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
