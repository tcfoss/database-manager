using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.MsSql.Statements;

/// <summary>
/// T-SQL basic <c>RAISERROR ( message , severity , state [ , argument , ... ] )</c>.
/// </summary>
// TODO: Support the WITH LOG | NOWAIT | SETERROR options.
public record MsRaiseError(Expression Message, Expression Severity, Expression State) : Statement
{
    /// <summary>Optional substitution arguments for printf-style format specifiers in <see cref="Message"/>.</summary>
    public SqlValueList<Expression>? Arguments { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"RAISERROR({Message}, {Severity}, {State}");
        if (Arguments != null)
        {
            foreach (Expression arg in Arguments)
            {
                writer.WriteSql($", {arg}");
            }
        }
        writer.Write(")");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("RAISERROR(");
        Message.FormatSql(writer, manager);
        writer.Write(", ");
        Severity.FormatSql(writer, manager);
        writer.Write(", ");
        State.FormatSql(writer, manager);
        if (Arguments != null)
        {
            foreach (Expression arg in Arguments)
            {
                writer.Write(", ");
                arg.FormatSql(writer, manager);
            }
        }
        writer.Write(")");
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Message.GetReferencedItems(context))
        {
            yield return item;
        }
        foreach (ItemRef item in Severity.GetReferencedItems(context))
        {
            yield return item;
        }
        foreach (ItemRef item in State.GetReferencedItems(context))
        {
            yield return item;
        }
        if (Arguments != null)
        {
            foreach (Expression arg in Arguments)
            {
                foreach (ItemRef item in arg.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }
}
