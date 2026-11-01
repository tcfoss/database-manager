using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record PgRoutineBody(Identifier? Language) : Statement
{
    public bool UseAsBeforeBody { get; init; }

    public record Literal(string RawLiteral, Identifier? Language) : PgRoutineBody(Language)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(RawLiteral);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.Write(manager.Indent);
            writer.Write(RawLiteral);
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }
    }

    public record StatementBody(Statement Body, Identifier? Language) : PgRoutineBody(Language)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Body}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Body.FormatSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Body.GetReferencedItems(context);
        }
    }
}
