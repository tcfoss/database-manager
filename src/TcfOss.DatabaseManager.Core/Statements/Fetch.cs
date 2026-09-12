using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record Fetch() : Statement
{
    public abstract override void ToSql(SqlTextWriter writer);

    public record FromCursorInto(Identifier CursorName, SqlValueList<Identifier> Intos) : Fetch
    {
        public FromLabelOption? FromLabel { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            string? fromLabel = FromLabel switch
            {
                FromLabelOption.From => "FROM ",
                FromLabelOption.NextFrom => "NEXT FROM ",
                _ => null
            };
            writer.WriteSql($"FETCH {fromLabel}{CursorName} INTO {Intos.ToSqlDelimited()}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            string? fromLabel = FromLabel switch
            {
                FromLabelOption.From => "FROM ",
                FromLabelOption.NextFrom => "NEXT FROM ",
                _ => null
            };
            writer.WriteSqlI($"FETCH {fromLabel}");
            CursorName.FormatSql(writer, manager);
            writer.Write(" INTO ");
            writer.FormatDelimited(Intos, manager);
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            yield break;
        }

        public enum FromLabelOption
        {
            From,
            NextFrom
        }
    }
}
