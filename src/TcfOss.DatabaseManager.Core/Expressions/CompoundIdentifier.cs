using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record CompoundIdentifier(SqlValueList<Identifier> Identifiers) : Expression
{
    public SourceRef? Source => SourceRef.FromIdentifiers(Identifiers);

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteDelimited(Identifiers, ".");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        SqlValueList<Identifier> identifiers = Identifiers;

        if (manager.ExpectingQualifiedColumn)
        {
            SqlValueList<Identifier>? sourcedIdentifiers = manager.GetQualifiedSelectableItem(Identifiers);
            if (sourcedIdentifiers != null)
            {
                identifiers = sourcedIdentifiers;
            }
        }

        identifiers = manager.QuoteIdentifiers(Identifiers, identifiers);
        writer.WriteDelimited(identifiers, ".");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        ItemRef? item = context.CreateItemRef(this);
        if (item != null)
        {
            yield return item;
        }
    }
}
