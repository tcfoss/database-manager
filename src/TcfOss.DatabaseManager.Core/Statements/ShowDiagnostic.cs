using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record ShowDiagnostic(ShowDiagnosticType DiagnosticType) : Statement
{
    public record Count(ShowDiagnosticType DiagnosticType) : ShowDiagnostic(DiagnosticType)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SHOW COUNT(*) {DiagnosticType}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI($"SHOW COUNT(*) {DiagnosticType}");
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }

    public record Values(ShowDiagnosticType DiagnosticType) : ShowDiagnostic(DiagnosticType)
    {
        public Limit? Limit { get; init; }
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SHOW {DiagnosticType}");
            if (Limit != null)
            {
                writer.WriteSql($" {Limit}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI($"SHOW {DiagnosticType}");
            if (Limit != null)
            {
                writer.WriteSql($" {Limit}");
            }
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }
}
