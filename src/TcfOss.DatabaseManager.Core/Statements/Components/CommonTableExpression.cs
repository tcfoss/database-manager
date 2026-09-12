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

    /// <inheritdoc cref="CommonTableExpression.AddTablesToContext" />
    public void AddTablesToContext(PseudoTableSet pseudoTableSet, SourceRef? sourceRef = null)
    {
        foreach (CommonTableExpressionBody table in Tables)
        {
            table.AddTablesToContext(pseudoTableSet, sourceRef);
        }
    }
}
