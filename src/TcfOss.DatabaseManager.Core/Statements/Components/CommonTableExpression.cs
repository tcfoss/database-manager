using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record CommonTableExpression(SqlValueList<CommonTableExpressionBody> Tables, bool Recursive) : IWriteSql, IAddTablesToContext
{
    public void ToSql(SqlTextWriter writer)
    {
        string? recursive = Recursive ? "RECURSIVE " : null;
        writer.WriteSql($"WITH {recursive}{Tables}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        string? recursive = Recursive ? "RECURSIVE " : null;
        writer.WriteSql($"{manager.Indent}WITH {recursive}");
        for (int i = 0; i < Tables.Count; i++)
        {
            if (i > 0)
            {
                writer.WriteLine(",");
            }
            Tables[i].FormatSql(writer, manager);
        }
        writer.WriteLine();
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context, IEnumerable<ItemRef> queryReferences)
    {
        using (context.EnterTableScope())
        {
            HashSet<Identifier> visibleNames = [];
            if (Recursive)
            {
                visibleNames.UnionWith(Tables.Select(table => table.Name));
                if (context.PseudoTables != null)
                {
                    AddTablesToContext(context.PseudoTables);
                }
            }
            foreach (CommonTableExpressionBody body in Tables)
            {
                foreach (ItemRef reference in body.Query.GetReferencedItems(context))
                {
                    if (!IsCteReference(reference, visibleNames))
                    {
                        yield return reference;
                    }
                }
                visibleNames.Add(body.Name);
                if (!Recursive && context.PseudoTables != null)
                {
                    body.AddTablesToContext(context.PseudoTables);
                }
            }
            foreach (ItemRef reference in queryReferences)
            {
                if (!IsCteReference(reference, visibleNames))
                {
                    yield return reference;
                }
            }
        }
    }

    private static bool IsCteReference(ItemRef reference, HashSet<Identifier> names)
    {
        return reference is ItemRef.ObjectNameRef
            && reference.Type is ItemType.Unknown or ItemType.Table or ItemType.View or ItemType.DerivedTable
            && reference.Identifiers is [var name] && names.Contains(name);
    }

    /// <inheritdoc cref="AddTablesToContext" />
    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    {
        foreach (CommonTableExpressionBody table in Tables)
        {
            table.AddTablesToContext(pseudoTableSet, sourceRef);
        }
    }
}
