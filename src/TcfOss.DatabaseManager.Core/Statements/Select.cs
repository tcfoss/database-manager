using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements;

public record Select(SelectBody Body) : Statement, IAddTablesToContext
{
    public CommonTableExpression? CommonTableExpression { get; init; }
    public SqlValueList<OrderBy>? OrderBy { get; init; }
    public Limit? Limit { get; init; }
    public SelectInto? SelectInto { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        if (CommonTableExpression != null)
        {
            writer.WriteSql($"{CommonTableExpression} ");
        }

        writer.WriteSql($"{Body}");

        if (OrderBy != null)
        {
            writer.WriteSql($" ORDER BY {OrderBy.ToSqlDelimited()}");
        }

        if (Limit != null)
        {
            writer.WriteSql($" {Limit}");
        }

        if (SelectInto != null)
        {
            writer.WriteSql($" {SelectInto}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.SelectStatement))
        {
            manager.PseudoTables ??= new PseudoTableSet(manager.ActiveSchema, []);

            manager.PseudoTables.EnterSelectScope();

            AddTablesToContext(manager.PseudoTables);
            Meta.FormatPreNonSql(writer, manager);
            CommonTableExpression?.FormatSql(writer, manager);
            Body.FormatSql(writer, manager);
            if (OrderBy.SafeAny())
            {
                writer.WriteLine();
                writer.WriteSqlI("ORDER BY ");
                foreach (OrderBy orderBy in OrderBy)
                {
                    orderBy.FormatSql(writer, manager);
                    writer.Write(", ");
                }
                writer.RemoveChar(',');
            }
            if (Limit != null)
            {
                writer.WriteLine();
                Limit.FormatSql(writer, manager);
            }

            if (SelectInto != null)
            {
                writer.WriteLine();
                SelectInto.FormatSql(writer, manager);
            }
            Meta.FormatPostNonSql(writer, manager);
            manager.PseudoTables!.LeaveSelectScope();
        }
    }

    public PseudoTable ToPseudoTable(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
    {
        return Body.ToPseudoTable(name, identifier, sourceRef, type);
    }

    public PseudoTable ToPseudoTableRelaxed(string name, ObjectIdentifier? identifier, SourceRef? sourceRef, PseudoTableType type)
    {
        return Body.ToPseudoTableRelaxed(name, identifier, sourceRef, type);
    }

    /// <inheritdoc/>
    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    {
        CommonTableExpression?.AddTablesToContext(pseudoTableSet, sourceRef);
        Body.AddTablesToContext(pseudoTableSet, sourceRef);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.EnterTableScope())
        {
            HashSet<Identifier> cteNames = [];
            if (CommonTableExpression != null)
            {
                foreach (CommonTableExpressionBody body in CommonTableExpression.Tables)
                {
                    cteNames.Add(body.Name);
                    foreach (ItemRef r in body.Query.GetReferencedItems(context))
                    {
                        yield return r;
                    }

                    if (context.PseudoTables != null)
                    {
                        PseudoTable ctePseudoTable = body.Columns != null
                            ? new PseudoTable(body.Name.Name, null, [.. body.Columns.Select(x => x.Name)], PseudoTableType.CommonTableExpression)
                            : body.Query.ToPseudoTableRelaxed(body.Name.Name, null, null, PseudoTableType.CommonTableExpression);
                        context.PseudoTables.AddExternalSource(body.Name.Name, ctePseudoTable);
                    }
                }
            }

            foreach (ItemRef r in Body.GetReferencedItems(context))
            {
                if (r.Identifiers.Count == 1 && cteNames.Contains(r.Identifiers[0]))
                {
                    continue;
                }
                yield return r;
            }
        }
    }
}
