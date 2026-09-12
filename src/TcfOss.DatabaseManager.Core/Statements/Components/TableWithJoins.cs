using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record TableWithJoins(TableFactor Relation) : IWriteSql, IAddTablesToContext
{
    public SqlValueList<Join>? Joins { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        Relation.ToSql(writer);

        if (Joins?.Any() ?? false)
        {
            foreach (Join join in Joins)
            {
                join.ToSql(writer);
            }
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Relation.FormatSql(writer, manager);

        if (Joins.SafeAny())
        {
            writer.WriteLine();
            Joins.ForEach(x => x.FormatSql(writer, manager));
        }
    }

    /// <inheritdoc/>
    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    {
        Relation.AddTablesToContext(pseudoTableSet, sourceRef);

        if (Joins.SafeAny())
        {
            Joins?.ForEach(x => x.AddTablesToContext(pseudoTableSet, sourceRef));
        }
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Relation.GetReferencedItems(context))
        {
            yield return item;
        }

        if (Joins.SafeAny())
        {
            foreach (Join join in Joins)
            {
                foreach (ItemRef item in join.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }
}
