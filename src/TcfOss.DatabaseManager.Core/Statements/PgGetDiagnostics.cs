using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record PgGetDiagnostics : Statement
{
    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }

    public record Stacked(SqlValueList<PgDiagnosticAssignment.Stacked> Assignments)
        : PgGetDiagnostics
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("GET STACKED DIAGNOSTICS ");
            writer.WriteDelimited(Assignments);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("GET STACKED DIAGNOSTICS ");
            for (int i = 0; i < Assignments.Count; i++)
            {
                if (i > 0)
                {
                    writer.Write(", ");
                }
                Assignments[i].FormatSql(writer, manager);
            }
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record Current(SqlValueList<PgDiagnosticAssignment.Current> Assignments)
        : PgGetDiagnostics
    {
        public bool PrintCurrent { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("GET ");
            if (PrintCurrent)
            {
                writer.Write("CURRENT ");
            }
            writer.Write("DIAGNOSTICS ");
            writer.WriteDelimited(Assignments);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("GET ");
            if (PrintCurrent)
            {
                writer.Write("CURRENT ");
            }
            writer.Write("DIAGNOSTICS ");
            for (int i = 0; i < Assignments.Count; i++)
            {
                if (i > 0)
                {
                    writer.Write(", ");
                }
                Assignments[i].FormatSql(writer, manager);
            }
            Meta.FormatPostNonSql(writer, manager);
        }
    }
}
