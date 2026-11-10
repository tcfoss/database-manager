using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record FunctionCall(ObjectName Name) : Expression
{
    public required FunctionArguments Arguments { get; init; }
    public WindowSpec? Over { get; init; }

    public SqlValueList<Statements.Components.OrderBy>? WithinGroup { get; init; }
    public Expression? Filter { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name}{Arguments}");
        if (WithinGroup != null)
        {
            writer.WriteSql($" WITHIN GROUP (ORDER BY {WithinGroup.ToSqlDelimited()})");
        }
        if (Filter != null)
        {
            writer.WriteSql($" FILTER (WHERE {Filter})");
        }
        if (Over != null)
        {
            writer.WriteSql($" OVER {Over}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        if (Name.Values is [{ QuoteStyle: QuoteStyle.None }] && manager.FunctionNameProvider.IsBuiltInFunction(Name.Values[0].Name))
        {
            writer.Write(Name.Values[0]);
        }
        else
        {
            Name.FormatSql(writer, manager);
        }
        Arguments.FormatSql(writer, manager);
        if (WithinGroup != null)
        {
            writer.Write(" WITHIN GROUP (ORDER BY ");
            for (int index = 0; index < WithinGroup.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(", ");
                }
                WithinGroup[index].FormatInline(writer, manager);
            }
            writer.Write(")");
        }
        if (Filter != null)
        {
            writer.Write(" FILTER (WHERE ");
            Filter.FormatSql(writer, manager);
            writer.Write(")");
        }
        if (Over != null)
        {
            writer.Write(" OVER ");
            Over.FormatSql(writer, manager);
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        ItemRef? functionOrProcRef = context.CreateFunctionOrProcRef(Name);
        if (functionOrProcRef != null)
        {
            yield return functionOrProcRef;
        }

        foreach (ItemRef item in Arguments.GetReferencedItems(context))
        {
            yield return item;
        }

        if (WithinGroup != null)
        {
            foreach (Statements.Components.OrderBy order in WithinGroup)
            {
                foreach (ItemRef item in order.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }

        if (Filter != null)
        {
            foreach (ItemRef item in Filter.GetReferencedItems(context))
            {
                yield return item;
            }
        }

        if (Over != null)
        {
            foreach (ItemRef item in Over.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
