using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public record SimpleSelect(SqlValueList<SimpleSelectItem> Selections) : Statement, IAddTablesToContext
{
    public DistinctFilter? Distinct { get; init; }
    public Top? Top { get; init; }
    public SelectInto? SelectInto { get; init; }
    public SqlValueList<TableWithJoins>? From { get; init; }
    public Expression? Where { get; init; }
    public SqlValueList<Expression>? GroupBy { get; init; }
    public Expression? Having { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("SELECT");
        if (Distinct != null)
        {
            writer.WriteSql($" {Distinct}");
        }
        if (Top != null)
        {
            writer.WriteSql($" {Top}");
        }
        writer.WriteSql($" {Selections}");
        if (SelectInto != null)
        {
            writer.WriteSql($" {SelectInto}");
        }
        if (From != null)
        {
            writer.WriteSql($" FROM {From}");
        }

        if (Where != null)
        {
            writer.WriteSql($" WHERE {Where}");
        }

        if (GroupBy != null)
        {
            writer.WriteSql($" GROUP BY {GroupBy.ToSqlDelimited()}");
        }

        if (Having != null)
        {
            writer.WriteSql($" HAVING {Having}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSqlI("SELECT");
        if (Distinct != null)
        {
            writer.WriteSql($" {Distinct}");
        }
        if (Top != null)
        {
            writer.WriteSql($" {Top}");
        }
        writer.WriteLine();
        manager.IncreaseIndent();

        using (manager.Enter(FormatContext.SelectItem))
        {
            DmlFormatter.FormatSelections(writer, manager, Selections);
        }
        manager.DecreaseIndent();

        if (SelectInto != null)
        {
            DmlFormatter.FormatSelectIntoClause(writer, manager, SelectInto);
        }

        if (From != null)
        {
            DmlFormatter.FormatFromClause(writer, manager, From);
        }

        if (Where != null)
        {
            DmlFormatter.FormatWhereClause(writer, manager, Where);
        }

        if (GroupBy != null)
        {
            DmlFormatter.FormatGroupByClause(writer, manager, GroupBy);
        }

        if (Having != null)
        {
            DmlFormatter.FormatHavingClause(writer, manager, Having);
        }
    }

    public PseudoTable ToPseudoTable(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
    {
        var identifiers = new List<string>();
        var uniqueIdentifiers = new HashSet<string>();
        foreach (SimpleSelectItem item in Selections)
        {
            SourceRef? itemSourceRef = null;
            if (sourceRef != null && item.Meta is { Start: not null, End: not null })
            {
                itemSourceRef = new SourceRef(sourceRef.Value.SourceId, item.Meta.Start.Value, item.Meta.End.Value);
            }
            Identifier curr = item switch
            {
                SimpleSelectItem.QualifiedWildcard => throw new DefinitionException.ViewSelectWildcard(name, itemSourceRef),
                SimpleSelectItem.Wildcard => throw new DefinitionException.ViewSelectWildcard(name, itemSourceRef),
                SimpleSelectItem.ExpressionWithAlias expr => expr.Alias,
                SimpleSelectItem.UnnamedExpression expr => expr.Expression switch
                {
                    SingleIdentifier ident => ident.Identifier,
                    CompoundIdentifier ident => ident.Identifiers.Last(),
                    Wildcard => throw new DefinitionException.ViewSelectWildcard(name, itemSourceRef),
                    QualifiedWildcard => throw new DefinitionException.ViewSelectWildcard(name, itemSourceRef),
                    _ => throw new DefinitionException.ViewSelectUnexpectedItem(name, expr, itemSourceRef),
                },
                _ => throw new DefinitionException.ViewSelectUnexpectedItem(name, item, itemSourceRef),
            };

            if (!uniqueIdentifiers.Add(curr.Name))
            {
                throw new DefinitionException.ViewNonUniqueSelectionName(name, curr.Name, sourceRef);
            }
            identifiers.Add(curr.Name);
        }
        return new PseudoTable(name, identifier, identifiers, type)
        {
            SourceRef = sourceRef
        };
    }

    public PseudoTable ToPseudoTableRelaxed(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
    {
        var identifiers = new List<string>();
        foreach (SimpleSelectItem item in Selections)
        {
            Identifier? curr = item switch
            {
                SimpleSelectItem.ExpressionWithAlias expr => expr.Alias,
                SimpleSelectItem.UnnamedExpression expr => expr.Expression switch
                {
                    SingleIdentifier ident => ident.Identifier,
                    CompoundIdentifier ident => ident.Identifiers.Last(),
                    _ => null,
                },
                _ => null,
            };

            if (curr != null)
            {
                identifiers.Add(curr.Name);
            }
        }
        return new PseudoTable(name, identifier, identifiers, type)
        {
            SourceRef = sourceRef
        };
    }

    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    {
        if (From.SafeAny())
        {
            foreach (TableWithJoins table in From)
            {
                table.AddTablesToContext(pseudoTableSet, sourceRef);
            }
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (From.SafeAny())
        {
            using (context.Enter(ReferencedItemsContext.FromClause))
            {
                foreach (TableWithJoins table in From)
                {
                    if (context.PseudoTables != null)
                    {
                        table.AddTablesToContext(context.PseudoTables);
                    }
                    foreach (ItemRef r in table.GetReferencedItems(context))
                    {
                        yield return r;
                    }
                }
            }
        }

        using (context.Enter(ReferencedItemsContext.SelectItem))
        {
            foreach (SimpleSelectItem item in Selections)
            {
                foreach (ItemRef r in item.GetReferencedItems(context))
                {
                    yield return r;
                }
            }
        }

        if (Where != null)
        {
            using (context.Enter(ReferencedItemsContext.WhereClause))
            {
                foreach (ItemRef r in Where.GetReferencedItems(context))
                {
                    yield return r;
                }
            }
        }
    }
}
