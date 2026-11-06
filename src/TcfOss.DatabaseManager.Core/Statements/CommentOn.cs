using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record CommentOn(PgStatementCommentTarget Target, Value Comment) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"COMMENT ON {Target} IS {Comment}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.Write("COMMENT ON ");
        Target.FormatSql(writer, manager);
        writer.Write(" IS ");
        Comment.ToSql(writer);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        ObjectName? name = Target switch
        {
            PgStatementCommentTarget.DatabaseObject databaseObject => databaseObject.Name,
            PgStatementCommentTarget.Column { Name.Values.Count: > 1 } column => new ObjectName([.. column.Name.Values[..^1]]),
            PgStatementCommentTarget.Routine routine => routine.Name,
            PgStatementCommentTarget.Trigger trigger => trigger.OnTable,
            _ => null
        };
        if (name != null)
        {
            ItemRef? reference = Target is PgStatementCommentTarget.Routine
                ? context.CreateFunctionOrProcRef(name) : context.CreateObjectRef(name);
            if (reference != null)
            {
                yield return reference;
            }
        }
    }
}
