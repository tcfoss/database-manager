using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record CommonTableExpressionBody(Identifier Name, SqlValueList<Identifier>? Columns, Statement Query, Identifier? From = null) : IWriteSql, IAddTablesToContext
{
    public bool? Materialized { get; init; }
    public PgCteSearch? Search { get; init; }
    public PgCteCycle? Cycle { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        Name.ToSql(writer);
        if (Columns.SafeAny())
        {
            writer.WriteSql($" ({Columns})");
        }
        writer.WriteSql($" AS {GetMaterializedSql()}({Query})");

        if (From != null)
        {
            writer.WriteSql($" FROM {From}");
        }
        if (Search != null)
        {
            writer.WriteSql($" {Search}");
        }
        if (Cycle != null)
        {
            writer.WriteSql($" {Cycle}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write(manager.Indent);
        writer.Write(manager.ComponentNormalizer.NormalizeSingleIdentifier(Name, manager.Formatting.CteDeclarationQuoting));
        if (Columns.SafeAny())
        {
            writer.WriteSql($" ({Columns})");
        }
        writer.Write($" AS {GetMaterializedSql()}(");
        if (Query.Meta.PreNonSql?.Any(nonSql => nonSql is NonSql.Newlines) != true)
        {
            writer.WriteLine();
        }
        manager.IncreaseIndent();
        Query.FormatSql(writer, manager);
        manager.DecreaseIndent();
        writer.Write(")");

        if (From != null)
        {
            writer.WriteSql($" FROM {From}");
        }
        if (Search != null)
        {
            writer.Write(" ");
            Search.FormatSql(writer, manager);
        }
        if (Cycle != null)
        {
            writer.Write(" ");
            Cycle.FormatSql(writer, manager);
        }
    }

    /// <inheritdoc cref="CommonTableExpression.AddTablesToContext" />
    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    {
        pseudoTableSet.AddExternalSource(Name.Name, ToPseudoTable(sourceRef));
    }

    public PseudoTable ToPseudoTable(SourceRef? sourceRef = null, bool relaxed = true)
    {
        PseudoTable result = CreateResultTable(sourceRef, relaxed);
        List<string> columns = [.. result.SelectableItems];
        if (Search != null)
        {
            columns.Add(Search.SequenceColumn.Name);
        }
        if (Cycle != null)
        {
            columns.Add(Cycle.MarkColumn.Name);
            columns.Add(Cycle.PathColumn.Name);
        }
        return result with { SelectableItems = columns };
    }

    private PseudoTable CreateResultTable(SourceRef? sourceRef, bool relaxed)
    {
        if (Columns.SafeAny())
        {
            return new PseudoTable(Name.Name, null, [.. Columns.Select(column => column.Name)], PseudoTableType.CommonTableExpression);
        }
        if (Query is Select { Body: SelectBody.ValuesQuery values })
        {
            int count = values.Values.Rows.FirstOrDefault()?.Count ?? 0;
            return new PseudoTable(Name.Name, null, [.. Enumerable.Range(1, count).Select(index => $"column{index}")], PseudoTableType.CommonTableExpression);
        }
        Select result;
        if (Query is Select select)
        {
            result = select;
        }
        else
        {
            SqlValueList<SimpleSelectItem>? returning = Query switch
            {
                Insert insert => insert.Returning,
                Update update => update.Returning,
                Delete delete => delete.Returning,
                _ => throw new NotSupportedException()
            };
            result = new Select(new SelectBody.SimpleSelectQuery(new SimpleSelect(returning ?? [])));
        }
        return relaxed
            ? result.ToPseudoTableRelaxed(Name.Name, null, sourceRef, PseudoTableType.CommonTableExpression)
            : result.ToPseudoTable(Name.Name, null, sourceRef, PseudoTableType.CommonTableExpression);
    }

    private string? GetMaterializedSql()
    {
        if (Materialized is null)
        {
            return null;
        }
        return Materialized.Value ? "MATERIALIZED " : "NOT MATERIALIZED ";
    }
}
