using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record FunctionCall(ObjectName Name) : Expression
{
    public required FunctionArguments Arguments { get; init; }

    /// <summary>
    /// Inline <c>OVER (...)</c> window specification, when this is a window
    /// function call. Null for ordinary function calls. Named windows (referred
    /// to by <c>OVER window_name</c>) are not yet supported.
    /// </summary>
    public WindowSpec? Over { get; init; }

    // TODO: Parameters?, Filter, NullTreatment, WithinGroup

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name}{Arguments}");
        if (Over != null)
        {
            writer.WriteSql($" OVER {Over}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        // TODO: What if Name is a user-defined function?
        if (Name.Values is [{ QuoteStyle: QuoteStyle.None }] && manager.FunctionNameProvider.IsBuiltInFunction(Name.Values[0].Name))
        {
            writer.Write(Name.Values[0]);
        }
        else
        {
            Name.FormatSql(writer, manager);
        }
        Arguments.FormatSql(writer, manager);
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

        if (Over != null)
        {
            foreach (ItemRef item in Over.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
