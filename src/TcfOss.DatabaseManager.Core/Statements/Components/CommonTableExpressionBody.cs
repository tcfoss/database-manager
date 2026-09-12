using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record CommonTableExpressionBody(Identifier Name, SqlValueList<Identifier>? Columns, Select Query, Identifier? From = null) : IWriteSql, IAddTablesToContext
{
    public void ToSql(SqlTextWriter writer)
    {
        Name.ToSql(writer);
        if (Columns.SafeAny())
        {
            writer.WriteSql($" ({Columns})");
        }
        writer.WriteSql($" AS ({Query})");

        if (From != null)
        {
            writer.WriteSql($" FROM {From}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Name.FormatSql(writer, manager);
        if (Columns.SafeAny())
        {
            writer.WriteSql($" ({Columns})");
        }
        writer.WriteLine(" AS (");
        manager.IncreaseIndent();
        Query.FormatSql(writer, manager);
        manager.DecreaseIndent();
        writer.Write(")");

        if (From != null)
        {
            writer.WriteSqlI($"FROM {From}");
        }
    }

    /// <inheritdoc cref="CommonTableExpression.AddTablesToContext" />
    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    {
        string sourceName = Name.Name;
        PseudoTable newSource = Columns.SafeAny()
            ? new PseudoTable(sourceName, null, [.. Columns.Select(c => c.Name)], PseudoTableType.CommonTableExpression)
            : Query.ToPseudoTableRelaxed(sourceName, null, sourceRef, PseudoTableType.CommonTableExpression);
        pseudoTableSet.AddExternalSource(sourceName, newSource);
    }
}
