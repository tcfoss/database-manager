using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record AlterSequence(ObjectName Name, PgAlterSequenceOperation Operation) : Statement
{
    public bool IfExists { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("ALTER SEQUENCE ");
        if (IfExists)
        {
            writer.Write("IF EXISTS ");
        }
        writer.WriteSql($"{Name} {Operation}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("ALTER SEQUENCE ");
        if (IfExists)
        {
            writer.Write("IF EXISTS ");
        }
        Name.FormatSql(writer, manager);
        writer.Write(" ");
        Operation.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.SequenceTarget))
        {
            ItemRef? item = context.CreateObjectRef(Name);
            if (item != null)
            {
                yield return item;
            }
        }
        if (Operation is PgAlterSequenceOperation.Parameters parameters)
        {
            foreach (ItemRef item in parameters.Options.OfType<PgStatementSequenceOption.OwnedBy>().SelectMany(option => option.GetReferencedItems(context)))
            {
                yield return item;
            }
        }
    }
}
